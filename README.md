# Solar Sim

Simulador do Sistema Solar em Godot 4 com C#, usando a solução analítica de Kepler para o
problema de dois corpos. A posição de qualquer corpo em qualquer instante é calculada
diretamente a partir da data, sem integração numérica de forças.

O projeto é desenhado como base para trabalho futuro com missões e transferências
orbitais, o que influencia decisões desde o início: unidades da astrodinâmica, vetores de
estado como tipo de primeira classe e suporte planejado a órbitas abertas.

## Estado

**Marcos M0 a M5 concluídos.** O simulador carrega
o Sistema Solar de um arquivo JSON — Sol, oito planetas, a Lua, as galileanas e Titã —
propaga cada corpo em torno do seu e desenha as órbitas, com a câmera ancorável em
qualquer corpo. O motor está validado contra as efemérides DE441 do JPL Horizons: erro
máximo de 0,0132% na distância radial ao longo de 26 anos simulados. A interface tem
árvore do sistema, barra de tempo com salto para uma data arbitrária e inspetor com os
elementos orbitais do corpo ancorado. O próximo passo é o M6, a decisão entre 2D e 3D. O
plano está em [ROADMAP.md](ROADMAP.md), dividido em oito marcos.

Controles: espaço pausa, setas ajustam a velocidade do tempo, R volta para J2000, Tab e
Shift+Tab ancoram a câmera no corpo seguinte e no anterior, um clique ancora no corpo
apontado, L alterna entre escala logarítmica e linear, N mostra ou esconde os nomes, Home
devolve a vista inicial, H mostra a lista de atalhos, a roda dá zoom e o botão direito
arrasta.

## Setup

1. Instalar o [.NET SDK](https://dotnet.microsoft.com/download) — versão 8.0 ou superior,
   64 bits. A recomendação oficial do Godot é usar sempre o SDK estável mais recente.
2. Instalar o [Godot 4.x](https://godotengine.org/download) na variante **.NET**. O build
   padrão não roda C#.
3. Verificar com `dotnet --info`.

```bash
dotnet build
dotnet test     # os testes do motor rodam sem o Godot
```

Verificado com .NET SDK 10.0.302 e Godot 4.7.1. Os mesmos dois comandos rodam no GitHub
Actions a cada push.

## Arquitetura

Três camadas, com dependências apontando apenas para dentro:

```
UI/ e Render/   ->  assinam eventos, tratam input, desenham
Bridge/         ->  converte precisão e escala; único ponto de contato com o motor
Engine/         ->  matemática orbital e estado; não conhece o Godot
```

`Engine/` é domínio puro: compila e é testado com o Godot desinstalado. Essa separação não
é cerimônia — é o que permite validar a mecânica orbital contra efemérides reais sem abrir
o editor, e o que mantém o motor reaproveitável se a camada visual mudar.

`Bridge/SimBridge.cs` é o único nó do Godot que alcança o motor. Os painéis e os nós
gráficos assinam o evento de snapshot ou chamam comandos e consultas dessa fachada, de
modo que o número de pontos de contato entre os dois mundos é um. O que o inspetor mostra
é um valor consultado ao motor e descartado logo depois, e não uma cópia mantida pela
tela: assim não existe onde um número desatualizado possa sobreviver.

## Precisão

Distâncias no Sistema Solar chegam a 4,5 bilhões de quilômetros. Em ponto flutuante de
precisão simples, isso trunca a mantissa e produz trepidação visual em corpos distantes da
origem.

O motor opera inteiramente em `double`. A conversão para `float` acontece em um único
lugar, na camada Bridge, e somente após subtrair a posição da câmera — quando os números
já são pequenos.

## Escalas

O Sistema Solar em proporção real é quase todo vazio: Netuno está 78 vezes mais longe do
Sol que Mercúrio, então qualquer escala linear que caiba na tela empilha os planetas
internos em um punhado de pixels. O modo logarítmico comprime o exterior por uma curva
perceptual e devolve os planetas internos ao mapa; `L` alterna entre os dois, com
transição suave.

A escala é hierárquica: cada corpo tem o seu próprio mapa para os filhos, dimensionado
pela maior órbita que abriga. Sem isso, a órbita da Lua — 390 vezes menor que a da Terra —
sumiria dentro do disco do planeta. O raio desenhado dos corpos tem escala própria, sem
relação com a das distâncias, porque em proporção real a Terra teria centésimos de pixel.

Quanto espaço cada nível recebe é fração da altura da janela, e não uma contagem fixa de
pixels: assim a mesma calibragem serve para qualquer resolução, em vez de deixar o sistema
encolhido no meio de uma tela grande.

## Unidades

Quilômetros, segundos e radianos internamente, com o parâmetro gravitacional em km³/s²,
que é a unidade em que o JPL publica valores de GM. O tempo de calendário é a Data
Juliana, com época J2000.0 em 2451545.0.

Graus e unidades astronômicas existem em um lugar só: `Data/solar_system_j2000.json`, onde
a unidade está declarada no nome de cada campo e a conversão acontece na carga. Acrescentar
um corpo é editar esse arquivo.

## Documentos

- [ROADMAP.md](ROADMAP.md) — os oito marcos e seus critérios de pronto
- [AGENTS.md](AGENTS.md) — orientação rápida para agentes de código
- `.cursor/rules/` — convenções detalhadas e armadilhas numéricas conhecidas
