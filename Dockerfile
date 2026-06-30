# ── Stage 1: build the headless worker ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only what's needed for restore (layer cache)
COPY JobHunterApp.Worker/JobHunterApp.Worker.csproj ./JobHunterApp.Worker/
RUN dotnet restore JobHunterApp.Worker/JobHunterApp.Worker.csproj

# Copy shared source referenced via <Compile Include="../...">
COPY JobHunterApp/Models/   ./JobHunterApp/Models/
COPY JobHunterApp/Services/ ./JobHunterApp/Services/
COPY JobHunterApp.Worker/   ./JobHunterApp.Worker/

RUN dotnet publish JobHunterApp.Worker/JobHunterApp.Worker.csproj \
    -c Release -o /app --self-contained

# Install Playwright CLI so we can download Chromium into the publish folder
RUN dotnet tool install --global Microsoft.Playwright.CLI 2>/dev/null || true
RUN /app/JobHunterApp.Worker --version 2>/dev/null || true

# ── Stage 2: runtime with Playwright Chromium ──────────────────────────────────
# Official Playwright image ships Chromium + all native deps for the matching version
FROM mcr.microsoft.com/playwright/dotnet:v1.47.0-noble
WORKDIR /app

# Copy published app
COPY --from=build /app .

# Install Playwright browsers inside the image
RUN pwsh -Command "./JobHunterApp.Worker install chromium" 2>/dev/null \
 || dotnet JobHunterApp.Worker.dll install chromium 2>/dev/null \
 || true

ENV JOBHUNTER_HEADLESS=true
ENV JOBHUNTER_DATA_DIR=/data
# Playwright finds its bundled Chromium here (set by the base image)
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# /data is mounted by docker-compose (config.json, search.json, browser-profile-chromium/, reports/)
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "JobHunterApp.Worker.dll"]
