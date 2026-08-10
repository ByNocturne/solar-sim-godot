# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica.
Pensado como base para missões e transferências orbitais.

O detalhe operacional — controles, dados, invariantes, como construir e gravar
quadros — está em [AGENTS.md](AGENTS.md). O plano por marcos está em
[ROADMAP.md](ROADMAP.md).

## Estado

**M0 a M25 estão concluídos** (até a Fase 5 — céu da Terra). O motor carrega o Sistema Solar e o
catálogo de corpos menores, propaga com taxas seculares (J₂ + GR), Yarkovsky onde
declarado, e oferece Lambert + impulso em sondas. Erro radial dos planetas abaixo de
0,02% contra DE441. Apresentação 3D ortográfica, com árvore, barra de tempo, inspetor e
HUD de ensino (`I`). A Fase 4 empacota o motor (`SimSession`) e o loop de exploração
cuidadosa; host CLI: `dotnet run --project Tools/ExplorationHost`. A Fase 5: tecla **K**
para o céu local na Terra (perspectiva, sem misturar com a escala orbital).

## Setup

1. [.NET SDK](https://dotnet.microsoft.com/download) 8.0+ (recomendado: o estável mais recente).
2. [Godot 4.x](https://godotengine.org/download) na variante **.NET** (`stable.mono`). O
   build sem .NET falha com `No loader found for … SimBridge.cs` — parece corrupção de
   cena, e não é.
3. No Windows, o executável certo costuma não estar no PATH. Use
   [`scripts/Resolve-Godot.ps1`](scripts/Resolve-Godot.ps1) ou defina `$env:GODOT`.

```bash
dotnet build
dotnet test     # motor e Bridge pura; Godot fechado basta
```

Smoke visual (Movie Maker):

```powershell
./scripts/movie-smoke.ps1
```

## Controles (resumo)

Espaço pausa · setas velocidade · R J2000 · Tab âncora · L escala · N nomes · Home vista · H ajuda · I ensino · P / Shift+P sonda · T / Shift+T Terra→Marte · Delete descarta · F5 / F9 save/load · K céu da Terra · direito orbita · meio / Shift+direito arrasta · roda zoom.

## Arquitetura

```
UI/ e Render/  ->  assinam eventos e tratam input
Bridge/        ->  precisão, escala, fachada SimBridge
Engine/        ->  domínio puro; zero Godot
```

Unidades internas: km, segundos, radianos. Estado = f(JD) (+ arcos das sondas).
