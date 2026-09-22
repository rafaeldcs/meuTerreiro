# Relatório de validação — 21/09/2026

**Versão avaliada:** 0.2.0-alpha.2. Relatório de execução local, não auditoria ou homologação de produção.

## Resultados obtidos

| Execução | Resultado | Evidência bruta |
|---|---|---|
| `node --test app/tests/*.test.mjs` | **138 testes aprovados; 0 falhas.** | `reports/node-tests.tap` |
| `python -m unittest -v test_processor` | **18 testes aprovados; 0 falhas.** | `reports/processor-tests.txt` |
| Renderização isolada + Chromium | **35 cenários de layout aprovados; 0 falhas.** | `reports/responsive.json`, `reports/screenshots/` |
| Parser JS/JSX | **41 arquivos; 0 erros sintáticos.** | `reports/syntax.json` |
| XML / YAML / JSON | Configurações analisadas sem erro de sintaxe. | `reports/configuration.json` |
| Configuração inicial | Credenciais geradas em pasta temporária, recusa de sobrescrita e preservação na atualização. | `reports/configuration.json` |
| Shell scripts | `bash -n` sem erro de sintaxe. | Verificação sintática; não execução do Docker/age. |

### Escopo JavaScript

Conversão exata de moeda/quantidade, versões e contagens da limpeza, navegação interna permitida, dados privados, política de service worker/cache, preservação de permissões e relação textual dos formulários com comandos da API. Alguns casos são testes estáticos de fonte: demonstram estrutura, não transações em um banco.

### Escopo do processador

PNG/JPEG sintéticos, PDF sem texto, limite de páginas, PDF criptografado, JavaScript/anexos/ações ativas, arquivos corrompidos e extração de valor/agendamento de documento de teste. O protocolo INSTREAM foi verificado com socket simulado: **não ocorreu varredura por um ClamAV com banco de assinaturas real**.

Versões efetivamente executadas: Python 3.13.5, Pillow 12.3.0, pypdf 5.9.0. O Dockerfile fixa Pillow 11.3.0 e pypdf 6.0.0; essas versões de produção do contêiner **não foram instaladas neste ambiente**. O CI deverá executar novamente com as dependências fixadas.

### Escopo responsivo

Sete fixtures: início, recebimentos, limpeza e formulários de compras, despesas, regras e recorrência. Viewports: **320×740, 390×844, 768×1024, 1024×768 e 1440×1000**.

Verificados: largura total da página, controles visíveis dentro da largura, área mínima dos botões/controles examinados, diálogo dentro da altura disponível e alternância entre menu lateral/navegação inferior. Dois problemas iniciais em tabelas de tablet/computador menor foram corrigidos com apresentação em cartões; a rodada final ficou sem falhas.

O harness transpila os componentes JSX e usa hooks simplificados, usuário/dados sintéticos e ícones substitutos. O Chromium recebeu HTML/CSS local por `set_content`. Não houve servidor Next, React em execução, requisições de negócio, login real, teste físico nem teste visual de todas as 37 áreas. O conteúdo das capturas não é um saldo da casa.

## Tentativas que não puderam ser concluídas

| Verificação | Resultado observado |
|---|---|
| .NET/xUnit | Executável `dotnet` ausente. Não compilado, não testado. |
| Next build | Comando executado; saída 127: `next: not found`. |
| Docker Compose | Executável `docker` ausente. Não validado pelo Compose e não orquestrado. |
| Dependências NuGet/npm | Hosts não puderam ser resolvidos. Download adicional de índice NuGet também falhou. |
| MongoDB/HTTP | Sem banco/replica set/API executáveis. O script de integração está escrito, não executado. |
| Banco Pix | Nenhuma credencial real ou Sandbox disponibilizada; integração não homologada. |
| Backup/restore | Scripts escritos com criptografia e destino isolado; sem execução Docker/age. |
| Publicação e celular | Sem URL pública, certificados testados, instalação Android/iPhone ou push efetivamente recebido. |

Saídas das tentativas em `reports/build-attempts.json`. A análise XML de `.csproj` não compila C#. O arquivo de workflow não prova que as Actions rodaram.

## Como reproduzir em ambiente preparado

```bash
cd app
npm install
npm test
npm run build
# Depois de revisar o lock gerado, versionar package-lock.json e usar npm ci.
cd ..
dotnet test api/Tests/Tests.csproj --configuration Release
dotnet build api/WebAPI/WebAPI.csproj --configuration Release
python -m pip install -r processor/requirements.txt
(cd processor && python -m unittest -v test_processor)
```

Integração somente com banco **novo e descartável**, sem pessoas/comprovantes reais:

```bash
node scripts/init.mjs
docker compose up -d --build api app gateway
node scripts/smoke.mjs
```

O smoke altera senhas e cria membros, escalas, perfis, mensalidades, recebimentos, despesas, estoque, conciliação e códigos de recuperação. Não execute no piloto existente, produção ou banco restaurado. Não assume que uma evidência de teste representa dinheiro.

O pipeline separa aplicação, API, processador e integração HTTP. A integração financeira base não inicia ClamAV para evitar confundir sua ausência com varredura aprovada; homologar o processador real é uma etapa adicional.

**Conclusão:** houve avanço real de implementação e verificações locais positivas, mas ainda não há evidência suficiente para afirmar que o sistema completo compila, funciona integrado, está seguro ou pronto para uso real.
