#!/bin/bash
set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
SSH_KEY=~/.ssh/google_compute_engine
VM=brian@35.237.133.46
REMOTE_DIR=/home/brian/speedtest

echo "Building frontend..."
cd "$ROOT/client"
npx vite build

echo "Publishing backend..."
cd "$ROOT/SpeedTest.Api"
dotnet publish -c Release -o ./publish

echo "Deploying to VM..."
scp -i "$SSH_KEY" -r "$ROOT/SpeedTest.Api/publish/"* "$VM:$REMOTE_DIR/"
scp -i "$SSH_KEY" -r "$ROOT/client/dist/"* "$VM:$REMOTE_DIR/wwwroot/"

echo "Restarting service..."
ssh -i "$SSH_KEY" "$VM" "sudo systemctl restart speedtest"

echo "Deployed successfully!"
