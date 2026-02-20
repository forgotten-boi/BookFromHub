# BookFromHub

> Turn any public GitHub repository's Markdown files into a downloadable PDF book — in one click.

BookFromHub is a minimal **ASP.NET 10** web application that:

1. Accepts a public GitHub repository URL
2. Fetches every `.md` file from the repository root via the GitHub REST API
3. Sorts them alphabetically and concatenates them with page breaks
4. Converts the combined Markdown to a PDF using **Pandoc + XeLaTeX**
5. Returns the PDF directly to the browser as a file download

---

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Running Locally](#running-locally)
- [Configuration](#configuration)
- [GitHub Rate-Limit Protection](#github-rate-limit-protection)
- [Docker](#docker)
- [Deploying to Azure](#deploying-to-azure)
- [API Reference](#api-reference)
- [Project Structure](#project-structure)

---

## Features

| Feature | Detail |
|---|---|
| Single-page UI | Dark glassmorphism theme, spinner feedback |
| One endpoint | `POST /generate` — no database, no auth required |
| GitHub rate-limit aware | Detects `403`/`429`, surfaces reset time, supports Bearer token |
| Containerised | Multi-stage `Dockerfile`, exposes port **8080** (Azure default) |
| Zero over-engineering | Single `Program.cs`, no extra layers or frameworks |

---

## Architecture

```
Browser  →  POST /generate { repoUrl }
              │
              ▼
        Parse owner/repo from URL
              │
              ▼
        GitHub REST API
        GET /repos/{owner}/{repo}/contents
              │
              ▼
        Filter .md files  →  download raw content
              │
              ▼
        Sort alphabetically  →  concatenate (with \newpage)
              │
              ▼
        Write book.md  →  pandoc book.md -o book.pdf --toc
              │
              ▼
        Return PDF as file download
```

---

## Prerequisites

### Local development

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | `dotnet --version` |
| [Pandoc](https://pandoc.org/installing.html) | any recent | `pandoc --version` |
| A LaTeX engine | e.g. `texlive-xetex` | required by Pandoc for PDF output |

**Linux / WSL quick install:**

```bash
sudo apt-get install -y pandoc texlive-xetex texlive-fonts-recommended texlive-latex-extra
```

**macOS:**

```bash
brew install pandoc
brew install --cask mactex-no-gui   # or: brew install basictex
```

### Docker / Azure

Only **Docker** is needed — Pandoc and LaTeX are installed inside the image.

---

## Running Locally

```bash
# 1. Clone
git clone https://github.com/forgotten-boi/BookFromHub.git
cd BookFromHub

# 2. Run
dotnet run --launch-profile http

# 3. Open
open http://localhost:5014
```

Enter any public GitHub repository URL (e.g. `https://github.com/microsoft/generative-ai-for-beginners`) and click **Generate PDF**.

---

## Configuration

Settings are read from `appsettings.json`, environment variables, or Azure App Settings.

| Key | Env var | Default | Description |
|---|---|---|---|
| `GitHub:Token` | `GitHub__Token` | _(empty)_ | Optional GitHub Personal Access Token. Raises API rate limit from 60 → 5 000 requests/hour. |

### Setting the token locally

**Option A — environment variable (recommended for local dev):**

```bash
export GitHub__Token=ghp_yourTokenHere
dotnet run --launch-profile http
```

**Option B — `appsettings.Development.json` (never commit this file):**

```json
{
  "GitHub": {
    "Token": "ghp_yourTokenHere"
  }
}
```

### Generating a token

1. Go to **GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)**
2. Click **Generate new token**
3. Select scopes: _no scopes needed_ for public repositories
4. Copy the token and set it as shown above

---

## GitHub Rate-Limit Protection

Without a token, GitHub allows **60 unauthenticated API requests per hour** per IP address. Each call to `/generate` uses at least one API request (listing contents) plus one per markdown file downloaded.

When the rate limit is exceeded the app returns **HTTP 429** with a human-readable message including the exact reset time:

```json
{
  "status": 429,
  "detail": "GitHub API rate limit exceeded. Resets at 14:22:00 UTC. Provide a GitHub token via the GitHub__Token environment variable to increase the limit."
}
```

Providing a token raises the limit to **5 000 requests/hour**.

---

## Docker

### Build the image

```bash
docker build -t bookfromhub .
```

### Run locally

```bash
docker run --rm -p 8080:8080 bookfromhub
# open http://localhost:8080
```

### Run with a GitHub token

```bash
docker run --rm -p 8080:8080 \
  -e GitHub__Token=ghp_yourTokenHere \
  bookfromhub
```

> **Note:** The image is built on `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian-based) and includes Pandoc, XeLaTeX, and recommended LaTeX fonts. Expect an image size of ~1 GB due to the TeX dependencies.

---

## Deploying to Azure

### Option A — Azure Container Apps (recommended)

```bash
# 1. Variables
RESOURCE_GROUP=bookfromhub-rg
LOCATION=westeurope
ACR_NAME=bookfromhubacr          # must be globally unique
APP_ENV=bookfromhub-env
APP_NAME=bookfromhub

# 2. Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# 3. Create Azure Container Registry
az acr create --resource-group $RESOURCE_GROUP \
              --name $ACR_NAME --sku Basic --admin-enabled true

# 4. Build & push image to ACR
az acr build --registry $ACR_NAME \
             --image bookfromhub:latest .

# 5. Create Container Apps environment
az containerapp env create \
  --name $APP_ENV \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# 6. Deploy container app
az containerapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $APP_ENV \
  --image $ACR_NAME.azurecr.io/bookfromhub:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $(az acr credential show -n $ACR_NAME --query username -o tsv) \
  --registry-password $(az acr credential show -n $ACR_NAME --query passwords[0].value -o tsv) \
  --target-port 8080 \
  --ingress external \
  --env-vars GitHub__Token=secretref:github-token \
  --secrets github-token=ghp_yourTokenHere \
  --cpu 0.5 --memory 1.0Gi \
  --min-replicas 0 --max-replicas 3
```

The command prints the app URL. Open it in your browser.

### Option B — Azure App Service (Web App for Containers)

```bash
az appservice plan create \
  --name bookfromhub-plan \
  --resource-group $RESOURCE_GROUP \
  --sku B1 --is-linux

az webapp create \
  --resource-group $RESOURCE_GROUP \
  --plan bookfromhub-plan \
  --name $APP_NAME \
  --deployment-container-image-name $ACR_NAME.azurecr.io/bookfromhub:latest

az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --settings GitHub__Token=ghp_yourTokenHere WEBSITES_PORT=8080
```

---

## API Reference

### `POST /generate`

Generates a PDF book from all Markdown files in the root of a public GitHub repository.

**Request**

```
Content-Type: application/json
```

```json
{
  "repoUrl": "https://github.com/owner/repo"
}
```

**Success response**

| Field | Value |
|---|---|
| Status | `200 OK` |
| Content-Type | `application/pdf` |
| Body | Binary PDF stream (triggers browser download) |

**Error responses**

| Status | Condition |
|---|---|
| `400 Bad Request` | Missing or invalid URL, no `.md` files found |
| `429 Too Many Requests` | GitHub rate limit exceeded |
| `500 Internal Server Error` | GitHub API failure or Pandoc error |

---

## Project Structure

```
BookFromHub/
├── Program.cs              # All backend logic (ASP.NET 10 Minimal API)
├── BookFromHub.csproj      # Project file — targets net10.0
├── appsettings.json        # Default config (GitHub:Token placeholder)
├── appsettings.Development.json
├── Dockerfile              # Multi-stage build; installs Pandoc + LaTeX
├── .dockerignore
├── .gitignore
├── BookFromHub.http        # HTTP test file for VS / VS Code REST Client
├── Properties/
│   └── launchSettings.json # Local dev profiles (http: 5014, https: 7004)
└── wwwroot/
    └── index.html          # Single-page frontend
```
