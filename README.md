# Solar Sim

Simulador do Sistema Solar em Godot 4 com C#, usando a solução analítica de Kepler para o
problema de dois corpos. A posição de qualquer corpo em qualquer instante é calculada
diretamente a partir da data, sem integração numérica de forças.

O projeto é desenhado como base para trabalho futuro com missões e transferências
orbitais, o que influencia decisões desde o início: unidades da astrodinâmica, vetores de
estado como tipo de primeira classe e suporte planejado a órbitas abertas.

## Estado

**Marcos M0 e M1 concluídos.** A fatia vertical atravessa todas as camadas: a Terra orbita
o Sol a partir dos seus elementos keplerianos reais na época J2000, com o tempo controlável
em tela. O próximo passo é o M2, que valida a precisão contra efemérides do JPL. O plano
completo está em [ROADMAP.md](ROADMAP.md), dividido em oito marcos.

Controles: espaço pausa, setas ajustam a velocidade do tempo, R volta para J2000, roda do
mouse dá zoom.

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

Verificado com .NET SDK 10.0.302 e Godot 4.7.1.

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

## Precisão

Distâncias no Sistema Solar chegam a 4,5 bilhões de quilômetros. Em ponto flutuante de
precisão simples, isso trunca a mantissa e produz trepidação visual em corpos distantes da
origem.

O motor opera inteiramente em `double`. A conversão para `float` acontece em um único
lugar, na camada Bridge, e somente após subtrair a posição da câmera — quando os números
já são pequenos.

## Unidades

Quilômetros, segundos e radianos internamente, com o parâmetro gravitacional em km³/s²,
que é a unidade em que o JPL publica valores de GM. O tempo de calendário é a Data
Juliana, com época J2000.0 em 2451545.0.

## Documentos

- [ROADMAP.md](ROADMAP.md) — os oito marcos e seus critérios de pronto
- [AGENTS.md](AGENTS.md) — orientação rápida para agentes de código
- `.cursor/rules/` — convenções detalhadas e armadilhas numéricas conhecidas
