using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("github", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BookFromHub/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/generate", async (GenerateRequest req, IHttpClientFactory httpClientFactory) =>
{
    // Step 1: Parse repo URL
    if (string.IsNullOrWhiteSpace(req.RepoUrl))
        return Results.BadRequest("repoUrl is required.");

    string owner, repo;
    try
    {
        var uri = new Uri(req.RepoUrl.Trim().TrimEnd('/'));
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            return Results.BadRequest("Invalid GitHub repository URL.");
        owner = segments[0];
        repo = segments[1];
    }
    catch
    {
        return Results.BadRequest("Invalid GitHub repository URL.");
    }

    var http = httpClientFactory.CreateClient("github");

    // Step 2: Call GitHub API to list repo contents
    List<GitHubFile> mdFiles;
    try
    {
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents";
        var response = await http.GetAsync(apiUrl);
        if (!response.IsSuccessStatusCode)
            return Results.Problem($"GitHub API error: {response.StatusCode}", statusCode: 500);

        var json = await response.Content.ReadAsStringAsync();
        var allFiles = JsonSerializer.Deserialize<List<GitHubFile>>(json, JsonOptions.Default) ?? [];
        mdFiles = allFiles
            .Where(f => f.Type == "file" && f.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to fetch repository contents: {ex.Message}", statusCode: 500);
    }

    if (mdFiles.Count == 0)
        return Results.BadRequest("No markdown (.md) files found in the repository root.");

    // Step 3 & 4: Download and sort alphabetically
    mdFiles = [.. mdFiles.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];

    var contentParts = new List<string>();
    foreach (var file in mdFiles)
    {
        if (string.IsNullOrEmpty(file.DownloadUrl))
            continue;
        try
        {
            var content = await http.GetStringAsync(file.DownloadUrl);
            contentParts.Add(content);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to download {file.Name}: {ex.Message}", statusCode: 500);
        }
    }

    if (contentParts.Count == 0)
        return Results.BadRequest("No downloadable markdown files found.");

    // Step 5: Concatenate with page breaks
    var combined = string.Join("\n\n\\newpage\n\n", contentParts);

    // Step 6: Write to temp file
    var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(tempDir);
    var mdPath = Path.Combine(tempDir, "book.md");
    var pdfPath = Path.Combine(tempDir, "book.pdf");

    try
    {
        await File.WriteAllTextAsync(mdPath, combined, Encoding.UTF8);

        // Step 7: Run Pandoc
        var psi = new ProcessStartInfo
        {
            FileName = "pandoc",
            Arguments = $"\"{mdPath}\" -o \"{pdfPath}\" --toc",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new Exception("Failed to start pandoc process.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            return Results.Problem($"Pandoc failed: {stderr}", statusCode: 500);

        // Step 8: Return PDF
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
        return Results.File(pdfBytes, "application/pdf", $"{repo}-book.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem($"PDF generation failed: {ex.Message}", statusCode: 500);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
});

app.Run();

record GenerateRequest([property: JsonPropertyName("repoUrl")] string RepoUrl);

record GitHubFile(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("download_url")] string? DownloadUrl
);

static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
