# SpeedTest Worker Setup — run as Administrator on non-central machines
# Prerequisites: Git, .NET 10 SDK, NSSM, Ookla speedtest CLI
$ErrorActionPreference = "Stop"
$repoDir = "C:\Development\SpeedTest"
$publishDir = "$repoDir\SpeedTest.Worker\publish"

# Find NSSM
$nssm = Get-ChildItem "C:\Users\*\AppData\Local\Microsoft\WinGet\Packages\NSSM*" -Recurse -Filter "nssm.exe" |
    Where-Object { $_.FullName -match "win64" } | Select-Object -First 1 -ExpandProperty FullName
if (-not $nssm) {
    Write-Host "NSSM not found. Install with: winget install NSSM.NSSM"
    exit 1
}
Write-Host "Using NSSM: $nssm"

# Clone repo if needed
if (-not (Test-Path "$repoDir\.git")) {
    Write-Host "Cloning repository..."
    git clone https://github.com/bclever1/SpeedTest.git $repoDir
}

# Build worker
Write-Host "Publishing worker..."
dotnet publish "$repoDir\SpeedTest.Worker" -c Release -o $publishDir

# Install SpeedTestWorker service
Write-Host "Installing SpeedTestWorker service..."
& $nssm install SpeedTestWorker "C:\Program Files\dotnet\dotnet.exe" "$publishDir\SpeedTest.Worker.dll"
& $nssm set SpeedTestWorker AppDirectory $publishDir
& $nssm set SpeedTestWorker DisplayName "SpeedTest Worker"
& $nssm set SpeedTestWorker Description "Runs speed tests and reports to central API"
& $nssm set SpeedTestWorker Start SERVICE_AUTO_START
& $nssm set SpeedTestWorker AppStdout "$repoDir\logs\worker.log"
& $nssm set SpeedTestWorker AppStderr "$repoDir\logs\worker.log"
& $nssm set SpeedTestWorker AppRotateFiles 1
& $nssm set SpeedTestWorker AppRotateBytes 1048576

# Install SpeedTestUpdater service
Write-Host "Installing SpeedTestUpdater service..."
& $nssm install SpeedTestUpdater "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" "-ExecutionPolicy Bypass -File `"$repoDir\updater.ps1`""
& $nssm set SpeedTestUpdater AppDirectory $repoDir
& $nssm set SpeedTestUpdater DisplayName "SpeedTest Updater"
& $nssm set SpeedTestUpdater Description "Auto-updater for SpeedTest — polls git every 60s"
& $nssm set SpeedTestUpdater Start SERVICE_AUTO_START
& $nssm set SpeedTestUpdater AppStdout "$repoDir\logs\updater.log"
& $nssm set SpeedTestUpdater AppStderr "$repoDir\logs\updater.log"
& $nssm set SpeedTestUpdater AppRotateFiles 1
& $nssm set SpeedTestUpdater AppRotateBytes 1048576

# Create logs directory
if (-not (Test-Path "$repoDir\logs")) { New-Item -ItemType Directory "$repoDir\logs" | Out-Null }

# Start services
Write-Host "Starting services..."
Start-Service SpeedTestWorker
Start-Service SpeedTestUpdater

Write-Host ""
Write-Host "Done! Worker is running and reporting to http://192.168.1.200:5091"
Write-Host "Updater is polling git every 60 seconds."
