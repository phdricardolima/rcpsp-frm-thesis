# Estrutura dos resultados

A estrutura abaixo reproduz os diretórios gerados pelo módulo experimental.

```text
results/
├── 00_config/
│   ├── experiment_config.json
│   ├── manifesto_files.csv
│   └── instance_selection_rfrs.csv
├── 01_raw/
├── 02_processed/
│   └── <instance_id>/
│       ├── 00_instance_validation.csv
│       ├── 01_instancia.csv
│       ├── 02_baselines.csv
│       ├── 02_baseline_validation.csv
│       ├── 02_baseline_deduplication.csv
│       ├── 03_frm.csv
│       ├── 03_frm_detalhado.csv
│       ├── 04_monte_carlo.csv
│       ├── 05_crashing.csv
│       ├── 05_crashing_candidates.csv
│       ├── 05_crashing_detalhes_activities.csv
│       ├── 06_integrated.csv
│       ├── 07_absorption_by_gamma.csv
│       └── 07_absorption_by_resource.csv
├── 03_consolidated/
├── 04_graph_data/
├── 04_charts/
├── 05_logs/
└── 06_report_tables/
```

## Regras para publicação

- Preserve os nomes produzidos pelo código.
- Não renomeie colunas depois do experimento.
- Não misture resultados de execuções diferentes na mesma pasta.
- Para cada execução, mantenha um identificador único e uma configuração correspondente.
- A versão final da tese deve apontar para um pacote congelado, não para uma pasta em atualização.
- Arquivos derivados por Python devem indicar claramente o consolidado de origem.
