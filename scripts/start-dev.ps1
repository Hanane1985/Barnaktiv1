$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$frontend = Join-Path $root "frontend\web"

function Start-BarnaktivProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Title,

        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string] $Command
    )

    Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-Command",
        "Set-Location '$WorkingDirectory'; `$Host.UI.RawUI.WindowTitle = '$Title'; $Command"
    )
}

Start-BarnaktivProcess `
    -Title "Barnaktiv API" `
    -WorkingDirectory $root `
    -Command "dotnet run --project backend/Barnaktiv.API"

Start-BarnaktivProcess `
    -Title "Barnaktiv Worker" `
    -WorkingDirectory $root `
    -Command "dotnet run --project backend/Barnaktiv.Worker"

Start-BarnaktivProcess `
    -Title "Barnaktiv Web" `
    -WorkingDirectory $frontend `
    -Command "npm run dev"

Write-Host "Started Barnaktiv API, Worker, and Web in separate PowerShell windows."
Write-Host "The Worker runs ingestion automatically on startup and then on its configured interval."
