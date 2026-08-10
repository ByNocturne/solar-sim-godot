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

## Quando UI / Render / layout mudou

1. `dotnet build` (obrigatório antes do Godot — ele não recompila sozinho).
2. `./scripts/movie-smoke.ps1` (resolve Godot Mono via `Resolve-Godot.ps1`).
3. Conferir frames em `frames/` para corte de painel, rótulos e linhas do inspetor.

Alvos úteis de âncora (ajustar cena/tempo se o smoke for só a vista inicial):

- Fobos — maré "em risco"
- Bennu — drift do semi-eixo
- Sonda após P — nome na tela

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
