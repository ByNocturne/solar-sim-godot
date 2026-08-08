# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica, pensado
como base para trabalho futuro com missões e transferências orbitais.

## Estado atual

Marcos M0, M1 e M2 concluídos. O motor propaga Sol, Terra e Marte com erro radial abaixo
de 0,02% contra as efemérides DE441 do JPL, e produz posição e velocidade. O próximo passo
é o M3, que traz o sistema completo por JSON e a hierarquia de luas, descrito no
[ROADMAP.md](ROADMAP.md).

Para ver rodando: abra o projeto no Godot e pressione F5, ou
`godot --path . --resolution 1152x648`. Espaço pausa, setas ajustam a velocidade, R volta
para J2000, roda do mouse dá zoom.

## Como construir

```bash
dotnet build          # compila motor, projeto Godot e testes
dotnet test           # roda os testes; não exige o Godot aberto
```

Verificado com .NET SDK 10.0.302 e Godot 4.7.1 (variante .NET). O mínimo é o SDK 8.0.

## Estrutura de projetos

Três projetos na solução:

- `Engine/SolarSim.Engine.csproj` — biblioteca de domínio, sem referência ao GodotSharp
- `solar-sim-godot.csproj` — projeto do Godot; exclui `Engine/**` e `Tests/**` dos globs
  e depende do motor por `ProjectReference`
- `Tests/SolarSim.Tests.csproj` — xUnit, referencia apenas o motor

Essa separação é o que faz o invariante 1 ser garantido pelo compilador: `using Godot` em
`Engine/` não compila, porque o assembly não está lá.

## Onde ficam as coisas

| Pasta | Responsabilidade |
| --- | --- |
| `Engine/` | Domínio puro: matemática orbital, tempo, estado. Zero Godot |
| `Bridge/` | Adaptação: precisão, escala, ponte de eventos com o Godot |
| `Render/` | Nós visuais. Andaime 2D descartável até o marco M6 |
| `UI/` | Painéis e controles |
| `Data/` | Dados estáticos do Sistema Solar na época J2000 |
| `Tests/` | Testes do motor; referencia apenas `Engine/` |

## Os quatro invariantes

1. Nenhum arquivo em `Engine/` contém `using Godot`
2. `double` no domínio; conversão para `float` só na Bridge, e só após subtrair a câmera
3. Unidades internas: km, segundos, radianos; mu em km³/s²
4. O estado é função pura da Data Juliana, nunca integração acumulativa

As regras detalhadas, com exemplos de código, estão em `.cursor/rules/`.

## Documentos

- [ROADMAP.md](ROADMAP.md) — plano em oito marcos, com critérios de pronto verificáveis
- `.cursor/rules/` — convenções e armadilhas numéricas conhecidas
