#!/bin/bash
# Start Job Hunter in Docker (Linux/Mac)

set -e

echo "🐳 Job Hunter - Docker Local Development"
echo "========================================"

# Check Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker not found. Install Docker Desktop and try again."
    exit 1
fi

# Check Docker is running
if ! docker info &> /dev/null; then
    echo "❌ Docker daemon not running. Start Docker Desktop."
    exit 1
fi

echo "✓ Docker is ready"

# Create config directory if it doesn't exist
mkdir -p ./config

# Build image
echo ""
echo "📦 Building Docker image..."
docker-compose build --no-cache

# Start container
echo ""
echo "🚀 Starting Job Hunter container..."
docker-compose up -d

# Show status
echo ""
echo "✓ Container started!"
echo ""
echo "Container info:"
docker-compose ps

echo ""
echo "📁 Config directory: ./config (mounted at /app/config in container)"
echo "📋 Logs: docker-compose logs -f job-hunter"
echo "🛑 Stop: docker-compose down"
echo "♻️  Restart: docker-compose restart"
echo ""
echo "✅ Job Hunter is running in Docker (headless mode)"
