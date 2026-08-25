# SpeedTest

Internet speed test app — .NET 10 API backend + React frontend.

## Deployment

Run `./deploy.sh` or `bash deploy.sh` from the project root to build and deploy to the Google VM (35.237.133.46).

## Ports
- Dev API: http://localhost:5000
- Dev Frontend: http://localhost:5190 (proxies /api to backend)
- Production: port 5091 on Google VM

## Versions
- API version: `Version` in `SpeedTest.Api/Program.cs`
- UI version: `UI_VERSION` in `client/src/App.tsx`
