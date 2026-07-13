# Guia de publicação profissional no GitHub

## 1. Preparação local

1. Descompacte o pacote em uma pasta definitiva, por exemplo `RCPSP-FRM-Thesis`.
2. Adicione os scripts Python finais em `code/python`.
3. Adicione em `results` os arquivos leves, manifestos, checksums e tabelas de síntese. Prepare os resultados volumosos em arquivos ZIP separados para os ativos da *release*.
4. Revise `README.md`, `CITATION.cff`, `LICENSE` e o texto do Apêndice B.13.
5. Confirme que não existem certificados, senhas, tokens, caminhos pessoais ou dados confidenciais.

## 2. Instalação das ferramentas

Instale Git e Git LFS. Opcionalmente, instale o GitHub CLI para criar o repositório e a *release* pelo terminal.

## 3. Inicialização do repositório

No terminal, dentro da pasta raiz:

```bash
git init
git branch -M main
git lfs install
git lfs track "results/**/*.csv"
git lfs track "results/**/*.xlsx"
git lfs track "results/**/*.zip"
git lfs track "results/**/*.7z"
git add .gitattributes

Use esses comandos somente para os tipos de arquivo que realmente ficarão sob Git LFS. Para os grandes pacotes históricos ou consolidados, prefira anexos da *release*, evitando inflar o clone e o histórico do repositório.
```

Confirme o que será versionado:

```bash
git status
git lfs track
git diff --cached
```

## 4. Primeiro commit

```bash
git add .
git commit -m "chore: publish thesis computational artifacts v1.0.0"
```

Antes de publicar, execute:

```bash
git status
git ls-files | sort
```

Verifique especialmente se não aparecem `.pfx`, `.key`, `.suo`, `bin`, `obj`, `.vs` ou arquivos pessoais.

## 5. Criação do repositório no GitHub

Nome recomendado:

```text
rcpsp-frm-thesis
```

Descrição recomendada:

```text
Computational artifacts for a doctoral thesis on RCPSP, FRM, probabilistic delay risk, and project crashing.
```

Tópicos recomendados:

```text
rcpsp, project-scheduling, frm, monte-carlo, cvar, project-crashing, operations-research, reproducible-research
```

Com GitHub CLI:

```bash
gh auth login
gh repo create rcpsp-frm-thesis --public --source=. --remote=origin --push
```

Sem GitHub CLI, crie um repositório vazio na interface do GitHub e execute:

```bash
git remote add origin https://github.com/SEU-USUARIO/rcpsp-frm-thesis.git
git push -u origin main
```

Não inicialize o repositório remoto com README, licença ou `.gitignore`, pois esses arquivos já existem localmente.

## 6. Configuração da página

Na página do repositório:

1. preencha a descrição e os tópicos;
2. adicione o endereço da tese ou do RepositóriUM quando disponível;
3. confirme que `CITATION.cff` gera a opção “Cite this repository”;
4. mantenha `main` como ramificação padrão;
5. habilite Issues apenas se pretende receber relatos técnicos;
6. não use Wiki para informações científicas essenciais, pois a versão da tese deve permanecer dentro do repositório versionado.

## 7. Validação antes da release

No Windows de reprodução:

1. abra `code/csharp/RCPSP-FRM.sln`;
2. compile em `Release | Any CPU`;
3. execute um teste pequeno no standalone;
4. execute um teste do módulo experimental;
5. confirme a criação das pastas e dos consolidados;
6. execute os scripts Python finais;
7. confira tabelas e figuras;
8. gere checksums SHA-256 dos pacotes finais.

Registre qualquer ajuste em novo commit. O commit usado na tese deve estar limpo e reproduzível.

## 8. Tag e release final

```bash
git tag -a v1.0.0 -m "Thesis computational artifacts v1.0.0"
git push origin v1.0.0
```

Com GitHub CLI:

```bash
gh release create v1.0.0 \
  --title "RCPSP-FRM Thesis Computational Artifacts v1.0.0" \
  --notes "Frozen version used in the doctoral thesis."
```

Antes desse passo, atualize `CITATION.cff` com a data real da publicação da *release*. Anexe à *release* os pacotes de resultados que não devam ficar diretamente no histórico Git. A tese deve registrar:

- URL do repositório;
- nome da *release*;
- hash do commit;
- data da publicação.

## 9. Arquivos muito grandes

Não envie resultados volumosos como Git comum. O GitHub alerta para arquivos acima de 50 MiB e bloqueia arquivos acima de 100 MiB em Git normal. Para este estudo, a ordem recomendada é: arquivos pequenos e tabelas de síntese no repositório; pacotes ZIP dos resultados completos como ativos da *release*; Git LFS apenas quando a consulta individual no repositório justificar esse custo. Sempre publique os hashes SHA-256 e divida arquivos que excedam o limite aplicável ao seu plano.

## 10. Preservação científica

Após a defesa e a aprovação da versão final:

1. não altere a *release* `v1.0.0`;
2. faça correções futuras em novas versões, como `v1.0.1` ou `v1.1.0`;
3. mantenha o commit citado acessível;
4. considere integrar o GitHub ao Zenodo para emitir DOI;
5. atualize o Apêndice B.13 somente com referências definitivas.
