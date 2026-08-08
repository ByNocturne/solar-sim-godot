# Solar Sim

Simulador do Sistema Solar em Godot 4 (.NET) com propagação kepleriana analítica, pensado
como base para trabalho futuro com missões e transferências orbitais.

## Estado atual

Pré-build. A estrutura de arquivos existe, mas os arquivos de código ainda estão vazios e
o ambiente não tem .NET SDK instalado. O trabalho começa pelo marco M0 do
[ROADMAP.md](ROADMAP.md).

## Como construir

```bash
dotnet build          # compila motor e testes
dotnet test           # roda os testes; não exige Godot instalado
```

Requer .NET SDK 8.0 ou superior (recomendado: o mais recente) e o Godot 4.x na variante
**.NET**, que é diferente do build padrão.

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
