# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica.
Pensado como base para missões e transferências orbitais.

O detalhe operacional — controles, dados, invariantes, como construir e gravar
quadros — está em [AGENTS.md](AGENTS.md). O plano por marcos está em
[ROADMAP.md](ROADMAP.md).

## Estado

**M0–M25 + J1.** Simulador orbital (Main) e host do jogo (Game — céu da Terra). F5 abre o
jogo via `Boot.tscn`; feature `movie` abre o sim. **S** no jogo troca para o simulador.
Motor com taxas seculares, Lambert, exploração domínio e `LocalSky`. Erro radial dos
planetas abaixo de 0,02% contra DE441.

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
