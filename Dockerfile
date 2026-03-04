# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all files and restore + publish
COPY . . 
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install Pandoc + minimal LaTeX engine + required fonts
RUN apt-get update && apt-get install -y \
    pandoc \
    texlive-xetex \
    texlive-fonts-recommended \
    texlive-latex-recommended \
    texlive-latex-extra \
    texlive-lang-english \
    fonts-texgyre \
    fonts-jetbrains-mono \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy published .NET app
COPY --from=build /app/publish .

# Listen on port 8080 (Azure Container Apps default)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "BookFromHub.dll"]
