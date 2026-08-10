# Cobertura local do motor + Bridge pura (coverlet.collector já no csproj).
# Uso: ./scripts/coverage.ps1
# Abre coverage/report/index.html ao terminar (quando ReportGenerator roda).

param(
    [switch] $SkipReport,
    [switch] $Open
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$rawDir = Join-Path $root "coverage/raw"
$reportDir = Join-Path $root "coverage/report"

if (Test-Path $rawDir) {
    Remove-Item -Recurse -Force $rawDir
}

Write-Host "dotnet test + XPlat Code Coverage..."
dotnet test Tests/SolarSim.Tests.csproj --collect:"XPlat Code Coverage" --results-directory $rawDir --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test falhou."
}

$cobertura = Get-ChildItem -Path $rawDir -Recurse -Filter "coverage.cobertura.xml" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $cobertura) {
    throw "Nenhum coverage.cobertura.xml em $rawDir"
}

Write-Host "Cobertura: $($cobertura.FullName)"

if ($SkipReport) {
    exit 0
}

$manifest = Join-Path $root ".config/dotnet-tools.json"
if (-not (Test-Path $manifest)) {
    Write-Host "Criando tool-manifest local..."
    New-Item -ItemType Directory -Force -Path (Join-Path $root ".config") | Out-Null
    dotnet new tool-manifest --force | Out-Null
}

if (-not (Test-Path $manifest)) {
    throw "Falha ao criar $manifest"
}

$toolsJson = Get-Content $manifest -Raw
if ($toolsJson -notmatch "dotnet-reportgenerator-globaltool") {
    Write-Host "Instalando ReportGenerator (ferramenta local)..."
    dotnet tool install dotnet-reportgenerator-globaltool | Out-Null
}

Write-Host "ReportGenerator -> $reportDir"
dotnet tool run reportgenerator `
    "-reports:$($cobertura.FullName)" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;TextSummary" `
    "-classfilters:-*Tests*"
if ($LASTEXITCODE -ne 0) {
    throw "ReportGenerator falhou."
}

$summary = Join-Path $reportDir "Summary.txt"
if (Test-Path $summary) {
    Get-Content $summary | Select-Object -First 20
}

if ($Open) {
    $index = Join-Path $reportDir "index.html"
    if (Test-Path $index) {
        Start-Process $index
    }
}

Write-Host "Ok. HTML em coverage/report/"
