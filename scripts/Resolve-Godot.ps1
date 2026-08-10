# Resolve o executavel .NET (Mono) do Godot 4 nesta maquina.
# Preferencia: $env:GODOT, depois WinGet Packages, depois PATH.
# Uso: . ./scripts/Resolve-Godot.ps1; & $GodotExe --version

function Resolve-Godot {
    if ($env:GODOT -and (Test-Path -LiteralPath $env:GODOT)) {
        return (Resolve-Path -LiteralPath $env:GODOT).Path
    }

    $wingetRoot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path -LiteralPath $wingetRoot) {
        $found = Get-ChildItem -Path $wingetRoot -Recurse -Filter "Godot_*_mono_win64_console.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($found) {
            return $found.FullName
        }
    }

    $fromPath = Get-Command godot -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw @"
Godot .NET (console) nao encontrado.
Defina `$env:GODOT` para o caminho do Godot_*_mono_win64_console.exe
ou instale GodotEngine.GodotEngine.Mono via WinGet.
"@
}

$GodotExe = Resolve-Godot
Write-Host "Godot: $GodotExe"
& $GodotExe --version
