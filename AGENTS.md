# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica, pensado
como base para trabalho futuro com missões e transferências orbitais.

## Estado atual

Marcos M0 a M5 concluídos e verificados em execução. O motor carrega
o Sistema Solar de `Data/solar_system_j2000.json` — Sol, oito planetas, a Lua, as
galileanas e Titã — compõe a posição de cada corpo a partir da do pai e produz posição e
velocidade, com erro radial abaixo de 0,02% contra as efemérides DE441 do JPL. A
apresentação tem escala hierárquica com modo logarítmico e linear, câmera ancorável e
desenho de órbitas, com árvore do sistema, barra de tempo e inspetor de corpo. O próximo
passo é o M6, a decisão entre 2D e 3D, descrito no [ROADMAP.md](ROADMAP.md).

Para ver rodando: abra o projeto no Godot e pressione F5, ou
`godot --path . --resolution 1152x648`. Espaço pausa, setas ajustam a velocidade, R volta
para J2000, Tab e Shift+Tab ancoram a câmera no corpo seguinte e no anterior, um clique
ancora no corpo apontado, L alterna entre escala logarítmica e linear, N mostra ou esconde
os nomes, Home devolve a vista inicial, H mostra a lista de atalhos, a roda dá zoom e o
botão direito arrasta.

## Como construir

```bash
dotnet build          # compila motor, projeto Godot e testes
dotnet test           # roda os testes; não exige o Godot aberto
```

Verificado com .NET SDK 10.0.302 e Godot 4.7.1 (variante .NET). O mínimo é o SDK 8.0.

Para conferir a tela sem depender de alguém olhando, o modo Movie Maker grava a cena em
uma sequência de PNG, no tamanho declarado em `project.godot`:

```bash
godot --path . --write-movie frames/f.png --fixed-fps 60 --quit-after 70
```

É assim que se verifica posição de painel e corte de rótulo. `--headless --quit-after`
prova apenas que nada estourou; não mostra onde as coisas ficaram.

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

`Tests/` também compila os arquivos de `Bridge/` que não tocam no Godot — `ScaleMapper`,
`ScaleLayout`, `SystemProjector`, `CameraRig`, `BodyReport` e `DisplayFormat` — para poder
testar a matemática de escala e de câmera e o que a interface exibe. Se algum deles passar
a usar o Godot, o build dos testes quebra de propósito.

`SimBridge` é a fachada da simulação para a camada de cima: `UI/` e `Render/` não
alcançam o `SimEngine`, e sim assinam o evento de quadro ou chamam os comandos e as
consultas da ponte. Painel novo que precise de um dado do motor ganha um método ali, em
vez de uma referência ao motor.

## Os dados do sistema

`Data/solar_system_j2000.json` é a única fonte dos corpos, e acrescentar um planeta ou uma
lua é editá-lo. A unidade está no nome do campo — `radiusKm`, `inclinationDeg`, `muKm3S2` —
e a carga converte graus para radianos e unidades astronômicas para quilômetros. A
validação recusa pai inexistente, ciclo na hierarquia, campo ausente e campo com nome
desconhecido, sempre dizendo qual corpo e qual campo.

## Os quatro invariantes

1. Nenhum arquivo em `Engine/` contém `using Godot`
2. `double` no domínio; conversão para `float` só na Bridge, e só após subtrair a câmera
3. Unidades internas: km, segundos, radianos; mu em km³/s²
4. O estado é função pura da Data Juliana, nunca integração acumulativa

As regras detalhadas, com exemplos de código, estão em `.cursor/rules/`.

## Documentos

- [ROADMAP.md](ROADMAP.md) — plano em oito marcos, com critérios de pronto verificáveis
- `.cursor/rules/` — convenções e armadilhas numéricas conhecidas
