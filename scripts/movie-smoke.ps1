# Build + Movie Maker smoke. Sem a pasta frames/ o Godot falha quadro a quadro.
# Uso: ./scripts/movie-smoke.ps1
# Opcional: -QuitAfter 70 -Fps 60

param(
    [int] $QuitAfter = 70,
    [int] $Fps = 60,
    [string] $FramesDir = "frames"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

. "$PSScriptRoot/Resolve-Godot.ps1"

Write-Host "dotnet build..."
dotnet build --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build falhou."
}

New-Item -ItemType Directory -Force -Path $FramesDir | Out-Null
$out = Join-Path $FramesDir "f.png"

Write-Host "Movie Maker -> $out (fps=$Fps, quit-after=$QuitAfter)"
& $GodotExe --path . --write-movie $out --fixed-fps $Fps --quit-after $QuitAfter
if ($LASTEXITCODE -ne 0) {
    throw "Godot Movie Maker saiu com codigo $LASTEXITCODE."
}

Write-Host "Ok. Quadros em $FramesDir/"
