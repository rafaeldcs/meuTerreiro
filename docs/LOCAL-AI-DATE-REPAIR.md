# Correção nova produzida pela IA local — data ausente

Rodada de 29/09/2026; baseline `15665ba523a6955f8c6044adb7fb463c0cb843ee`.

O problema era `formatDate(null)` mostrar uma data de 1969, pois JavaScript interpreta `new Date(null)` como o timestamp zero. Codex encontrou a causa e ensinou uma especialidade restrita de ausência de data com exemplos sintéticos. O modelo geral anterior falhou; o alvo exato do projeto ficou fora do treino do novo especialista.

Saída original do LocalAuthor:

```javascript
if (value == null) return "Data indisponível";
assert.equal(formatDate(null), "Data indisponível");
```

A guarda foi inserida mecanicamente em `app/src/lib/presentation.mjs`, sem reescrita por Codex. A asserção foi colocada em `app/tests/local-ai-date-regression.test.mjs`; os imports, o título e o invólucro `node:test` são do professor. Hashes da fonte original, proposta e teste foram conferidos antes da aplicação. O diff contém somente uma linha de produto e o teste de regressão.

O teste falhou no código original e passou com a proposta. Os 196 testes JavaScript passaram. A avaliação independente preservou timestamp zero e datas válidas. Build Next.js, publish .NET 10 e os 138 testes C# passaram. A interface da simulação foi reconstruída na instância `meuterreiro-sim-five-final`, sem recriar o banco ou executar o seed.

Nove verificações funcionais passaram no navegador real: login de coordenação, lista de escalas da API real, data ausente injetada somente na resposta do navegador, desktop/celular, ausência de erros e retorno aos dados originais. Não se alteraram registros para fabricar uma data nula. Esse roteiro e suas verificações são autoria de Codex, executados pelo worker LocalAuthor; não são testes completos escritos pelo modelo.

Checkpoint SHA-256: `2aaff29ed2570eaa6d93b85f71cc987675cff75eb26d2dcaad3209ebf29e9ba0`. Modelo original de 103.936 parâmetros; 63 exemplos de treino, quatro de validação, 12 combinações reservadas e um alvo do projeto. Proposta e asserção foram geradas por inferência neural, sem consulta ao gabarito durante inferência. São variações de uma família ensinada, não prova de resolução geral de problemas desconhecidos.

Recibos locais em `artifacts/date-repair/` e no repositório LocalAuthor em `reports/date-repair-*`. O relatório completo de ensino e limitações é `ia-autoral/docs/APRENDIZ_PROGRAMADORA.md`. A investigação inicial e o fluxo de ferramentas ainda foram guiados pelo professor. Nenhuma qualificação de programação autônoma geral foi concedida.
