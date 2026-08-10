# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica, pensado
como base para trabalho futuro com missões e transferências orbitais.

## Estado atual

Os marcos M0 a M25 estão concluídos e verificados em testes (`dotnet test`). O motor
carrega o Sistema Solar de `Data/solar_system_j2000.json` — Sol, oito planetas, a Lua,
Fobos e Deimos, as galileanas e Titã — e perfis ambientais de
`Data/body_environment_j2000.json`. Compõe a
posição de cada corpo a partir da do pai e produz posição e velocidade, com erro radial
abaixo de 0,02% contra as efemérides DE441 do JPL. A apresentação é tridimensional com
projeção ortográfica, escala hierárquica com modo logarítmico e linear, câmera ancorável e
orbitável e desenho de órbitas, com árvore do sistema, barra de tempo e inspetor de corpo.
O M7 acrescentou missões: trajetórias hiperbólicas, conversão de vetor de estado em
elementos, corpos criados em runtime, emenda de cônicas por esfera de influência e
save/load. A Fase 2 (M8–M14) acrescenta termodinâmica, escape atmosférico, magnetosfera,
aquecimento de maré, BHI, ciclos diurno/sazonal, biosignatures, evolução geológica
f(JD) e HUD de ensino (`I`). A Fase 3 é astrodinâmica analítica: o M15 entregou elementos
que andam com o tempo, com precessão por relatividade geral e pelo achatamento do corpo
pai — o periélio de Mercúrio avança os 43″/século conhecidos —, e o M16 acrescentou 28
corpos menores de `Data/minor_bodies_j2000.json`, com filtros por classe na árvore e um
importador offline que gera o arquivo. O M17 acrescentou o limite de Roche, o destino de
cada satélite sob a maré do pai e uma heurística de anéis. O M18 acrescentou drift
secular do semi-eixo por Yarkovsky (Bennu a −0,0019 UA/Myr) e pressão de radiação via
Poynting–Robertson. O M19 fechou a Fase 3: Lambert, janelas de Δv e impulso que emenda
arco na sonda. A Fase 4 (M20–M22) empacota o motor em `SimSession` (host sem Godot), abre
o loop de **exploração cuidadosa** (risco ambiental → carga → EVA → volta) e faz amostras
alimentarem a próxima ida (gelo → propelente). Host CLI: `dotnet run --project
Tools/ExplorationHost`. A Fase 5 (M23–M25) acrescenta o **céu da Terra**: tecla K, vista
de superfície em perspectiva com Sol/Lua/planetas por direção (esfera celeste de raio
fixo — sem `ScaleMapper`). Monorepo mantido; NuGet/split só com segundo consumidor externo.
Ideias maiores (“O simulador”, N-corpos híbrido) ficam no backlog sem fase — ver
[ROADMAP.md](ROADMAP.md).

Para ver rodando: abra o projeto no Godot e pressione F5, ou use
`scripts/Resolve-Godot.ps1` / `scripts/movie-smoke.ps1` no Windows. Espaço pausa, setas
ajustam a velocidade, R volta para J2000, Tab e Shift+Tab ancoram a câmera no corpo
seguinte e no anterior, um clique ancora no corpo apontado, L alterna entre escala
logarítmica e linear, N mostra ou esconde os nomes, Home devolve a vista inicial, H
mostra a lista de atalhos, I abre o ensino ambiental, K entra no céu da Terra, a roda dá zoom, o botão direito
gira a câmera e o do meio — ou Shift com o direito — arrasta. P e Shift+P soltam uma
sonda em órbita ou em fuga do corpo ancorado, T mostra o Δv Terra→Marte na data atual e
Shift+T (ou o botão Partida) aplica a partida colocando a sonda fora da SOI da Terra com
a velocidade de Lambert, Delete descarta a sonda ancorada, F5 salva e F9 carrega.

Os atalhos in-game saem de `Bridge/ControlCatalog.cs`; README e este parágrafo são
conferidos por `ControlCatalogTests`.

## Como construir

```bash
dotnet build          # compila motor, projeto Godot e testes
dotnet test           # roda os testes; não exige o Godot aberto
```

Cobertura local (HTML em `coverage/report/`):

```powershell
./scripts/coverage.ps1
```

Verificado com .NET SDK 10.0.302 e Godot 4.7.1 (variante .NET). O mínimo é o SDK 8.0.

Os dois comandos acima rodam também no GitHub Actions, a cada push e a cada pull request
(`.github/workflows/build.yml`). O runner não tem o Godot instalado e não precisa: o
`Godot.NET.Sdk` vem do NuGet.

Para conferir a tela sem depender de alguém olhando, o modo Movie Maker grava a cena em
uma sequência de PNG. No smoke (`scripts/movie-smoke.ps1`) a coreografia ancora Fobos
(Roche), Bennu (Yarkovsky), solta uma sonda e mostra o Δv Terra→Marte — só com
`--write-movie` / feature `movie`:

