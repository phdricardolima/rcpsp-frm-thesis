# Auditoria estática e limpeza do código

## Escopo

A versão recebida continha código-fonte, artefatos compilados, caches locais, certificados temporários, documentos históricos de correção e arquivos não incluídos nos projetos. A limpeza teve como objetivo produzir uma versão apropriada para consulta pública e rastreabilidade científica.


## Dimensão antes e depois da preparação

| Indicador | Pacote recebido | Versão preparada |
|---|---:|---:|
| Arquivos | 529 | 129 antes do relatório e dos manifestos finais |
| Tamanho descompactado | 79.492.662 bytes | 1.279.747 bytes antes do relatório e dos manifestos finais |
| Arquivos C# | 109 | 89 |
| Linhas C# | 29.289 | 26.015 |
| Projetos C# | 11 | 8 |
| Arquivos TXT históricos | 111 | 0 |
| Comentários C# | 1.078 na origem | 14 comentários novos de rastreabilidade |

A redução não representa simplificação do método científico. Foram preservados os componentes usados pela tese: geração e deduplicação de baselines, referência exata, diagnóstico FRM, Monte Carlo, sensibilidade, crashing, reavaliação, consolidação e estatística.

## Elementos de código removidos por ausência de uso estático

- `Ribbon1.BuildApplyReport` e `Ribbon1.GetDropDownLabel`;
- `CpuCrashingAnalyzer.GetScenarioDurationDomain`;
- `MsProjectPsplibImporter.ResolveApplication`;
- `FormBaseline.Core.FindExactPendingRunIndex`;
- `FormBaseline.CrashingTab.CreateCrashCard`;
- `FormBaseline.CrashingTab.ConfigureCrashScatterChart`;
- `FormBaseline.RiskTab.GetPairedRisk`;
- `FormBaseline.UiHelpers.InterpretCrashScenario`;
- classe privada `SuccessfulRunRef`.

A decisão de remoção foi baseada em busca de declarações e referências no conjunto completo dos fontes. Como a reflexão e ligações externas não podem ser excluídas apenas por análise estática, a compilação e os testes funcionais no Windows continuam obrigatórios antes da publicação definitiva.

## Scripts Python

O pacote recebido não continha arquivos `.py`. Por isso, nenhum script Python foi inventado ou reconstruído. A pasta `code/python` foi preparada para receber apenas as versões finais efetivamente utilizadas na tese.

## Remoções realizadas

- diretórios `.vs`, `bin`, `obj` e `packages`;
- certificado temporário VSTO e referências de assinatura associadas;
- configurações locais de ferramentas;
- logs, relatórios históricos de correção e arquivos `LEIA_ME` intermediários;
- arquivo vazio e residual no diretório raiz;
- projeto vazio `RCPSP.Infrastructure.Serialization`;
- projeto de domínio sem referências efetivas `RCPSP.Core`;
- projeto de validação em console não usado pelo fluxo da tese `RCPSP.ConsoleRunner`;
- DTOs duplicados e não compilados;
- arquivo do módulo reativo não incluído no projeto e fora do escopo metodológico da tese;
- métodos privados sem qualquer referência estática;
- classe privada sem qualquer referência estática;
- comentários históricos, comentários de correção e blocos automáticos de comentários.

## Documentação adicionada

Foram mantidos apenas comentários curtos de rastreabilidade em 14 arquivos centrais. A explicação completa foi transferida para `THESIS_TRACEABILITY.md`, evitando poluir a implementação com comentários narrativos extensos.

## Validações executadas

- análise sintática de todos os arquivos C# com parser de linguagem;
- verificação de ausência de nós sintáticos inválidos;
- verificação de que todos os arquivos declarados em `Compile Include` existem;
- verificação de que todos os `ProjectReference` apontam para projetos existentes;
- verificação de que os projetos da solução existem;
- busca de referências a projetos e arquivos removidos;
- busca de DTOs duplicados;
- busca de métodos privados declarados e nunca referenciados;
- busca de comentários residuais, caminhos absolutos, credenciais e arquivos sensíveis;
- validação XML dos arquivos de projeto, recursos e configurações.

## Limite da auditoria

A compilação VSTO completa exige Windows, Visual Studio, .NET Framework 4.8, Microsoft Project e assemblies do Office. Esse ambiente não estava disponível durante a preparação do pacote. Portanto, a auditoria comprova consistência estrutural e sintática, mas a *release* pública deve ser precedida por uma compilação `Release | Any CPU` e por um teste funcional no ambiente Windows descrito em `REPRODUCIBILITY.md`.
