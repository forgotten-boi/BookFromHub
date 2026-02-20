# BookFromHub — Quick Start

Get up and running in under 5 minutes.

---

## 1 · Run locally (no Docker)

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) · Pandoc · a LaTeX engine

```bash
# Install Pandoc + LaTeX (Debian/Ubuntu/WSL)
sudo apt-get install -y pandoc texlive-xetex texlive-fonts-recommended texlive-latex-extra

# macOS
brew install pandoc && brew install --cask mactex-no-gui

# Clone & run
git clone https://github.com/forgotten-boi/BookFromHub.git
cd BookFromHub
dotnet run --launch-profile http
```

Open **http://localhost:5014**, paste a public GitHub repo URL, click **Generate PDF**.

---

## 2 · Run with Docker

```bash
docker build -t bookfromhub .
docker run --rm -p 8080:8080 bookfromhub
```

Open **http://localhost:8080**

> Pandoc and LaTeX are pre-installed inside the image — no local tools needed.

---

## 3 · Avoid GitHub rate limits

Without a token you get **60 API requests/hour**. Add a [GitHub Personal Access Token](https://github.com/settings/tokens) (no scopes needed for public repos) to raise the limit to **5 000/hour**:

```bash
# Local
export GitHub__Token=ghp_yourTokenHere
dotnet run --launch-profile http

# Docker
docker run --rm -p 8080:8080 -e GitHub__Token=ghp_yourTokenHere bookfromhub
```

---

## 4 · Deploy to Azure Container Apps

```bash
RESOURCE_GROUP=bookfromhub-rg
LOCATION=westeurope
ACR_NAME=bookfromhubacr        # must be globally unique
APP_ENV=bookfromhub-env
APP_NAME=bookfromhub

# Create infrastructure
az group create --name $RESOURCE_GROUP --location $LOCATION
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME \
              --sku Basic --admin-enabled true

# Build & push to Azure Container Registry
az acr build --registry $ACR_NAME --image bookfromhub:latest .

# Create environment & deploy
az containerapp env create --name $APP_ENV \
  --resource-group $RESOURCE_GROUP --location $LOCATION

az containerapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $APP_ENV \
  --image $ACR_NAME.azurecr.io/bookfromhub:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $(az acr credential show -n $ACR_NAME --query username -o tsv) \
  --registry-password $(az acr credential show -n $ACR_NAME --query passwords[0].value -o tsv) \
  --target-port 8080 --ingress external \
  --env-vars GitHub__Token=secretref:github-token \
  --secrets github-token=ghp_yourTokenHere \
  --cpu 0.5 --memory 1.0Gi \
  --min-replicas 0 --max-replicas 3
```

The final command prints your public URL. Done. 🎉

---

## 5 · Try a sample request (curl)

```bash
curl -X POST http://localhost:5014/generate \
     -H "Content-Type: application/json" \
     -d '{"repoUrl":"https://github.com/microsoft/generative-ai-for-beginners"}' \
     --output book.pdf
```

---

## What happens next?

| Scenario | Where to look |
|---|---|
| Full configuration options | [README.md → Configuration](README.md#configuration) |
| Azure App Service deployment | [README.md → Deploying to Azure](README.md#deploying-to-azure) |
| Rate-limit details | [README.md → GitHub Rate-Limit Protection](README.md#github-rate-limit-protection) |
| HTTP test file | `BookFromHub.http` (VS / VS Code REST Client) |
