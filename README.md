# RCPSP–FRM Thesis Computational Artifacts

Repositório dos artefatos computacionais associados à tese **“Gestão de projetos sob incerteza: a influência da flexibilidade dos recursos no cronograma”**.

O repositório separa a implementação computacional dos resultados gerados. Essa organização permite inspecionar o código sem misturá-lo com bases experimentais volumosas e preserva a rastreabilidade entre o modelo da tese, o protocolo experimental e os arquivos analisados.

## Estrutura

```text
.
├── code/
│   ├── csharp/          Aplicação C#, add-in VSTO, módulo experimental e versão standalone
│   └── python/          Scripts finais de pós-processamento e geração de figuras
├── results/
│   ├── 00_config/       Configurações, manifesto e seleção de instâncias
│   ├── 01_raw/          Arquivos brutos preservados
│   ├── 02_processed/    Resultados por instância
│   ├── 03_consolidated/ Resultados consolidados
│   ├── 04_graph_data/   Dados preparados para gráficos
│   ├── 04_charts/       Figuras exportadas
│   ├── 05_logs/         Registros de execução e auditoria
│   └── 06_report_tables/ Tabelas finais e checklists
└── documentation/       Rastreabilidade, reprodução, auditoria e publicação no GitHub
```

## Componentes principais

- **Geração de baselines RCPSP** por regras de prioridade, esquemas seriais e paralelos e direções de escalonamento.
- **Referência exata** por Modified DH Branch-and-Bound, distinguindo ótimo comprovado, incumbente e falha.
- **Diagnóstico FRM** com folga estrutural, limites de duração, *score*, *balance* e SIF.
- **Simulação de Monte Carlo** com métricas de atraso, P95, VaR95, CVaR95, CVaR95 relativo e diagnósticos de absorção.
- **Análise de sensibilidade** por níveis de intensidade da perturbação.
- **Compressão temporal** com recálculo integral do cronograma, do FRM e do risco.
- **Deduplicação estrutural**, ponderação por instância, arquivos consolidados e auditorias.

## Requisitos para o código C#

- Windows 10 ou Windows 11.
- Visual Studio 2022.
- .NET Framework 4.8 Developer Pack.
- Ferramentas de desenvolvimento do Office/VSTO.
- Microsoft Project Desktop para executar o add-in.

A solução principal está em:

```text
code/csharp/RCPSP-FRM.sln
```

A aplicação standalone permite abrir e avaliar arquivos `.rcp` sem iniciar o Microsoft Project. O projeto `Main` contém o add-in VSTO.

## Scripts Python

A pasta `code/python` deve receber apenas as versões finais dos scripts efetivamente usados para produzir as tabelas e figuras da tese. Cada script deve declarar entradas, saídas, dependências e exemplo de execução em seu cabeçalho ou em um README próprio.

## Resultados

Os resultados não são misturados ao código. A pasta `results` reproduz a estrutura criada pelo módulo experimental. Os arquivos leves podem permanecer versionados na pasta `results`. Os pacotes volumosos devem, preferencialmente, ser anexados à *release* final, com manifesto e SHA-256; Git LFS fica reservado aos casos em que o acesso arquivo a arquivo dentro do repositório seja realmente necessário. Consulte `documentation/GITHUB_PUBLICATION_GUIDE.md`.

## Rastreabilidade científica

A correspondência entre algoritmos, seções metodológicas, classes, métodos e arquivos de saída está documentada em:

```text
documentation/THESIS_TRACEABILITY.md
```

A auditoria estática, as limitações do ambiente de validação e o relatório legível por máquina estão em `documentation/CODE_AUDIT.md` e `documentation/VALIDATION_REPORT.json`.

## Versão citável

A tese deve citar uma *release* imutável e o respectivo *commit*, e não apenas a ramificação `main`. A versão prevista para o depósito final é `v1.0.0`.

## Licença

O código está disponibilizado para inspeção, validação e reprodução acadêmica nos termos do arquivo `LICENSE`.