```bash
mkdir frames   # sem a pasta o Godot falha quadro a quadro e nao grava nada
dotnet build   # o Godot roda o assembly ja compilado, e nao recompila sozinho
godot --path . --write-movie frames/f.png --fixed-fps 60 --quit-after 300
```

Ou no Windows: `./scripts/movie-smoke.ps1` (resolve o Godot Mono e limpa `frames/`).

O `dotnet build` antes não é opcional: sem ele o Godot grava a versão anterior do código e
o quadro parece provar que a mudança não teve efeito.

É assim que se verifica posição de painel e corte de rótulo. `--headless --quit-after`
prova apenas que nada estourou; não mostra onde as coisas ficaram.

**Tem de ser a variante .NET, e nesta máquina ela não está no PATH.** O `godot` acima é,
no Windows do autor:

```powershell
$godot = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe"
& $godot --path . --write-movie frames/f.png --fixed-fps 60 --quit-after 300
```

O `_console.exe` é o que devolve a saída ao terminal; o outro executável da mesma pasta
abre janela e não imprime nada. Há também um Godot **sem** .NET em `Downloads`, e usá-lo
por engano custa tempo: ele não recusa o projeto, e sim falha com `No loader found for
resource: res://Bridge/SimBridge.cs`, seguido de um erro de cena que parece corrupção de
arquivo. `--version` distingue os dois — a variante certa imprime `stable.mono.official`.

Scripts de apoio no Windows: `scripts/Resolve-Godot.ps1`, `scripts/movie-smoke.ps1` e
`scripts/coverage.ps1`.
## Estrutura de projetos

Quatro projetos na solução:

- `Engine/SolarSim.Engine.csproj` — biblioteca de domínio, sem referência ao GodotSharp
- `solar-sim-godot.csproj` — projeto do Godot; exclui `Engine/**`, `Tests/**` e `Tools/**`
  dos globs e depende do motor por `ProjectReference`
- `Tests/SolarSim.Tests.csproj` — xUnit, referencia apenas o motor
- `Tools/CatalogImporter/SolarSim.CatalogImporter.csproj` — ferramenta de linha de comando,
  rodada a mão; não entra no jogo exportado
- `Tools/ExplorationHost/SolarSim.ExplorationHost.csproj` — host CLI da Fase 4 (exploração
  cuidadosa), só referencia o motor

Essa separação é o que faz o invariante 1 ser garantido pelo compilador: `using Godot` em
`Engine/` não compila, porque o assembly não está lá.

## Onde ficam as coisas

| Pasta | Responsabilidade |
| --- | --- |
| `Engine/` | Domínio puro: matemática orbital, tempo, estado, ambiente/habitabilidade, `SimSession`, exploração (Fase 4) e `LocalSky` (Fase 5). Zero Godot |
| `Bridge/` | Adaptação: precisão, escala, ponte de eventos com o Godot; `ControlCatalog` dos atalhos |
| `Render/` | Nós visuais em 3D com projeção ortográfica |
| `UI/` | Painéis e controles |
| `Data/` | Dados estáticos do Sistema Solar (órbitas + perfis ambientais + composição) na época J2000 |
| `Tests/` | Testes do motor; referencia apenas `Engine/` |
| `Tools/` | Ferramentas de quem desenvolve, fora do jogo (`CatalogImporter`, `ExplorationHost`). Tem `.gdignore` |

`Tests/` também compila os arquivos de `Bridge/` que não tocam no Godot — `ScaleMapper`,
`ScaleLayout`, `SystemProjector`, `CameraRig`, `BodyReport`, `BodyFilter` e
`DisplayFormat` — para poder testar a matemática de escala e de câmera e o que a interface
exibe. Se algum deles passar a usar o Godot, o build dos testes quebra de propósito. Pelo
mesmo arranjo compila a parte pura do importador, para que o que ele escreve seja conferido
pelo mesmo carregador que o jogo usa.

`SimBridge` é a fachada da simulação para a camada de cima: `UI/` e `Render/` não
alcançam o `SimEngine`, e sim assinam o evento de quadro ou chamam os comandos e as
consultas da ponte. Painel novo que precise de um dado do motor ganha um método ali, em
vez de uma referência ao motor.

## Os dados do sistema

`Data/solar_system_j2000.json` é a única fonte dos corpos maiores — Sol, oito planetas e
oito luas —, e acrescentar um planeta ou uma lua é editá-lo. A unidade está no nome do campo — `radiusKm`, `inclinationDeg`, `muKm3S2` —
e a carga converte graus para radianos e unidades astronômicas para quilômetros. A
validação recusa pai inexistente, ciclo na hierarquia, campo ausente e campo com nome
desconhecido, sempre dizendo qual corpo e qual campo.

Os elementos são os de J2000 e andam com o tempo por duas vias. `j2` e
`equatorialRadiusKm` descrevem o achatamento do corpo, que afeta quem o orbita e não ele
mesmo; junto com a relatividade geral, o motor deriva daí a precessão sozinho. `orbit.rates`
existe para o que o motor **não** modela, em graus por século — declarar ali um efeito já
calculado é contá-lo duas vezes.

