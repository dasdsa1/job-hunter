#!/bin/bash
# Deploy job-hunter Worker to EC2
# Usage: ./deploy-to-ec2.sh <EC2_HOST> <GEMINI_API_KEY> [DOCKER_REGISTRY]
# Example: ./deploy-to-ec2.sh ec2-user@1.2.3.4 "AIza..." docker.io/myrepo

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <EC2_HOST> <GEMINI_API_KEY> [DOCKER_REGISTRY]"
    echo ""
    echo "  EC2_HOST:        SSH host, e.g. ec2-user@1.2.3.4"
    echo "  GEMINI_API_KEY:  Your Gemini API key"
    echo "  DOCKER_REGISTRY: Registry prefix, e.g. docker.io/myrepo (optional, default: local)"
    echo ""
    echo "Example:"
    echo "  $0 ec2-user@1.2.3.4 'AIza...' docker.io/myorg"
    exit 1
fi

EC2_HOST="$1"
GEMINI_API_KEY="$2"
DOCKER_REGISTRY="${3:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "🔨 Building Docker image..."
cd "$PROJECT_ROOT"

# Tag with git SHA for reproducibility and rollback capability
GIT_SHA=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
docker build -t job-hunter-worker:latest -t job-hunter-worker:$GIT_SHA .

if [ -n "$DOCKER_REGISTRY" ]; then
    echo "📦 Pushing to registry: $DOCKER_REGISTRY"
    docker tag job-hunter-worker:latest "$DOCKER_REGISTRY/job-hunter-worker:latest"
    docker push "$DOCKER_REGISTRY/job-hunter-worker:latest"
    IMAGE_REF="$DOCKER_REGISTRY/job-hunter-worker:latest"
else
    IMAGE_REF="job-hunter-worker:latest"
fi

echo ""
echo "✅ Docker image built: $IMAGE_REF"
echo ""
echo "📝 To deploy on $EC2_HOST:"
echo ""
echo "  # Copy docker-compose.yml and .env to EC2:"
echo "  scp docker-compose.yml \"$EC2_HOST:~/job-hunter/\""
echo "  scp .env \"$EC2_HOST:~/job-hunter/.env\" 2>/dev/null || echo 'No .env file, skipping'"
echo ""
echo "  # SSH into EC2 and run:"
echo "  ssh \"$EC2_HOST\""
echo "  mkdir -p ~/job-hunter ~/job-hunter/data"
echo "  cd ~/job-hunter"
echo ""
if [ -n "$DOCKER_REGISTRY" ]; then
    echo "  # Pull the image from your registry:"
    echo "  docker pull $IMAGE_REF"
    echo ""
fi
echo "  # Create .env file with API key (if not copied):"
echo "  echo 'GEMINI_API_KEY=$GEMINI_API_KEY' > .env"
echo ""
echo "  # Run once:"
echo "  docker compose up"
echo ""
echo "  # Or schedule with cron (daily at 9 AM):"
echo "  0 9 * * * cd ~/job-hunter && docker compose up 2>&1 | tee -a logs/runs.log"
echo ""
