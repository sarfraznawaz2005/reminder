# Cleans and runs the app in dev mode (Debug build via `dotnet run`).
# Note: Windows-toast button activation (Snooze/Done) doesn't work under `dotnet run` -
# that's expected in dev mode. Use build.ps1 and run the published exe to test that.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\Reminder\Reminder.csproj'

Write-Host 'Cleaning previous build output...' -ForegroundColor Cyan
foreach ($dir in @((Join-Path $root 'src\Reminder\bin'), (Join-Path $root 'src\Reminder\obj'))) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

Write-Host 'Running in dev mode...' -ForegroundColor Cyan
dotnet run --project $project -c Debug

exit $LASTEXITCODE
