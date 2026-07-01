# Deploying Job Hunter to EC2

This guide covers deploying the headless job-hunter Worker to an AWS EC2 instance.

## Prerequisites

- AWS EC2 instance (Ubuntu 24.04 LTS recommended, t3.micro or larger)
- Docker and Docker Compose installed on EC2
- Gemini API key (from [aistudio.google.com/app/apikey](https://aistudio.google.com/app/apikey))
- SSH access to your EC2 instance

## Quick Start

1. **Build the Docker image locally:**

```bash
cd job-hunter
docker build -t job-hunter-worker:latest .
```

2. **Option A: Push to a registry (e.g., Docker Hub, ECR)**

```bash
docker tag job-hunter-worker:latest your-registry/job-hunter-worker:latest
docker push your-registry/job-hunter-worker:latest
```

3. **Option B: Copy the image directly to EC2**

```bash
# This creates a tar archive and transfers it (slow for large images)
docker save job-hunter-worker:latest | ssh ec2-user@your-ec2-ip docker load
```

4. **On your EC2 instance:**

```bash
# Create directory structure
mkdir -p ~/job-hunter/data
cd ~/job-hunter

# Copy docker-compose.yml from the repo
scp docker-compose.yml ec2-user@your-ec2-ip:~/job-hunter/

# Set up environment variables
cat > .env <<EOF
GEMINI_API_KEY=AIza...
SEARCH_TITLE=Software Engineer
SEARCH_LOCATION=Remote
SEARCH_MIN_SCORE=6
SEARCH_MAX_JOBS=20
EOF

# Run once to test
docker compose up

# (Optional) Or run in the background
docker compose up -d
```

## Scheduling Runs

To run job searches automatically on a schedule, use cron:

```bash
# Edit crontab
crontab -e

# Add a daily 9 AM run
0 9 * * * cd ~/job-hunter && docker compose up >> ~/job-hunter/logs/runs.log 2>&1
```

## Data Persistence

The `/data` volume in docker-compose.yml persists:
- `config.json` — setup configuration
- `search.json` — search parameters
- `reports/` — generated HTML reports
- `browser-profile-chromium/` — browser session/login state

Mount this to your EC2 instance to keep data across runs:

```yaml
volumes:
  - /home/ec2-user/job-hunter/data:/data
```

## Monitoring

Check logs from the last run:

```bash
# On EC2
cd ~/job-hunter
ls -la data/reports/
cat logs/runs.log | tail -50
```

## Troubleshooting

**"API key not found"** — Make sure `.env` file exists with `GEMINI_API_KEY` set.

**"Chrome/Chromium not found"** — The Docker image includes Playwright Chromium. If using remote LinkedIn scraping, ensure the browser can access it.

**"Out of memory"** — Job scoring is parallelized in batches. Reduce `SEARCH_MAX_JOBS` or choose a larger EC2 instance.

## Integration with CI/CD

To add automated deployments:

1. Push image to ECR/Docker Hub in your CI pipeline
2. SSH into EC2 and trigger `docker compose pull && docker compose up`
3. Or use a container orchestration tool (ECS, Kubernetes) for true production deployments

## Security Notes

- Never commit `.env` files with real API keys to version control
- Use AWS Secrets Manager or EC2 Systems Manager Parameter Store for sensitive values
- The Worker runs in headless mode; no user interaction is possible
- All data is local to the EC2 instance; no external data storage is assumed
