---
name: solar-sim-verify
description: >-
  Verifica o Solar Sim após mudanças: dotnet build/test e, quando a UI mudou,
  Movie Maker via scripts. Use ao validar marcos, correções de painel, Roche,
  Yarkovsky, transferências ou antes de declarar pronto.
---

# Solar Sim — verificação

## Sempre

```powershell
dotnet build
dotnet test
```

Os testes cobrem o motor e a Bridge pura sem Godot.

## Cobertura (opcional, local)

```powershell
./scripts/coverage.ps1
```

HTML em `coverage/report/`. O CI não publica cobertura; isto é para achar buracos
Bridge/UI-adjacent.

## Quando UI / Render / layout mudou

1. `dotnet build` (obrigatório antes do Godot — ele não recompila sozinho).
2. `./scripts/movie-smoke.ps1` (resolve Godot Mono; coreografia Fobos → Bennu → sonda → Δv).
3. Conferir frames em `frames/` (~1s Fobos Roche, ~2s Bennu drift, ~3s rótulo da sonda, ~4s aviso Δv).

## Quando atalhos / Help mudaram

Editar só `Bridge/ControlCatalog.cs`. Depois:

```powershell
dotnet test --filter ControlCatalog
```

O hook `.cursor/hooks/sync-controls.ps1` lembra de sincronizar README/AGENTS.

## Quando catálogo mudou

Editar só o CSV; regenerar:

```powershell
dotnet run --project Tools/CatalogImporter
dotnet test --filter CatalogImporter
```

## Não fazer

- Usar Godot sem `.mono` (falha enganosa em `SimBridge.cs`).
- Declarar UI pronta só com `--headless --quit-after`.
- Expor `SimEngine` a partir de `UI/`.
- Editar HelpLines em `TimeControls` — a fonte é `ControlCatalog`.
