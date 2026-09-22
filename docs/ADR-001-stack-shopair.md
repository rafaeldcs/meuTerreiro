# ADR-001 — mesma stack da ShopAir, aplicação separada

**Data:** 21/09/2026. **Decisão de Rafael:** mesmas tecnologias da ShopAir e instalação no celular.

## Evidência consultada

- `ShopAir-Sistemas/app`, branch `dev`, `package.json`, SHA `6833bd5305736426ea7b73372c70bc903e35905c`: Next.js `^16.2.11`, React/React DOM `19.2.0`, Tailwind `^3.4.17` e cliente JavaScript/JSX.
- `ShopAir-Sistemas/api`, branch `dev`, `Data/Data.csproj`, SHA `f404af2a136fb9ee479cf76390f2a7f5c93d3a88`: `net10.0` e `MongoDB.Driver` `3.9.0`.
- A busca do cliente também encontrou `src/components/PwaManager.jsx` e rotas de manifesto. Não é necessário introduzir Expo para construir uma experiência instalável usando essa base web.

## Decisão aplicada ao código

Next.js **16.3.5**, React/React DOM **19.2.7**, Tailwind 3.4.17; API .NET 10; MongoDB.Driver 3.9.0; Docker; cliente PWA. Bibliotecas de comércio eletrônico da ShopAir não foram copiadas sem necessidade. Para transporte Web Push, foi acrescentada `Lib.Net.Http.WebPush` 3.3.1, com VAPID e AES128GCM. A dependência e sua integração precisam de build, auditoria e teste em dispositivos antes do piloto.

A proposta anterior de React Native/Expo ou PostgreSQL está **substituída** por esta decisão. A especificação funcional v0.3 continua como backlog; suas escolhas tecnológicas ainda apresentadas como propostas não prevalecem sobre este ADR.

## Manutenção de segurança

A stack foi preservada, mas o novo projeto não fixa a versão mínima antiga `16.2.11` do manifesto da ShopAir. O aviso oficial de 25/08/2026 exige Next.js 16.3.3 ou posterior na linha ativa para suas correções. A documentação consultada apresenta 16.3.5; essa versão foi fixada aqui. React/React DOM foram mantidos na família 19.2, com manutenção 19.2.7, listada oficialmente. Não se introduziu React 19.3 nem outra tecnologia.

O intervalo `^16.2.11` observado no manifesto permite versões mais recentes; sem ler lock e runtime, ele **não prova qual versão está implantada na ShopAir**. Nenhum pacote da ShopAir foi modificado. A decisão deste projeto também não substitui `npm audit`, restore, build ou homologação.

## Isolamento

Mesmo conjunto de tecnologias não significa compartilhar código privado integral, regras comerciais, banco, autenticação, segredos ou deploy da ShopAir. Este projeto usa `terreiro_hml`, rede e volumes próprios, um usuário MongoDB próprio e uma aplicação nova. Não houve gravação no GitHub nem acesso à VPS nesta entrega.

## Consequências

O pacote é fonte de uma PWA, não APK/IPA. A instalação ocorre a partir do endereço publicado em HTTPS. Web Push no iPhone exige uma combinação compatível de versão e instalação na tela inicial. A experiência deve ser homologada, não deduzida da existência do manifesto. O painel no computador não pede permissão de notificações.

MongoDB será usado com transações de replica set para persistir atividade, avisos e auditoria juntos. Um replica set de um nó atende ao desenho transacional do piloto, mas não proporciona alta disponibilidade. Não degradar para gravações independentes se a transação falhar.

## Fontes públicas para revisão técnica

- Next.js, guia de PWA: https://nextjs.org/docs/app/guides/progressive-web-apps
- WebKit, Web Push em apps instalados no iOS/iPadOS: https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/
- MongoDB, transações no driver C#: https://www.mongodb.com/docs/drivers/csharp/current/crud/transactions/
- Biblioteca de transporte: https://www.nuget.org/packages/Lib.Net.Http.WebPush/3.3.1

As versões da ShopAir foram observadas no repositório, não são declaração de ausência de vulnerabilidades. O código deve passar por restore, build e análise das dependências antes de publicação.

- Next.js, correções de agosto: https://nextjs.org/blog/august-2026-security-release
- React, versões publicadas: https://react.dev/versions
