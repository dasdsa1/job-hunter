@echo off
REM Start Job Hunter in Docker (Windows)

setlocal enabledelayedexpansion

echo.
echo 🐳 Job Hunter - Docker Local Development
echo ========================================

REM Check Docker is installed
where docker >nul 2>nul
if errorlevel 1 (
    echo ❌ Docker not found. Install Docker Desktop and try again.
    exit /b 1
)

REM Check Docker is running
docker info >nul 2>nul
if errorlevel 1 (
    echo ❌ Docker daemon not running. Start Docker Desktop.
    exit /b 1
)

echo ✓ Docker is ready
echo.

REM Create config directory if it doesn't exist
if not exist config mkdir config

REM Build image
echo 📦 Building Docker image...
docker-compose build --no-cache
if errorlevel 1 (
    echo ❌ Build failed
    exit /b 1
)

REM Start container
echo.
echo 🚀 Starting Job Hunter container...
docker-compose up -d
if errorlevel 1 (
    echo ❌ Failed to start container
    exit /b 1
)

REM Show status
echo.
echo ✓ Container started!
echo.
echo Container info:
docker-compose ps

echo.
echo 📁 Config directory: .\config (mounted at /app/config in container)
echo 📋 Logs: docker-compose logs -f job-hunter
echo 🛑 Stop: docker-compose down
echo ♻️  Restart: docker-compose restart
echo.
echo ✅ Job Hunter is running in Docker (headless mode)
echo.
