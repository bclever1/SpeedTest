# SpeedTest Updater — polls git for changes, rebuilds and restarts
$ErrorActionPreference = "Continue"
$repoDir = "C:\Development\SpeedTest"
Set-Location $repoDir

$userProfile = [System.Environment]::GetFolderPath("UserProfile")
if ($userProfile -eq "C:\WINDOWS\system32\config\systemprofile") {
    $profiles = Get-ChildItem "C:\Users" -Directory | Where-Object { Test-Path "$($_.FullName)\.config\gh" }
    if ($profiles) { $userProfile = $profiles[0].FullName }
}
$env:HOME = $userProfile
$env:GIT_TERMINAL_PROMPT = "0"
$env:GIT_ASKPASS = ""
$git = "C:\Program Files\Git\cmd\git.exe"

# Detect role: API (central server) or Worker
$isApi = (Get-Service -Name "SpeedTest" -ErrorAction SilentlyContinue) -ne $null
$isWorker = (Get-Service -Name "SpeedTestWorker" -ErrorAction SilentlyContinue) -ne $null

Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Role: API=$isApi Worker=$isWorker"

while ($true) {
    try {
        Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Checking for updates..."
        & $git fetch origin 2>&1 | Out-Null
        $local = (& $git rev-parse HEAD 2>$null).Trim()
        $remote = (& $git rev-parse origin/main 2>$null).Trim()

        if ($local -and $remote -and $local -ne $remote) {
            Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Update found: $($local.Substring(0,7)) -> $($remote.Substring(0,7))"
            & $git pull 2>&1 | Out-Null

            if ($isApi) {
                Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Rebuilding API..."
                net stop SpeedTest 2>&1 | Out-Null
                Start-Sleep -Seconds 3

                Push-Location "$repoDir\client"
                npx vite build 2>&1 | Out-Null
                Pop-Location

                dotnet publish "$repoDir\SpeedTest.Api" -c Release -o "$repoDir\SpeedTest.Api\publish" 2>&1 | Out-Null

                if (Test-Path "$repoDir\SpeedTest.Api\publish\wwwroot") {
                    Remove-Item "$repoDir\SpeedTest.Api\publish\wwwroot" -Recurse -Force
                }
                Copy-Item "$repoDir\client\dist" "$repoDir\SpeedTest.Api\publish\wwwroot" -Recurse

                net start SpeedTest 2>&1 | Out-Null
                Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] API updated and restarted"
            }

            if ($isWorker) {
                Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Rebuilding Worker..."
                net stop SpeedTestWorker 2>&1 | Out-Null
                Start-Sleep -Seconds 3

                dotnet publish "$repoDir\SpeedTest.Worker" -c Release -o "$repoDir\SpeedTest.Worker\publish" 2>&1 | Out-Null

                net start SpeedTestWorker 2>&1 | Out-Null
                Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Worker updated and restarted"
            }
        }
    } catch {
        Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Updater error: $_"
    }
    Start-Sleep -Seconds 60
}
