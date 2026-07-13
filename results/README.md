# Resultados experimentais

Esta pasta deve conter os arquivos gerados pela versão do código identificada na *release* final da tese.

## Estrutura preservada

- `00_config`: configuração do experimento, manifesto de arquivos e seleção de instâncias.
- `01_raw`: cópia dos arquivos brutos que precisam ser preservados.
- `02_processed`: resultados separados por instância.
- `03_consolidated`: bases consolidadas usadas nas análises.
- `04_graph_data`: dados derivados para construção de gráficos.
- `04_charts`: figuras exportadas pela aplicação e pelos scripts Python.
- `05_logs`: logs de execução, erros, advertências e auditorias.
- `06_report_tables`: tabelas finais, sínteses e checklists.

## Arquivos consolidados centrais

A implementação C# produz, entre outros:

- `todos_baselines.csv`
- `baseline_deduplication.csv`
- `todos_frm.csv`
- `todos_frm_detalhado.csv`
- `todos_monte_carlo.csv`
- `monte_carlo_replications.csv`
- `monte_carlo_confidence_intervals.csv`
- `monte_carlo_stability.csv`
- `sensibilidade.csv`
- `frm_absorption_by_gamma.csv`
- `frm_absorption_by_resource.csv`
- `todos_crashing.csv`
- `todos_crashing_candidates.csv`
- `todos_integrated.csv`
- `chapter4_weighted_results.csv`
- `dominance.csv`
- `correlacoes.csv`
- `correlacoes_rfrs_estratificadas.csv`
- `modified_dh_bb_vs_heuristics_by_instance.csv`
- `modified_dh_bb_vs_heuristics_summary.csv`
- `experiment_summary.csv`

## Regras de depósito

1. Não altere manualmente os arquivos consolidados depois da execução.
2. Preserve a configuração e as sementes que produziram os resultados.
3. Gere um arquivo de checksums SHA-256 para os resultados finais.
4. Registre no manifesto o nome, tamanho, hash, origem e função de cada arquivo.
5. Para pacotes volumosos, use preferencialmente ativos da *release* final e mantenha nesta pasta o manifesto, os checksums e os arquivos de síntese. Use Git LFS apenas quando for necessário consultar os arquivos individualmente dentro do repositório.
