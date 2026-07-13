# Protocolo de reprodução computacional

## 1. Identificação da versão

A reprodução deve usar uma *release* específica e o respectivo hash de *commit*. Não utilize a ramificação `main` como referência científica definitiva.

Registre:

- nome da *release*;
- hash do *commit*;
- data da *release*;
- sistema operacional;
- versão do Visual Studio;
- versão do .NET Framework;
- versão do Microsoft Project;
- versão do Python e dependências dos scripts de pós-processamento.

## 2. Compilação do código C#

1. Instale o Visual Studio 2022 com desenvolvimento para desktop .NET e ferramentas do Office/VSTO.
2. Instale o .NET Framework 4.8 Developer Pack.
3. Instale o Microsoft Project Desktop para o add-in.
4. Abra `code/csharp/RCPSP-FRM.sln`.
5. Selecione `Release | Any CPU`.
6. Restaure ou resolva as referências do Office disponíveis no ambiente local.
7. Compile a solução.

O certificado temporário originalmente associado ao projeto VSTO não é distribuído. Para publicação ou instalação do add-in, gere e configure um certificado próprio. Para inspeção e desenvolvimento local, use a configuração de assinatura apropriada ao ambiente institucional.

## 3. Execução

### Add-in do Microsoft Project

O projeto `Main` contém o Ribbon, a importação de instâncias, o formulário de baseline e o módulo experimental da tese.

### Aplicação standalone

O projeto `RCPSP.Standalone` permite carregar e avaliar um arquivo `.rcp` sem executar o Microsoft Project. Ele é útil para inspeção do pipeline e testes em pequena escala.

## 4. Entradas mínimas

- instâncias PSPLIB no formato `.rcp`;
- conjunto de heurísticas e esquemas definido na configuração;
- parâmetros FRM;
- modo de perturbação;
- níveis de gamma;
- número de iterações e replicações;
- sementes;
- limites de candidatos, combinações e cenários de crashing;
- limite de tempo do Modified DH Branch-and-Bound.

## 5. Saídas e auditoria

A execução cria as pastas `00_config` a `06_report_tables`. Antes de interpretar os resultados, confirme:

1. `experiment_summary.csv` com status final `COMPLETED`;
2. arquivos de validação por instância;
3. logs de erros e exclusões;
4. distinção entre ótimo comprovado e incumbente;
5. deduplicação estrutural;
6. estabilidade das replicações de Monte Carlo;
7. recálculo de FRM e risco nos cenários comprimidos;
8. ponderação igual por instância nas análises agregadas.

## 6. Números aleatórios comuns

Quando a comparação entre baseline e cenário comprimido é pareada, preserve a configuração de números aleatórios comuns. Essa decisão reduz variação espúria e deve permanecer registrada no arquivo de configuração da execução.

## 7. Pós-processamento Python

Os scripts Python devem operar sobre cópias imutáveis dos consolidados. Cada execução de pós-processamento deve registrar:

- script e versão;
- parâmetros de linha de comando;
- arquivos de entrada;
- arquivos de saída;
- dependências;
- data e hora;
- hash dos arquivos de entrada.

## 8. Verificação de integridade

Antes da *release* final, gere SHA-256 para o código e para os pacotes de resultados. O manifesto deve permitir verificar se os arquivos baixados são idênticos aos usados na tese.
