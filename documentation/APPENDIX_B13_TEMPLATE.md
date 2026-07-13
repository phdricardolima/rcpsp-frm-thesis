# Texto-base para a Seção B.13

## B.13 Controle de versão e disponibilidade dos artefatos computacionais

A reprodutibilidade externa da investigação depende da preservação dos artefatos computacionais utilizados na geração, consolidação e análise dos resultados apresentados no estudo computacional. Para esse fim, o código-fonte do framework, os scripts Python de pós-processamento, os arquivos de configuração e os resultados experimentais foram organizados em um repositório público e versionado no GitHub.

O repositório está disponível em **[INSERIR URL DEFINITIVA]**. A versão de referência da tese corresponde à *release* **v1.0.0**, associada ao *commit* **[INSERIR HASH]**, publicada em **[INSERIR DATA]**. Essa identificação distingue os artefatos efetivamente empregados na produção dos resultados finais de alterações posteriores realizadas no código ou na documentação.

O repositório separa os artefatos em três blocos. A pasta `code` contém a implementação C#, os scripts Python finais e a documentação das dependências. A pasta `results` contém as configurações, os arquivos por instância, os consolidados, os dados para gráficos, as figuras, os logs e as tabelas finais. A pasta `documentation` contém a matriz de rastreabilidade, o protocolo de reprodução, a estrutura dos resultados e o relatório de auditoria do código.

A disponibilização do código-fonte não substitui a descrição metodológica apresentada na tese. Os pseudocódigos do Apêndice A registram o encadeamento científico do framework, enquanto o repositório preserva o nível operacional necessário para inspeção, compilação e reexecução. Para fins de auditoria e reprodução, deve ser considerada exclusivamente a versão identificada pela *release* e pelo *commit* registrados nesta seção.