Todo corpo declara `kind`, que é a classe **dinâmica**: onde ele vive e como se move, e não
o rótulo que a IAU lhe deu. É o que um filtro de vista precisa saber — "planeta anão"
cortaria a lista em diagonal, porque Ceres é do cinturão principal e Plutão é
transnetuniano. O rótulo físico, quando importa, é texto livre em `family`.

## O catálogo de corpos menores

`Data/minor_bodies_j2000.json` traz 28 asteroides, cometas, troianos, centauros e
transnetunianos, todos filhos do Sol. **É gerado, e não se edita à mão:** a fonte é
`Tools/CatalogImporter/source/minor_bodies_j2000.csv`, e regerar é

```bash
dotnet run --project Tools/CatalogImporter
```

Há teste conferindo que o JSON versionado é exatamente o que o CSV versionado produz, para
que uma edição direta no gerado não passe despercebida.

O importador roda a mão e **não acessa a rede** — nem ele nem, muito menos, o jogo. Em
runtime o simulador só lê arquivo: atualizar os dados é baixar as tabelas do JPL a mão,
reescrever o CSV e rodar o comando acima. A conversão que justifica a ferramenta é a de
época: as fontes publicam elementos osculadores na época que lhes convém, e aqui a anomalia
média é trazida para J2000.0 pelo movimento médio, uma vez, em vez de a cada abertura do
jogo. Antes de gravar, o próprio `DataLoader` valida o resultado.

Corpo menor aceita `muKm3S2` **ou** `densityGCm3`, nunca os dois: quase nenhum teve a massa
medida, e do diâmetro mais uma densidade típica da classe o motor deriva o GM da esfera
equivalente. É estimativa, e serve à esfera de influência, não à balança.

Esses corpos ficam fora do dimensionamento da escala de propósito — o afélio de Sedna passa
de mil unidades astronômicas, e incluí-lo encolheria o Sistema Solar inteiro para acomodar
um ponto quase sempre invisível. Continuam sendo desenhados, apenas além do raio nominal do
nível. A consequência: acrescentar corpos ao catálogo não move um pixel do resto.

## Maré, limite de Roche e anéis

`SimEngine.TidesOn` responde o que a maré do corpo pai faz com um satélite, e
`SimEngine.RingZoneOf` responde onde esse corpo poderia hospedar um anel. São duas
perguntas opostas sobre o mesmo cálculo: a primeira trata o corpo como quem sofre a maré,
a segunda como quem a impõe.

São dois limites de Roche, e por isso três destinos. Entre o rígido e o fluido está a
faixa em que a resposta depende de do que o corpo é feito, e é onde Fobos está — ele
orbita entre os dois limites de Marte, e é o corpo do arquivo que existe para provar que o
cálculo funciona. Deimos é o controle ao lado. A comparação é sempre com o **periápside**,
porque o corpo se parte no mergulho.

A heurística de anéis não é geométrica. Pela largura da zona a Terra ganharia de Saturno,
já que a Terra é densa; o que falta à Terra é gelo, não espaço. As condições são haver
faixa acima da superfície, estar além da linha de gelo (2,7 UA, pelo semi-eixo maior) e
orbitar a estrela. A última é limite de escopo declarado: a vizinhança de uma lua é
governada pela maré do planeta, e um modelo de dois corpos não tem o que dizer sobre ela.

## Yarkovsky e pressão de radiação

O drift do semi-eixo por Yarkovsky não é inventado pelo motor: quem teve o efeito medido
declara `nonGravitational.yarkovskyDaAuPerMyr` no JSON (no catálogo, a coluna do CSV).
Bennu traz −0,0019 UA/Myr. O motor converte para taxa secular e soma ao mesmo caminho que
a relatividade e o J₂ já usam — `f(JD)`, sem força por quadro. Planetas e o resto do
catálogo, sem o bloco, não andam o semi-eixo.

Pressão de radiação é o coeficiente β opcional no mesmo bloco; vira drift
Poynting–Robertson de `a` e `e`. Para asteróides típicos é desprezível diante do
Yarkovsky.

## Transferências Lambert e impulso

`TransferPlanner.Preview` / `ScanWindows` e a fachada `SimBridge.PreviewTransfer` /
`ScanTransferWindows` só consultam: duas posições, um tempo de voo, Δv de partida e de
chegada. `ApplyTransferDeparture` coloca a sonda **fora da SOI** do originário com a
velocidade de Lambert (não só muda Δv no lugar errado) e emenda o arco. T / botão Δv
consultam; Shift+T / Partida aplicam se a sonda ancorada orbitar o Sol.

Scripts de apoio no Windows: `scripts/Resolve-Godot.ps1` e `scripts/movie-smoke.ps1`.

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

- [ROADMAP.md](ROADMAP.md) — plano em marcos, com critérios de pronto verificáveis
- `.cursor/rules/` — convenções e armadilhas numéricas conhecidas
