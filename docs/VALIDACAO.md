# Validação — isenção por médium cadastrado

**Versão do código:** 0.2.2-alpha.1 · **Data:** 22/09/2026.

| Verificação executada | Resultado | Evidência |
|---|---|---|
| `node --test app/tests/*.test.mjs` | 195 testes, 195 aprovados, zero falhas | `reports/member-exemption-node-tests.tap` |
| Parser JS/JSX/CJS/MJS | 47 arquivos, zero diagnósticos de sintaxe | `reports/syntax.json` |
| Renderização isolada de componentes no Chromium | 60 cenários, 60 aprovados, zero falhas | `reports/responsive.json` |
| `dotnet --info` | Executável ausente; sem compilação ou xUnit | `reports/member-exemption-build-attempts.json` |
| `docker --version` | Executável ausente; sem MongoDB/integração Docker | Mesmo arquivo |
| `npm run build` | Falhou com código 127: `next: not found` | Mesmo arquivo |

## Alcance

Dos testes JS, 33 foram adicionados nesta revisão. Eles cobrem apresentação da isenção recorrente, motivo e vínculo do formulário, limites de vigência, possibilidade de encerramento, navegação por permissões, fonte dos dados, preservação de pagamentos e estrutura do código servidor. As verificações de fonte **não executam regras C# nem transações MongoDB**. Os 162 casos anteriores também passaram novamente.

Layout: 12 fixtures em cinco larguras (320, 390, 768, 1024, 1440), incluindo detalhes de isenção no cadastro e formulário de concessão. JSX real transpilado, hooks simplificados, dados sintéticos, ícones substitutos e HTML/CSS local. Verificados transbordamento horizontal, campos fora da largura, altura dos controles e limites dos diálogos. **Não é React/Next em execução, teste de cliques, autenticação, API, celular físico ou entrega de push.**

Uma asserção estática inicialmente confundiu comparação `PaidCents == 0` com atribuição. O padrão foi corrigido para diferenciar comparação e gravação; a rodada final passou integralmente. O código não grava pagamento fictício.

## Escrito, mas não executado

`api/Tests/MemberExemptionTests.cs` e a extensão em `scripts/smoke.mjs` cobrem vigência contínua, término inclusivo, encerramento exclusivo, sobreposição, múltiplas competências, escopo de casa/médium, independência de aprovação, preservação de recebimentos, restauração de descontos anteriores e histórico. Os comandos existem para execução num ambiente preparado, mas não foram executados aqui.

Nenhuma execução nova de ClamAV, Pix, instalação PWA, notificação em aparelho real, backup/restauração, publicação ou pipeline remoto. O relatório da revisão anterior está em `history/VALIDACAO-0.2.1.md`, apenas como histórico.

## Reproduzir antes da homologação

```bash
node --test app/tests/*.test.mjs
node scripts/inspect-source.cjs
# SDK .NET 10, Docker e dependências precisam estar disponíveis:
dotnet test api/Tests/Tests.csproj --configuration Release
(cd app && npm install && npm run build)
# A integração HTTP modifica dados e só pode usar um banco descartável:
# seguir README.md, scripts/init.mjs e scripts/smoke.mjs.
```

A funcionalidade foi incorporada ao código; não há evidência suficiente para declarar seu funcionamento integrado ou liberação de produção. Mantenha o bloqueio de produção e utilize dados fictícios até completar os testes.
