# Rastreabilidade entre a tese e o código

Este documento liga o fluxo científico descrito nos capítulos metodológicos e no Apêndice A às classes e aos métodos da implementação. A interface gráfica não define o método científico. Ela apenas configura e aciona os componentes listados abaixo.

## Matriz principal

| Elemento da tese | Função | Implementação principal | Métodos ou pontos de entrada | Saídas relacionadas |
|---|---|---|---|---|
| Algoritmo A.1 | Orquestrar o pipeline integrado | `RCPSP.Application/PipelineRunner.cs` e `RCPSP.WinForms/ExperimentalTese/BatchExperimentalStudyRunner.cs` | `PipelineRunner.Run`, `BatchExperimentalStudyRunner.Run` | conjunto completo de pastas experimentais |
| Algoritmo A.2 | Gerar baselines candidatos | `CpuBaselineBatchScheduler.cs`, `CpuBaselineScheduler.cs`, `RCPSP.Scheduling/Heuristics`, `RCPSP.Scheduling/Schemes` | `Run`, `RunAll`, `ExecuteHeuristicRun`, `BuildBaselineResult` | `todos_baselines.csv` |
| Referência exata | Produzir ótimo comprovado ou incumbente | `CpuExactBaselineScheduler.cs` e `DhBranchAndBoundSolver.cs` | `RunModifiedDhBranchAndBoundDetailed`, `RunBranchAndBoundDetailed` | campos de classificação exata e arquivos `modified_dh_bb_*` |
| Algoritmo A.3 | Deduplicar baselines estruturalmente equivalentes | `BatchExperimentalStudyRunner.cs` | `DeduplicateBaselineRuns`, `BuildScheduleSignature`, `ComputeStringSha256`, `BuildBaselineId` | `baseline_deduplication.csv` |
| Algoritmo A.4 | Calcular folga estrutural, limites, score, balance e SIF | `CpuFrmCalculator.cs` | `Run`, `BuildDemandByActivity`, `BuildBaselineUsageProfile`, `ComputeSifByResourceId`, `BuildDiagnostics` | `todos_frm.csv`, `todos_frm_detalhado.csv` |
| Algoritmo A.5 | Executar Monte Carlo e calcular risco de atraso | `CpuRiskAnalyzer.cs` e `RiskScenarioScheduler.cs` | `Run`, `RunSingle`, `BuildFrmWorkContentScenario`, `BuildDelaySamples`, `Schedule` | `todos_monte_carlo.csv`, arquivos de replicação e estabilidade |
| Algoritmo A.6 | Avaliar sensibilidade à intensidade da perturbação | `BatchExperimentalStudyRunner.cs` | `RunSensitivity`, `WriteSensitivityCorrelations` | `sensibilidade.csv`, `sensitivity_correlations.csv`, `frm_absorption_by_gamma.csv` |
| Algoritmo A.7 | Gerar cenários de compressão temporal | `CpuCrashingAnalyzer.cs` | `BuildCandidates`, `BuildActiveCandidates`, `BuildScenarioDefinitions`, `GenerateScenarioDefinitionsRecursive` | `todos_crashing_candidates.csv` |
| Algoritmo A.8 | Recalcular cronograma, FRM e risco após crashing | `CpuCrashingAnalyzer.cs` | `EvaluateScenario`, `CloneWithCrashing`, `AggregateReplications`, `ComputeFrri` | `todos_crashing.csv`, `todos_integrated.csv` |
| Algoritmo A.9 | Aplicar pesos e estatísticas confirmatórias | `BatchExperimentalStudyRunner.cs` e `ExperimentalStatistics.cs` | `BuildEqualInstanceBaselineWeights`, `WriteWeightedChapter4Results`, `WeightedPearson`, `WeightedSpearman`, `WilcoxonSignedRankTest` | `chapter4_weighted_results.csv`, correlações e testes |
| Algoritmo A.10 | Persistir arquivos, tabelas e dados para gráficos | `ExperimentalCsv.cs`, `BatchExperimentalStudyRunner.cs`, `FormExperimentalTese.ChartsTab.cs` | `WriteConsolidated`, `WriteGraphData`, `WriteReportTables`, `WriteRows` | CSV, JSON, PNG e XLSX |
| Apêndice H | Comparar heurísticas com a referência exata | `BatchExperimentalStudyRunner.cs` | `BuildModifiedDhBbInstanceComparisons`, `WriteModifiedDhBbSummary`, `WriteExactClassificationAudit` | arquivos `modified_dh_bb_*` |

## Arquitetura por projeto

| Projeto | Responsabilidade |
|---|---|
| `Main` | Add-in VSTO para Microsoft Project, importação, leitura e aplicação de cronogramas |
| `RCPSP.Contracts` | Objetos de entrada e saída compartilhados pelo pipeline |
| `RCPSP.Application` | Interfaces dos serviços e orquestração do pipeline |
| `RCPSP.Scheduling` | Regras de prioridade, esquemas de geração, CPM e método exato |
| `RCPSP.Infrastructure.Cpu` | Implementações de baseline, FRM, risco e crashing |
| `RCPSP.Infrastructure.MsProject` | Integração com Microsoft Project e importação PSPLIB |
| `RCPSP.WinForms` | Interfaces de análise exploratória e módulo experimental da tese |
| `RCPSP.Standalone` | Execução de arquivo `.rcp` sem iniciar o Microsoft Project |

## Delimitação metodológica preservada

O código de reparação reativa não integra a solução publicada. Essa exclusão é deliberada e coerente com a tese, que trata o escalonamento reativo como contexto teórico, não como núcleo do experimento. A implementação publicada concentra-se na avaliação probabilística de baselines fixos, no diagnóstico FRM e na reavaliação completa após compressão temporal.

## Como localizar uma saída

1. Identifique o arquivo no Apêndice C.
2. Consulte a linha correspondente na matriz acima.
3. Abra o método de persistência em `BatchExperimentalStudyRunner.WriteConsolidated`.
4. Siga a origem da linha até o componente responsável pelo cálculo.
5. Confirme os parâmetros em `00_config/experiment_config.json` e nos logs da execução.
