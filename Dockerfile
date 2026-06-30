# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder
WORKDIR /build

# Copy project files
COPY JobHunterApp/ ./JobHunterApp/
COPY JobHunterApp.Tests/ ./JobHunterApp.Tests/

# Restore and build
RUN dotnet restore JobHunterApp/JobHunterApp.csproj
RUN dotnet build JobHunterApp/JobHunterApp.csproj -c Release

# Test stage
FROM builder AS tester
RUN dotnet test JobHunterApp.Tests/JobHunterApp.Tests.csproj -c Release

# Runtime stage (headless - no GUI in container)
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022
WORKDIR /app

# Copy built app from builder
COPY --from=builder /build/JobHunterApp/bin/Release/ ./

# Set environment
ENV JOBHUNTER_HEADLESS=true
ENV JOBHUNTER_CONFIG_DIR=/app/config

# Create config directory
RUN mkdir -p /app/config

# Entrypoint: just sleep (app is GUI, won't run interactively)
CMD ["powershell", "-Command", "Write-Host 'Job Hunter App (Headless Mode)'; Write-Host 'Config directory: /app/config'; Start-Sleep -Seconds 3600"]
