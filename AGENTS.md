# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica, pensado
como base para trabalho futuro com missões e transferências orbitais.

## Estado atual

Os marcos M0 a M14 estão concluídos e verificados em testes (`dotnet test`). O motor
carrega o Sistema Solar de `Data/solar_system_j2000.json` — Sol, oito planetas, a Lua, as
galileanas e Titã — e perfis ambientais de `Data/body_environment_j2000.json`. Compõe a
posição de cada corpo a partir da do pai e produz posição e velocidade, com erro radial
abaixo de 0,02% contra as efemérides DE441 do JPL. A apresentação é tridimensional com
projeção ortográfica, escala hierárquica com modo logarítmico e linear, câmera ancorável e
orbitável e desenho de órbitas, com árvore do sistema, barra de tempo e inspetor de corpo.
O M7 acrescentou missões: trajetórias hiperbólicas, conversão de vetor de estado em
elementos, corpos criados em runtime, emenda de cônicas por esfera de influência e
save/load. A Fase 2 (M8–M14) acrescenta termodinâmica, escape atmosférico, magnetosfera,
aquecimento de maré, BHI, ciclos diurno/sazonal, biosignatures, evolução geológica
f(JD) e HUD de ensino (`I`). A Fase 3 é astrodinâmica analítica: o M15 já entregou
elementos que andam com o tempo, com precessão por relatividade geral e pelo achatamento
do corpo pai — o periélio de Mercúrio avança os 43″/século conhecidos. Faltam M16 a M19:
catálogo curado de corpos menores com importador offline, Roche/anéis, Yarkovsky e
Lambert/Δv. Ideias maiores (“O simulador”, N-corpos híbrido) ficam no backlog sem fase —
ver [ROADMAP.md](ROADMAP.md).

Para ver rodando: abra o projeto no Godot e pressione F5, ou `godot --path .`. Espaço pausa, setas ajustam a velocidade, R volta
para J2000, Tab e Shift+Tab ancoram a câmera no corpo seguinte e no anterior, um clique
ancora no corpo apontado, L alterna entre escala logarítmica e linear, N mostra ou esconde
os nomes, Home devolve a vista inicial, H mostra a lista de atalhos, I abre o ensino
ambiental, a roda dá zoom, o botão direito gira a câmera e o do meio — ou Shift com o
direito — arrasta. P e Shift+P soltam uma sonda em órbita ou em fuga do corpo ancorado,
Delete descarta a sonda ancorada, F5 salva e F9 carrega.

## Como construir

```bash
dotnet build          # compila motor, projeto Godot e testes
dotnet test           # roda os testes; não exige o Godot aberto
```

Verificado com .NET SDK 10.0.302 e Godot 4.7.1 (variante .NET). O mínimo é o SDK 8.0.

Os dois comandos acima rodam também no GitHub Actions, a cada push e a cada pull request
(`.github/workflows/build.yml`). O runner não tem o Godot instalado e não precisa: o
`Godot.NET.Sdk` vem do NuGet.

Para conferir a tela sem depender de alguém olhando, o modo Movie Maker grava a cena em
uma sequência de PNG, no tamanho declarado em `project.godot`:

```bash
mkdir frames   # sem a pasta o Godot falha quadro a quadro e nao grava nada
dotnet build   # o Godot roda o assembly ja compilado, e nao recompila sozinho
godot --path . --write-movie frames/f.png --fixed-fps 60 --quit-after 70
```

O `dotnet build` antes não é opcional: sem ele o Godot grava a versão anterior do código e
o quadro parece provar que a mudança não teve efeito.

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
| `Engine/` | Domínio puro: matemática orbital, tempo, estado, ambiente/habitabilidade. Zero Godot |
| `Bridge/` | Adaptação: precisão, escala, ponte de eventos com o Godot |
| `Render/` | Nós visuais em 3D com projeção ortográfica |
| `UI/` | Painéis e controles |
| `Data/` | Dados estáticos do Sistema Solar (órbitas + perfis ambientais) na época J2000 |
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

Os elementos são os de J2000 e andam com o tempo por duas vias. `j2` e
`equatorialRadiusKm` descrevem o achatamento do corpo, que afeta quem o orbita e não ele
mesmo; junto com a relatividade geral, o motor deriva daí a precessão sozinho. `orbit.rates`
existe para o que o motor **não** modela, em graus por século — declarar ali um efeito já
calculado é contá-lo duas vezes.

## Corpos que não vêm do arquivo

Uma sonda entra por `SimEngine.AddFromState`, a partir de posição e velocidade relativas
ao pai; a órbita não é escolhida, e sim consequência do estado. Um corpo assim carrega uma
`Trajectory` — a lista de arcos por onde passou — em vez de uma órbita só, e é o único
tipo de corpo que a emenda de cônicas reatribui a outro pai ao atravessar uma esfera de
influência. `SaveState` grava a Data Juliana e esses corpos, e mais nada: o estado do que
veio do JSON é função da data.

Trajetórias hiperbólicas são suportadas, com o semi-eixo negativo; a parábola exata é
recusada com mensagem explícita, tanto na carga quanto na conversão inversa.

## Os quatro invariantes

1. Nenhum arquivo em `Engine/` contém `using Godot`
2. `double` no domínio; conversão para `float` só na Bridge, e só após subtrair a câmera
3. Unidades internas: km, segundos, radianos; mu em km³/s²
4. O estado é função pura da Data Juliana, nunca integração acumulativa

As regras detalhadas, com exemplos de código, estão em `.cursor/rules/`.

## Documentos

- [ROADMAP.md](ROADMAP.md) — plano em oito marcos, com critérios de pronto verificáveis
- `.cursor/rules/` — convenções e armadilhas numéricas conhecidas
