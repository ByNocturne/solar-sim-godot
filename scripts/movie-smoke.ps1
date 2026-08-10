# Build + Movie Maker smoke com coreografia (Fobos, Bennu, sonda, Δv).
# Uso: ./scripts/movie-smoke.ps1
# Opcional: -QuitAfter 300 -Fps 60

param(
    [int] $QuitAfter = 300,
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

if (Test-Path $FramesDir) {
    Remove-Item -Recurse -Force $FramesDir
}

New-Item -ItemType Directory -Force -Path $FramesDir | Out-Null
$out = Join-Path $FramesDir "f.png"

# 4 passos × ~1 s a $Fps + folga para o ultimo aviso ficar no quadro.
$minFrames = (4 * $Fps) + 30
if ($QuitAfter -lt $minFrames) {
    Write-Host "QuitAfter=$QuitAfter baixo demais para a coreografia; usando $minFrames."
    $QuitAfter = $minFrames
}

Write-Host "Movie Maker -> $out (fps=$Fps, quit-after=$QuitAfter)"
& $GodotExe --path . --write-movie $out --fixed-fps $Fps --quit-after $QuitAfter
if ($LASTEXITCODE -ne 0) {
    throw "Godot Movie Maker saiu com codigo $LASTEXITCODE."
}

$pngs = @(Get-ChildItem -Path $FramesDir -Filter "*.png" -ErrorAction SilentlyContinue)
Write-Host "Ok. $($pngs.Count) quadro(s) em $FramesDir/"
Write-Host "Alvos: ~frame $($Fps) Fobos, $($Fps*2) Bennu, $($Fps*3) sonda, $($Fps*4) Delta-v."
