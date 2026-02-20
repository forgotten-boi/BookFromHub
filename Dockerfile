# ── Stage 1: build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# ── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install Pandoc + a minimal LaTeX engine for PDF generation
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      pandoc \
      texlive-xetex \
      texlive-fonts-recommended \
      texlive-latex-extra \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Azure Container Apps listens on 8080 by default
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BookFromHub.dll"]
