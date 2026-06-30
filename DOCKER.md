# Job Hunter - Docker Setup

Run Job Hunter locally in Docker containers for development and testing.

## Quick Start

### Windows
```bash
.\start-docker.bat
```

### Linux / macOS
```bash
chmod +x start-docker.sh
./start-docker.sh
```

## What It Does

- **Builds** the .NET 9 application in a Docker image
- **Runs tests** automatically during build
- **Starts** a container with the app in headless mode
- **Mounts** config directory for persistent settings

## Docker Compose Commands

**Start (from scratch)**
```bash
docker-compose up --build
```

**Start (use existing image)**
```bash
docker-compose up -d
```

**Stop**
```bash
docker-compose down
```

**View logs**
```bash
docker-compose logs -f job-hunter
```

**Restart**
```bash
docker-compose restart job-hunter
```

**Rebuild image**
```bash
docker-compose build --no-cache
```

**Remove everything (containers + images)**
```bash
docker-compose down --rmi all
```

## File Structure

```
job-hunter/
├── Dockerfile              # Build instructions
├── docker-compose.yml      # Container orchestration
├── start-docker.bat        # Windows startup script
├── start-docker.sh         # Linux/macOS startup script
├── config/                 # Persistent config (mounted)
├── JobHunterApp/
├── JobHunterApp.Tests/
└── ...
```

## Configuration

Config files are stored in `./config/` directory (mounted at `/app/config` in container):
- `config.json` — API keys, browser settings, CV paths
- `search_history.json` — Search history
- `applied_jobs.json` — Applied job tracking

These persist between container restarts.

## Headless Mode

The app runs in **headless mode** inside Docker (no GUI). Use for:
- **CI/CD pipelines** (testing, builds)
- **Batch processing** (automated job runs)
- **Server deployments** (if you add an API layer later)
- **Cross-platform testing**

For interactive use, run locally:
```bash
dotnet run --project JobHunterApp/JobHunterApp.csproj
```

## Requirements

- **Docker Desktop** installed and running
- **2GB+ RAM** recommended
- **.NET SDK 9.0** (inside container, not required on host)

## Troubleshooting

**"Docker daemon not running"**
- Open Docker Desktop application

**"Build failed"**
- Check `docker-compose logs job-hunter` for errors
- Verify `.csproj` files are present and valid

**"Permission denied" on Linux/Mac**
- Run `chmod +x start-docker.sh` before executing

**Clean rebuild**
```bash
docker-compose down --rmi all
docker-compose up --build
```

## Notes

- The Dockerfile uses **Windows Server Core** base image (for .NET Framework compatibility)
- For Linux containers, modify `Dockerfile` to use `mcr.microsoft.com/dotnet/runtime:9.0-alpine`
- Config persistence requires Docker volume mounts (`./config:/app/config`)
