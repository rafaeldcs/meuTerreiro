# Aplicativo de gestão administrativa do terreiro
## Especificação funcional e técnica — versão 0.3

**Casa de referência:** Centro de Umbanda Caboclo Tenda d’Água  
**Elaborado para:** Rafael  
**Data:** 21 de setembro de 2026  
**Situação:** proposta para desenvolvimento; não representa aplicativo implementado, integração bancária homologada ou auditoria de segurança concluída.

### Histórico desta versão

**Versão 0.3 — notificações exclusivamente no aplicativo do celular:** aplica a decisão de Rafael de não usar WhatsApp e de concentrar toda comunicação do sistema no próprio aplicativo. Define central interna e push móvel como os únicos meios de notificação do produto, sem e-mail, SMS, outros mensageiros ou push de desktop, inclusive como alternativa em caso de falha. Acrescenta requisitos NOT-01–NOT-20 e testes T53–T70; ajusta limpeza, telas, arquitetura, dados e implantação. Mantém os controles das versões anteriores.

**Versão 0.2 — limpeza e escalas:** acrescenta limpeza recorrente com quantidade configurável de pessoas, mutirões de participação geral e composição mista antes/depois de eventos. Detalha público convocado, confirmações, substituições, presença, tarefas, consumo de materiais, telas, permissões e testes T31–T52. Mantém os controles financeiros e documentais da versão 0.1.

As decisões confirmadas são permitir equipes de X pessoas, limpeza com todos nos eventos e notificações somente no celular, dentro do próprio sistema, sem WhatsApp. Para concretizar a exclusividade de canal, esta versão também exclui notificações por e-mail, SMS, outros mensageiros e desktop. Os demais detalhes operacionais são propostas de implementação/configuração, a validar com a casa; não são regras de participação já aprovadas nem funcionalidades implementadas.

## 1. Objetivo e premissas

Organizar mensalidades, doações, caixa, despesas, compras, estoque, patrimônio, eventos, limpeza, escalas e prestação de contas com evidências verificáveis e responsabilidades identificadas. As políticas financeiras pertencem à casa; o aplicativo registra e aplica as políticas aprovadas, sem definir regras religiosas.

Princípio central: **arquivo recebido não é dinheiro recebido**. O desenho separa segurança do arquivo, análise documental, confirmação financeira e destinação do valor. Um documento pode estar legível e coerente sem que o recebimento tenha sido confirmado. Um pagamento pode ter sido recebido mesmo que o comprovante enviado esteja ilegível.

Para transferências, a referência financeira será a informação obtida na conta recebedora autorizada — por integração autenticada ou conferência independente da tesouraria. Para dinheiro físico, será o recebimento registrado e a conferência do caixa. O Banco Central recomenda conferir o crédito no extrato, inclusive no contexto de Pix agendado [S1].

Não se promete detectar toda falsificação, impedir toda fraude interna ou classificar automaticamente uma pessoa como fraudadora. A análise deve permitir erro de preenchimento, pagamento por terceiros, falhas bancárias e revisão humana.

### Premissas iniciais

- Uso inicial por uma casa, com segregação explícita por organização no modelo caso se amplie o produto.
- Moeda inicial: real brasileiro. Datas de apresentação em America/Sao_Paulo; instantes técnicos armazenados em UTC, preservando o horário original recebido do provedor.
- Contribuição mensal sujeita às regras aprovadas da casa; doação voluntária é uma categoria separada.
- Perfis de referência: membro, tesouraria, dirigente/Pai de Santo e administrador. Secretaria, estoque, revisor e responsáveis por projetos são permissões complementares.
- Banco, titularidade da conta, provedor de pagamentos, limites de aprovação, política de retenção e tecnologias finais ainda não foram definidos.
- O escopo administrativo não inclui consultas espirituais, registros de saúde, punições religiosas ou exposição de inadimplentes.
- Todas as notificações do produto pertencem ao aplicativo do celular: central interna persistida no servidor e push móvel quando permitido. Não há canal externo de mensagem nem fallback por WhatsApp, e-mail, SMS ou desktop. O painel administrativo no computador pode continuar sendo utilizado para gestão, sem alertas push de desktop.
- Aviso registrado, push aceito pelo provedor, abertura no aplicativo e resposta a uma solicitação são informações distintas; nenhuma equivale automaticamente a presença, quitação ou aprovação.

## 2. Modelo de confiança

| Informação | Origem | Uso permitido no desenho |
|---|---|---|
| Valor, data e mês digitados | Usuário | Solicitação; nunca confirmação financeira. |
| Texto extraído de imagem/PDF | Arquivo do usuário | Pré-preenchimento e identificação de divergências. |
| Metadados e aparência visual | Arquivo do usuário | Indícios documentais fracos; não determinam fraude. |
| Assinatura digital verificável | Documento e cadeia de confiança | Verificar integridade/autoria dentro do escopo da assinatura, não crédito atual. |
| Aviso de pagamento/webhook | Provedor autenticado | Iniciar processamento e verificação do recebimento. |
| Consulta financeira autenticada | Conta recebedora no provedor | Confirmar os dados financeiros que o provedor efetivamente disponibilizar. |
| Extrato importado | Tesouraria | Apoiar conciliação; arquivo importado não se torna prova independente por ter formato OFX/CSV/PDF. |
| Conferência manual no banco | Tesouraria identificada | Evidência operacional documentada, com revisão conforme a política. |
| Recibo de dinheiro | Operador do caixa | Registrar declaração de recebimento, sujeita à contagem e revisão do caixa. |

Um identificador EndToEndId copiado para uma imagem não autentica a imagem. A especificação Pix prevê consulta autenticada de um Pix por e2eid no contexto do prestador de serviços da conta recebedora [S2]. Não se deve oferecer uma suposta consulta pública universal a qualquer transação de qualquer pessoa.

## 3. Perfis e separação de responsabilidades

| Perfil | Pode fazer | Não pode fazer por padrão |
|---|---|---|
| Membro | Ver suas cobranças, iniciar pagamento, enviar comprovante, consultar recibos, pedir revisão; consultar suas escalas, responder e solicitar troca. | Ver documentos de outros membros, dar baixa, mudar dados bancários ou aprovar despesas. |
| Tesouraria | Conferir créditos, conciliar, registrar caixa e despesas, vincular recebimentos, preparar fechamento. | Aprovar o próprio reembolso ou excluir movimentação confirmada. |
| Dirigente | Consultar consolidados, aprovar exceções e despesas sob sua responsabilidade. | Acessar automaticamente todos os documentos pessoais apenas por ser dirigente. |
| Secretaria | Cadastros administrativos, comunicações autorizadas e registros de escala em nome de quem não usa o aplicativo, com permissão específica. | Alterar recebimentos, analisar ocorrências restritas ou conceder acesso financeiro. |
| Estoque | Receber materiais, registrar consumo, reservar e contar itens. | Registrar dinheiro como recebido ou aprovar o próprio ajuste extraordinário. |
| Coordenação de limpeza | Planejar e publicar escalas autorizadas, dividir tarefas, aprovar trocas, verificar participação e encerrar atividades sob sua responsabilidade. | Ver comprovantes financeiros, motivos privados de outras atividades ou aplicar penalidades financeiras. |
| Revisor/contador | Consultar documentos necessários, verificar conciliação e fechamento. | Movimentar dinheiro, salvo atribuição separada e explícita. |
| Administrador técnico | Contas de usuário, operação técnica e configurações autorizadas. | Receber automaticamente o poder de quitar, devolver ou alterar cobranças. |

As autorizações precisam ser aplicadas no servidor, em cada operação e em cada recurso; esconder um botão não é controle de acesso [S6][S7]. Uma pessoa pode acumular funções, mas operações com conflito de interesse devem exigir outra pessoa. Quando isso não for operacionalmente possível, a exceção deve ficar explícita e sujeita a revisão independente, sem dois cliques da mesma pessoa simularem duas aprovações.

### Operações com controle reforçado

Alteração da conta recebedora, concessão de acesso financeiro, reembolso próprio, devolução, baixa manual excepcional, mudança retroativa de regra, ajuste relevante de estoque e reabertura de fechamento exigem justificativa, reautenticação e aprovação independente conforme a política. Alterar valor ou destinatário após aprovação invalida a aprovação anterior.

## 4. Mensalidades e cobranças

| ID | Requisito | Critério objetivo |
|---|---|---|
| MEN-01 | Regras de contribuição com vigência. | Mudar valor futuro não altera competências já geradas. |
| MEN-02 | Gerar mensalidade por vínculo, competência e tipo. | Reexecutar geração não duplica obrigação. |
| MEN-03 | Registrar afastamento, redução, isenção e cancelamento. | Cada exceção tem motivo, vigência, autor e aprovação aplicável. |
| MEN-04 | Permitir pagamentos parciais e antecipados. | Saldo é calculado a partir de ajustes e alocações confirmadas. |
| MEN-05 | Permitir pagamento por terceiros. | Pagador e membro beneficiado são entidades distintas. |
| MEN-06 | Distribuir um pagamento entre meses ou membros. | Soma destinada não ultrapassa o valor disponível do recebimento. |
| MEN-07 | Tratar excesso ou duplicidade como crédito a destinar. | Não converter sobra em doação silenciosamente. |
| MEN-08 | Manter recibos e correções rastreáveis. | Recibo cancelado/substituído continua identificado no histórico autorizado. |

A alteração de vínculo não elimina dívida nem histórico automaticamente. A casa deve aprovar a política de tratamento do saldo anterior. Beneficiário de outro membro só pode ser selecionado por delegação autorizada ou por intervenção da tesouraria, sem expor a lista e a situação financeira de toda a comunidade.

### Separação de estados

**Mensalidade:** aberta, parcialmente quitada, quitada, isenta ou cancelada. “Em atraso” é uma condição derivada de vencimento e saldo, não uma conclusão sobre a conduta da pessoa.

**Cobrança no provedor:** criada, ativa, concluída, expirada ou cancelada, segundo o mapeamento homologado de cada integração. Não é a mesma entidade da mensalidade.

**Comprovante:** recebido, em processamento, disponível para análise, aguardando informação, conciliado, não aceito como evidência ou substituído. Segurança técnica do arquivo fica em campo separado: quarentena, liberado, bloqueado ou análise inconclusiva.

**Recebimento:** confirmado, com devolução parcial ou com devolução integral. Valores bloqueados/indisponíveis são controles adicionais de disponibilidade; bloqueio não equivale automaticamente a devolução.

**Caso de revisão:** aberto, aguardando membro, aguardando banco, em revisão independente ou encerrado com motivo.

O botão “enviar comprovante” não altera o estado financeiro da mensalidade. A aprovação da qualidade documental também não o altera. A quitação depende da confirmação do recebimento e de sua alocação.

## 5. Entrada segura e análise do arquivo

### 5.1 Recebimento e proteção

**ARQ-01:** aceitar inicialmente JPG, PNG e PDF, com proposta configurável de 10 MB por arquivo e 5 páginas por envio. Conferir tipo real e conteúdo, não somente extensão. Arquivos protegidos por senha, corrompidos ou fora do limite exigem reenvio, sem acusação.

**ARQ-02:** guardar o original em área privada de quarentena, com nome interno gerado pelo servidor, autor, organização, tamanho, horário e hash calculado no servidor. Processar em ambiente limitado, sem executar conteúdo ativo ou seguir links do arquivo. Usar verificação antimalware, controles de memória, tempo, páginas e pixels. Falha na análise não deve liberar o arquivo silenciosamente.

**ARQ-03:** oferecer prévia segura separada do original. Downloads dependem de autorização e links temporários; nunca usar endereço público permanente. Bibliotecas de processamento devem ser mantidas e testadas. O tratamento segue defesa em camadas para uploads, não um único verificador [S4][S5].

### 5.2 Extração de dados

**ARQ-04:** tentar leitura de texto do PDF primeiro; usar reconhecimento de texto de imagem quando necessário. Extrair valor, data, horário, recebedor, pagador, instituição, identificador da transação e indicação de agendamento, se presentes.

Preservar três versões: dado informado pelo usuário, dado extraído e dado obtido da fonte financeira. Campo ausente não é valor zero. Confiança baixa de leitura exige conferência, não rejeição automática. Correções manuais registram autor e justificativa, sem reescrever o arquivo original.

### 5.3 Duplicidade e integridade

**ARQ-05:** SHA-256 do original permite detectar reenvio de conteúdo idêntico e conferir se a cópia preservada mudou em relação ao registro protegido. O hash não atesta autenticidade do documento e não detecta toda variante editada.

**ARQ-06:** comparação por similaridade visual e por dados extraídos pode sugerir documentos relacionados. Não bloquear um pagamento verdadeiro só porque o arquivo é parecido com outro. Um reenvio da mesma evidência para completar uma distribuição legítima deve ser relacionado ao mesmo recebimento, não tratado como nova receita.

**ARQ-07:** metadados, datas de criação, fontes e sinais visuais serão auxiliares opcionais. O aplicativo não exibirá “fraude comprovada” nem percentuais de autenticidade sem validação específica do método. Compatibilidade com aplicativos bancários diferentes deve ser testada com amostras autorizadas.

**ARQ-08:** quando houver assinatura digital, verificar assinatura, integridade coberta, cadeia, emissor e condições de validação. Assinatura válida de qualquer pessoa não prova emissão pelo banco. Resultado indeterminado exige leitura do relatório técnico. A ausência de assinatura em um print não indica falsificação. O VALIDAR do ITI verifica assinaturas eletrônicas, não crédito na conta [S3].

### 5.4 Uso opcional de inteligência artificial

No primeiro lançamento, priorizar regras verificáveis e conciliação, não um classificador generativo de fraude. Uma futura IA pode ajudar a extrair ou resumir dados, sem acesso a comandos de aprovação, credenciais bancárias ou execução de instruções contidas no documento. Exigir testes de falsos positivos e revisão de privacidade antes de enviar comprovantes a terceiros.

## 6. Confirmação financeira: três caminhos

### 6.1 Pix identificado dentro do aplicativo — caminho preferencial

1. O membro escolhe as mensalidades ou a finalidade de doação.
2. O servidor calcula o total, verifica permissões e cria a intenção de pagamento.
3. O provedor cria cobrança identificada; o servidor guarda o identificador e a conta recebedora autorizada.
4. O aplicativo apresenta QR Code/Pix copia e cola e o favorecido para conferência.
5. O membro paga no próprio banco. Não entrega senha bancária ao aplicativo.
6. O servidor recebe aviso autenticado e consulta a transação no provedor.
7. Confere organização, conta recebedora, ambiente, valor, referência e situação financeira.
8. Registra uma única entrada, vincula às obrigações e emite o recibo após a confirmação persistida.

A API Pix é uma especificação de integração dos serviços oferecidos pelo PSP recebedor; o aplicativo não se conecta ao Banco Central como se fosse um banco. A integração concreta depende dos serviços e da contratação com o provedor [S2].

**PIX-01:** uma intenção pode possuir múltiplas tentativas de cobrança, mas não pode criar receita duplicada. Cancelamento local ou expiração não autorizam ignorar dinheiro que o banco confirme posteriormente: isso deve gerar crédito/revisão.

**PIX-02:** autenticar webhooks conforme documentação do provedor, com validação adicional da conta e consulta autenticada. Há implementações que usam mTLS, como a documentação da Efí; não presumir o mesmo protocolo para todos os bancos [S10].

**PIX-03:** credenciais, certificados e segredos existem somente no servidor, com escopos mínimos. O módulo de arrecadação não precisa, por padrão, de permissão para enviar dinheiro.

**PIX-04:** tratar repetição, atraso e inversão da ordem dos eventos. Persistir o evento de forma durável antes de responder; reprocessamento deve ser seguro e monitorado. Uma consulta de recuperação deve encontrar recebimentos que chegaram sem aviso.

**PIX-05:** diferenciar homologação e produção. Um resultado simulado jamais pode quitar mensalidade real. Mensagens de erro ou indisponibilidade ficam como pendência técnica, não como negativa de pagamento.

### 6.2 Transferência externa e comprovante enviado

1. O membro envia o arquivo e informa a destinação pretendida.
2. O sistema analisa o arquivo e sugere correspondências, sem baixa.
3. A tesouraria consulta a conta recebedora por canal autorizado.
4. Confirma identificador disponível, direção de entrada, conta, valor e data.
5. Resolve diferenças e a propriedade da destinação; valor igual e nome parecido não bastam quando há ambiguidade.
6. Vincula a evidência a um recebimento único e registra a revisão.

**CON-01:** guardar referência da operação, origem da verificação, conta, horário da consulta, responsável e observação necessária. Não guardar senha ou extrato do pagador.

**CON-02:** OFX/CSV importado deve ter lote, conta, período, arquivo original protegido, validação de totais e prevenção de duplicidade. Não é uma assinatura do banco por definição. Importações sobrepostas devem se relacionar às transações já existentes, inclusive quando também vieram por API.

**CON-03:** nunca conciliar automaticamente apenas por valor e data. Na ausência de referência inequívoca, o sistema apresenta candidatos e requer revisão.

**CON-04:** se não houver integração, a primeira versão pode operar com conferência manual real. Ela deve informar “confirmado pela tesouraria”, sem inventar “verificado automaticamente pelo banco”.

### 6.3 Dinheiro físico

**CX-01:** abrir caixa com saldo inicial e operador, registrar recebimento numerado, beneficiário, finalidade e recibo.

**CX-02:** registrar retirada, reforço e depósito bancário com vínculos e responsáveis. Depósito de dinheiro já registrado é transferência, não nova doação.

**CX-03:** encerrar com saldo esperado, contagem física, divergência e revisão. Não gerar ajuste silencioso para o caixa “bater”. Falta de dinheiro continua visível até resolução documentada.

**CX-04:** se houver indisponibilidade do aplicativo, usar formulário/recibo de contingência autorizado com numeração própria e posterior transcrição identificada. Não emitir confirmação bancária offline.

## 7. Regras financeiras que não podem ser contornadas

| ID | Regra | Implementação proposta |
|---|---|---|
| FIN-01 | Não criar duas entradas para o mesmo recebimento. | Identidade canônica por organização, conta e identificador financeiro; mapear aliases entre conectores e importações. |
| FIN-02 | Não alocar valor além do disponível. | Controle transacional sobre recebimento, créditos, devoluções e alocações. |
| FIN-03 | Não quitar acima da obrigação ajustada. | Excesso permanece como crédito, nunca como valor “perdido”. |
| FIN-04 | Duas aprovações simultâneas não duplicam baixa. | Restrição de unicidade e concorrência no banco, não apenas na interface. |
| FIN-05 | Não apagar lançamento confirmado. | Correção por reversão/ajuste vinculado, preservando histórico autorizado. |
| FIN-06 | Transferência interna não é receita ou despesa consolidada. | Par de movimentações ligadas entre origem e destino. |
| FIN-07 | Doação de material não aumenta o caixa. | Entrada em doações e estoque/patrimônio, sem dinheiro fictício. |
| FIN-08 | Receber material comprado não repete desembolso. | Compra, recebimento e pagamento vinculados, mas independentes. |
| FIN-09 | Fechamento não admite mudança silenciosa. | Reabertura autorizada ou ajuste identificado, com versão do relatório. |
| FIN-10 | Aprovação não é execução bancária. | Registrar aprovação e confirmação de pagamento em etapas distintas. |

Valores monetários serão representados de forma exata, por exemplo em centavos inteiros, com limites explícitos; não usar ponto flutuante para saldos. Atualizar recebimento, alocações e lançamentos dentro de transação de banco. Se uma parte falhar, a operação não pode ficar parcialmente quitada. Transações e tratamento de conflitos de concorrência são recursos documentados do EF Core, caso essa tecnologia seja adotada [S11][S12].

### Exemplo de conservação de valor

Recebimento de R$ 150,00: destinar R$ 50,00 a agosto, R$ 50,00 a setembro e R$ 50,00 a outro membro. Total destinado: R$ 150,00. Novo envio do comprovante não gera novos R$ 150,00 nem pode quitar um quarto mês.

Uma devolução posterior exige lançamento financeiro próprio e revisão das destinações afetadas. Se parte da obrigação for perdoada por decisão da casa, isso será ajuste administrativo separado — não manutenção de pagamento que deixou de existir.

## 8. Ocorrências, devoluções e comunicação

**REV-01:** abrir ocorrência por divergência objetiva, com código de motivo e evidências. Motivos iniciais: leitura incompleta, possível reenvio, destinatário divergente, valor divergente, crédito não localizado, agendamento, transação já destinada e assinatura inconclusiva.

**REV-02:** separar observação interna de mensagem ao membro. Exemplos de texto apresentado ao membro dentro do aplicativo: “Precisamos confirmar o crédito na conta da casa” ou “O valor localizado é diferente do valor informado; a tesouraria está conferindo”. Não afirmar tentativa de fraude com base em um alerta.

**REV-03:** permitir esclarecimento, reenvio e revisão por outra pessoa. Encerrar casos com resultado como erro de preenchimento, pagamento por terceiro, falha de leitura, duplicidade de envio, ausência de confirmação ou encaminhamento restrito para apuração. Não gerar lista de suspeitos.

**REV-04:** lembretes de cobrança respeitam análise em andamento por prazo operacional definido. Vencido o prazo, escalar a revisão em vez de suspender lembretes indefinidamente por reenvios sucessivos. Em atraso técnico da integração, evitar pedir novo pagamento como primeira resposta.

**DEV-01:** devolução depende de recebimento confirmado, valor ainda devolvível, motivo, autorização e confirmação da execução. Preferir operação de devolução vinculada ao recebimento original quando o provedor permitir; não devolver a uma chave diferente informada em mensagem sem revisão reforçada.

**DEV-02:** distinguir devolução solicitada, em processamento, concluída e falha. Só a execução confirmada altera o caixa; o pedido pode reservar disponibilidade. Recalcular obrigações afetadas sem apagar o pagamento original.

**DEV-03:** bloqueios bancários e contestações afetam a disponibilidade e geram revisão. Não tratar bloqueio como devolução liquidada nem acusar o membro automaticamente. Regras específicas dependem dos eventos disponibilizados pelo banco.

## 9. Controles equivalentes nas demais áreas

| Módulo | Fluxo funcional | Proteção proposta |
|---|---|---|
| Doação financeira | Promessa → recebimento → destinação → utilização. | Promessa não aumenta saldo; conta e recebimento devem ser conferidos; anonimato público não exige coletar identidade desnecessária. |
| Campanha/fundo | Objetivo → orçamento → entradas vinculadas → despesas → prestação. | Identificar saldo reservado separadamente do saldo livre; mudança de destinação exige autorização e respeito às condições assumidas. |
| Doação material | Registro → conferência física → entrada → destinação. | Quantidade recebida pode diferir da prometida; avaliação de valor não vira caixa. |
| Despesa | Solicitação → documento → aprovação → pagamento → conciliação. | Favorecido e valor aprovados ficam vinculados; mudança reinicia aprovação. |
| Reembolso | Gasto próprio → documento → revisão → aprovação → pagamento. | Solicitante não aprova o próprio pedido; alertar documento repetido sem recusar rateios legítimos. |
| Adiantamento | Autorização → saída → prestação → devolução/complemento. | Dinheiro entregue não é despesa comprovada; manter pendência até prestação. |
| Compra | Necessidade → consulta ao estoque → cotação → pedido → recebimento → pagamento. | Conferir pedido, entrega e cobrança; tolerâncias e recebimentos parciais explícitos. |
| Fornecedor | Cadastro → validação → vigência → alteração. | Mudança bancária exige revisão por canal conhecido e histórico, não só e-mail recebido. |
| Estoque | Entrada → reserva → retirada/consumo → devolução → inventário. | Proibir saldo negativo por padrão; ajuste com motivo e revisão; dupla baixa concorrente não pode exceder o disponível. |
| Validade/perdas | Identificação → segregação → registro → destinação. | Perda não é apagamento de estoque; registrar lote, quantidade e responsável quando aplicável. |
| Patrimônio | Aquisição/doação/cessão → localização → manutenção → empréstimo → baixa. | Bem particular cedido é separado do bem da casa; transferência e descarte com responsabilidade identificada. |
| Evento | Planejamento → orçamento → reserva → execução → encerramento. | Separar movimentação de caixa e materiais utilizados; sobras e devoluções ficam identificadas. Relacionar limpezas de preparação e encerramento, preservando pendências de cada uma. |
| Limpeza e escalas | Planejamento → seleção/convocação → resposta → execução → verificação → encerramento. | Quantidade configurável ou participação geral; convite não é presença; tarefa concluída não baixa material nem quita mensalidade automaticamente. |
| Ação social | Recebimento → separação → distribuição → saldo → resultado. | Minimizar cadastro de beneficiários; divulgar consolidados, não identidades. |
| Orçamento | Previsão → aprovação → realizado → revisão. | Doação incerta não aparece como dinheiro disponível; revisão não altera histórico do orçamento original. |
| Documentos | Cadastro → acesso → revisão → retenção/eliminação. | Acesso por necessidade, vencimentos e trilha; não expor contratos e comprovantes no mural público. |

A escrituração e exigências legais específicas devem ser alinhadas com os responsáveis profissionais da casa; este desenho não substitui a definição contábil ou jurídica.

### 9.1 Limpeza, conservação e escalas

#### 9.1.1 Objetivo e modalidades

Organizar quem participa da limpeza, quando, em qual área e com quais materiais, sem confundir estar escalado, ter confirmado disponibilidade e ter participado. Este é um módulo operacional próprio, ligado à agenda, aos eventos, aos membros e ao estoque, mas separado da situação financeira dos participantes.

| Modalidade | Regra proposta | Exemplo ilustrativo |
|---|---|---|
| Equipe com quantidade definida | A coordenação define a quantidade-alvo por turno; mínimo e limite máximo são configurações independentes quando necessários. | Limpeza de sábado com equipe-alvo de 4 pessoas. |
| Mutirão geral | Convocar o conjunto de pessoas definido para a atividade, sem limitar a participação a uma quantidade fixa. | Todos os membros ativos abrangidos pelo evento, considerando dispensas autorizadas. |
| Organização mista | Criar turnos vinculados ao mesmo evento, cada um com sua modalidade. | Equipe de 4 pessoas antes da festa e limpeza geral ao final. |

A quantidade-alvo não é automaticamente um teto: uma quinta pessoa pode ajudar uma equipe-alvo de quatro, se não houver limite configurado. Por outro lado, alcançar o mínimo não preenche silenciosamente a quantidade-alvo. O painel mostra separadamente o atendimento desses critérios.

**O significado de “todos” é obrigatório no planejamento.** Opções propostas: todos os membros ativos abrangidos pela atividade; todos os integrantes de um grupo; ou todos os participantes de um evento segundo uma lista administrativa identificada. Doadores, fornecedores, visitantes e todo o cadastro de pessoas não entram automaticamente. A regra escolhida, os filtros e o conjunto selecionado ficam registrados.

No modo “participantes do evento”, a origem da lista deve ser definida: inscritos, confirmados ou presença registrada. Mudanças posteriores geram uma revisão explícita da escala, e não inclusão ou exclusão silenciosa. A lista publicada é versionada; a coordenação pode sincronizar novos participantes, revisar dispensas e comunicar alterações. Não se presume presença individual de visitantes que não foram cadastrados.

#### 9.1.2 Cadastro da atividade e dos turnos

Uma atividade de limpeza pode ser avulsa, recorrente ou vinculada a um evento. Cada turno possui data, horário, local, responsável, modalidade, público, política de resposta, tarefas e materiais próprios. O nome da atividade não substitui o vínculo estruturado com o evento.

| Campo | Uso e regra proposta |
|---|---|
| Título e finalidade | Identificar limpeza regular, preparação, apoio durante evento, encerramento ou conservação. |
| Origem e evento relacionado | Avulsa, regra recorrente ou evento específico; não duplicar o mesmo turno por reprocessamento. |
| Data, início e fim | Horários definidos ou derivados do evento. “Após o encerramento” pode ter previsão ajustável, claramente apresentada. |
| Local e áreas | Salão, cozinha, banheiros, entrada, pátio ou áreas cadastradas pela casa. |
| Coordenador e substituto | Pessoas autorizadas a organizar, verificar presença e acompanhar pendências. |
| Modalidade e público | Equipe definida ou participação geral; turnos mistos são combinados na mesma atividade. |
| Quantidade-alvo, mínimo e máximo | Números inteiros positivos quando aplicáveis; mínimo ≤ alvo ≤ máximo, quando os três estiverem definidos. |
| Forma de seleção | Designação manual, equipe predefinida, voluntariado com vagas ou sugestão de rodízio. |
| Prazo de resposta | Prazo para confirmar, comunicar impedimento ou pedir substituição; atraso não prova ausência. |
| Áreas, tarefas e responsáveis | Lista de verificação adaptada à atividade, com responsáveis e itens indispensáveis. |
| Materiais previstos | Itens, unidades, quantidades e vínculo com reserva de estoque, quando controlados. |
| Notificações | Publicação, lembrete, alteração, troca aprovada, cancelamento e resumo autorizado, sempre na central do aplicativo e com push exclusivamente móvel quando habilitado. |

Todos os números e nomes dos exemplos são ilustrativos. A casa define os horários, o esforço esperado e as tarefas permitidas. Áreas restritas e tarefas específicas não são atribuídas a qualquer participante automaticamente.

#### 9.1.3 Requisitos funcionais e regras de negócio

| ID | Requisito | Critério objetivo |
|---|---|---|
| LIMP-01 | Configurar limpeza avulsa, recorrente ou ligada a evento. | Cada ocorrência tem identificação própria e pode ser concluída/cancelada sem apagar a série. |
| LIMP-02 | Configurar equipe de X pessoas por turno. | Exibir selecionados, confirmados, pendentes e necessidade restante; contar pessoas distintas, não tarefas. |
| LIMP-03 | Configurar participação geral. | Exigir definição do público e preservar a versão da lista publicada; não convocar todo o cadastro indistintamente. |
| LIMP-04 | Combinar modalidades antes, durante ou depois de evento. | Uma pessoa pode participar de turnos distintos; nenhuma presença é copiada automaticamente entre turnos. |
| LIMP-05 | Formar equipes manualmente, por grupo ou por voluntariado. | Inscrição verifica elegibilidade, duplicidade, conflito e eventual capacidade; não ultrapassar teto em inscrições simultâneas. |
| LIMP-06 | Permitir sugestão de rodízio. | Usar disponibilidade, compatibilidade de tarefa/horário e participações verificadas; publicar somente após revisão autorizada. |
| LIMP-07 | Tratar indisponibilidade e dispensa. | Não exigir diagnóstico ou motivo íntimo; solicitação e decisão ficam restritas. Dispensa não vira falta. |
| LIMP-08 | Registrar confirmação individual. | Confirmar participação não marca presença nem executa tarefa. Ausência de resposta mantém pendência. |
| LIMP-09 | Solicitar troca de participante. | A substituição depende do aceite do substituto e da aprovação definida; preservar titular, substituto, datas e decisões. |
| LIMP-10 | Dividir áreas e tarefas. | Toda tarefa indispensável tem responsável identificado; em mutirão, dividir grupos não retira pessoas do público geral. |
| LIMP-11 | Verificar participação e execução separadamente. | Presença é registrada por operador autorizado; lista de tarefas tem estado próprio e revisão do coordenador. |
| LIMP-12 | Integrar materiais sem duplicidade. | Reserva não é consumo. Saída, retorno e perda usam o módulo de estoque com origem vinculada e proteção contra repetição. |
| LIMP-13 | Reagendar ou cancelar com rastreabilidade. | Alteração relevante gera versão e aviso; reabrir confirmação conforme política; cancelar não apaga consumo já ocorrido. |
| LIMP-14 | Controlar acesso e auditoria. | Membro não altera presença de terceiros, dispensas aprovadas nem encerramento; histórico registra alterações autorizadas. |
| LIMP-15 | Permitir operação assistida. | Secretaria/coordenador registra resposta recebida presencialmente, distinguindo operador e participante; não cria envio por WhatsApp, e-mail, SMS ou outro canal de mensagem. |
| LIMP-16 | Exibir cobertura e pendências. | Painel mostra equipe incompleta, respostas pendentes, tarefa sem responsável e material insuficiente, sem ranking público individual. |
| LIMP-17 | Não vincular participação à mensalidade automaticamente. | Limpeza, troca, falta e dispensa não geram cobrança, crédito, desconto ou restrição religiosa. |
| LIMP-18 | Permitir correção e revisão. | Participante pode solicitar correção de presença; alteração após encerramento exige autor, motivo e histórico. |
| LIMP-19 | Preservar o efeito da recorrência e dos eventos. | Editar série só altera ocorrências futuras selecionadas; mudar evento propõe atualização dos turnos vinculados. |
| LIMP-20 | Encerrar com pendências explícitas. | Tarefas indispensáveis não realizadas impedem “concluída integralmente”; cancelamento ou encerramento com pendências não afirma execução. |

**Rodízio proposto:** distribuir oportunidades sem repetir sempre as mesmas pessoas. Considerar apenas o grupo elegível para a atividade, as disponibilidades declaradas, as tarefas autorizadas e o histórico verificado. Uma participação em mutirão geral não deve distorcer automaticamente o rodízio da equipe semanal: definir escopos separados ou uma regra explícita de equivalência. Não usar mensalidades, doações, ocorrências financeiras ou posição religiosa como pontuação de seleção. Não produzir ranking de merecimento nem presumir disponibilidade por falta de resposta.

Para uma troca pendente, a cobertura continua apresentada como incerta. O aplicativo não pode tratar quem já informou impedimento como garantia de comparecimento. Se ninguém aceitar a substituição, a coordenação é avisada para refazer a escala; não se inventa substituto automático.

#### 9.1.4 Estados que devem permanecer separados

| Registro | Estados propostos |
|---|---|
| Atividade/turno | Rascunho, publicado, em execução, concluído integralmente, encerrado com pendências ou cancelado. |
| Designação | Ativa, substituída, dispensada ou cancelada. Mantém quem foi convocado em cada versão. |
| Resposta do participante | Pendente, confirmou, informou impedimento ou solicitou troca. Resposta pode ficar desatualizada após mudança de horário. |
| Participação verificada | Não verificada, participou, participou parcialmente ou não participou. Justificativa/dispensa fica em controle separado. |
| Tarefa | Pendente, em execução, informada como concluída, verificada como concluída ou não aplicável com motivo. |
| Troca | Solicitada, aguardando aceite, aguardando aprovação, aprovada, recusada ou cancelada. |

“Todos convocados” não significa “todos confirmados”. “Confirmados” não significa “presentes”. “Presentes” não significa “tarefas concluídas”. Um clique, horário de acesso, foto ou leitura de QR Code não será apresentado como prova absoluta de participação.

Na primeira versão, a verificação é manual pelo responsável identificado. O membro pode informar que terminou sua tarefa, mas isso não substitui a revisão combinada. Fotografias do local são opcionais, somente quando úteis e autorizadas, sem exigir pessoas na imagem. Não exigir localização, reconhecimento facial ou biometria como controle padrão. Restrição por área e leitura do próprio histórico são aplicadas no servidor.

#### 9.1.5 Fluxos práticos

**Limpeza semanal com equipe-alvo de quatro:** criar série → revisar ocorrências → selecionar quatro pessoas disponíveis → publicar → acompanhar respostas → resolver impedimentos → executar → registrar participação → revisar tarefas → registrar consumo real → encerrar. O painel pode mostrar “4 escalados / 3 confirmados / 1 pendente”; não deve mostrar “equipe confirmada” só porque quatro nomes foram selecionados.

**Mutirão após evento:** criar evento → criar turno de limpeza geral → selecionar o público correspondente → revisar dispensas e distribuição por áreas → publicar → acompanhar respostas → confirmar a lista aplicável no dia → executar → verificar participação e tarefas → registrar sobras/consumo → encerrar. Se a casa tiver 30 membros ativos abrangidos e quatro dispensas aprovadas, o painel distingue “30 no público original / 4 dispensados / 26 previstos”; não registra presença automática de 26 pessoas.

**Organização mista:** criar um turno de preparação com equipe de quatro e outro de encerramento com participação geral. Confirmações, presença e tarefas são independentes. Caso quatro pessoas façam ambos os turnos, aparecem uma vez em cada turno e uma vez no total de pessoas únicas do evento; número de participações por turno é um indicador separado.

#### 9.1.6 Tarefas, materiais e custos

Modelos de lista de verificação podem incluir varrer o salão, organizar cadeiras, limpar áreas autorizadas, recolher lixo, conferir os banheiros, organizar a cozinha e guardar materiais. Os responsáveis da casa revisam instruções, restrições e adequação de cada tarefa antes de adotá-las. Não são instruções de uso químico ou garantia de segurança para qualquer serviço.

Cada área pode ter um líder de apoio, participantes e quantidade-alvo própria. O sistema verifica se a mesma pessoa foi contada em duas áreas simultâneas incompatíveis; atividades sequenciais podem compartilhar pessoas quando isso for explicitamente planejado. Uma divisão em subgrupos no mutirão não cria novas pessoas nem duplica presenças.

Materiais previstos geram reserva quando o estoque for controlado. Retirada é identificada para a atividade e não pode ser repetida ao marcar tarefa concluída. Sobras não consumidas retornam por movimento vinculado; quantidade usada e perdas permanecem identificadas. Um exemplo é reservar dois frascos, retirar dois, devolver um intacto e registrar consumo de um. No cancelamento, liberar apenas reservas ainda abertas e tratar fisicamente o que já saiu; não devolver tudo ao saldo por suposição.

Equipamentos e utensílios reutilizáveis são controlados por cessão/retirada e devolução, conforme o cadastro, sem serem integralmente consumidos a cada limpeza. Falta de material pode gerar solicitação de compra, que seguirá as aprovações existentes — nunca um pagamento automático. Serviço contratado, se houver, usa fornecedor e despesa próprios; ajuda voluntária não gera conta a pagar presumida.

#### 9.1.7 Comunicações, indicadores e limites de acesso

Mensagens propostas: publicação da escala, solicitação de confirmação, lembrete, mudança de horário, troca aprovada e cancelamento. Toda mensagem é registrada na central do aplicativo do celular; o push móvel é o aviso complementar, sujeito à permissão e às condições do dispositivo. Não utilizar WhatsApp, e-mail, SMS, outros mensageiros ou push de desktop, nem em falhas ou escalonamentos. Mensagem enviada ou aberta não conta como aceite nem presença. Deduplicar o registro por destinatário e versão; controlar separadamente as tentativas de push conforme a seção 9.2.

Texto ilustrativo dentro do aplicativo autenticado: “Você está escalado para a limpeza de sábado, das 9h às 11h. Área: salão. Confirme sua disponibilidade no aplicativo.” Na tela bloqueada, usar por padrão um texto genérico, como “Você tem uma atualização no aplicativo”. Alterações de data ou tarefas devem ser claras, não escondidas em uma atualização de calendário. Cancelamento interrompe lembretes futuros daquela versão.

Coordenação: calendário, equipe-alvo, mínimo atendido, confirmados, pendentes, impedimentos, trocas, tarefas e materiais. Membro: suas próximas escalas, área, horário, responsável, instruções, resposta e histórico próprio. Consulta aos demais nomes somente no escopo autorizado da atividade; não expor telefones, justificativas privadas ou escalas completas da comunidade indiscriminadamente.

Relatórios: limpezas previstas/concluídas, cobertura por turno, tarefas pendentes, consumo por período e distribuição de participação para revisão privada da coordenação. Manter numerador e denominador claros: pessoas convocadas, elegíveis após dispensas, confirmadas e participação verificada são medidas diferentes. Não usar ausência de confirmação como contagem de falta. Não divulgar rankings individuais.

#### 9.1.8 Regras técnicas específicas

Toda designação pertence à organização, ocorrência e turno corretos. Impedir duplicidade ativa da mesma pessoa no mesmo turno; alocações em tarefas são registros subordinados, não novas designações. Aplicar autorização, limite de capacidade e validação de horário também na API.

Publicação, edição relevante, resposta, aprovação de troca e encerramento usam versões para evitar sobrescritas concorrentes. Resposta a uma escala antiga não confirma automaticamente novo horário. Geração recorrente e sincronização de evento são idempotentes por origem e ocorrência. Salvar lembretes apenas após persistir a versão correspondente e impedir avisos de turnos cancelados.

A atualização da lista geral é uma operação explícita com resumo de inclusões e exclusões. Desligamento ou afastamento provoca revisão das escalas futuras, mas nunca apaga participação passada. Cancelamento do evento apresenta as limpezas vinculadas para cancelar ou manter justificadamente; serviço de limpeza já iniciado não é convertido em “nunca aconteceu”.

O evento pode encerrar sua programação mantendo pendência de limpeza visível. “Evento encerrado operacionalmente” e “limpeza totalmente concluída” são informações diferentes. Pendências indispensáveis de conservação exigem tratamento identificado, sem obrigar a fingir que um evento já passado ainda está ocorrendo.

### 9.2 Notificações e comunicados exclusivamente no aplicativo do celular

#### 9.2.1 Regra de canal e experiência

**Decisão de produto:** toda notificação gerada pelo sistema será consultada no próprio aplicativo do celular. São dois componentes do mesmo recurso, não dois canais de mensagem independentes: a **central interna** mantém o registro e o contexto; o **push móvel** chama a atenção para a atualização. A mensagem e as ações não são transferidas para outro aplicativo.

| Meio | Regra desta versão |
|---|---|
| Central interna do aplicativo no celular | Obrigatória para todo aviso destinado ao usuário, com acesso autenticado, histórico e indicação de pendência. |
| Push no celular | Permitido nos dispositivos móveis cadastrados e compatíveis, respeitando autorização e preferências. Sem entrega garantida. |
| WhatsApp, e-mail, SMS, Telegram e outros mensageiros | Fora do escopo; não cadastrar integrações, enviar avisos ou usar como fallback. |
| Push, pop-up ou alerta sonoro de desktop | Não habilitar nem solicitar permissão no painel de computador. Consultar registros de gestão não é disparar uma notificação. |
| Serviços de transporte de push | Dependência técnica da plataforma, não um mensageiro que o usuário precisa abrir. Provedor e implementação ainda serão homologados. |

A central existe no servidor, vinculada à conta, e não apenas na memória do aparelho. Trocar de celular ou reinstalar o aplicativo não deve apagar o histórico ainda abrangido pela política de retenção. O novo aparelho precisa ser autenticado e habilitado; nenhuma instalação herda acesso apenas pelo número de telefone.

Na abertura do aplicativo, sincronizar avisos autorizados e estado atual das solicitações. Permitir filtros por não lidas, ação pendente, categoria e período. O indicador de não lidas dentro do app é independente da permissão do sistema para mostrar push. O contador no ícone do sistema operacional é opcional e depende do suporte/permissão [S15].

Ao tocar no aviso, abrir a tela interna correspondente, solicitando autenticação quando necessário e verificando acesso no servidor. O texto do push não autoriza a operação nem substitui a consulta ao estado atual.

#### 9.2.2 Matriz de eventos e destinatários

A tabela define propostas de eventos do produto, não mensagens já enviadas ou agendamentos ativos. Todo item gera registro interno para os destinatários autorizados; o push segue habilitação, relevância, preferências e prazo de validade.

| Evento | Quem recebe | Destino no aplicativo e regra |
|---|---|---|
| Mensalidade disponível, vencimento próximo ou pendência | Membro beneficiado autorizado | Abrir sua mensalidade. Revalidar saldo e revisão em andamento antes de lembrar; não expor situação financeira no push. |
| Comprovante recebido, pedido de complemento ou resultado da conferência | Membro e equipe responsável, em avisos separados | Abrir envio ou revisão. Distinguir documento recebido de dinheiro confirmado. |
| Recebimento confirmado e corretamente destinado | Membro beneficiado autorizado | Abrir recibo/destinação; somente após persistência da operação financeira. |
| Nova escala de limpeza ou pedido de confirmação | Pessoas designadas na versão publicada | Abrir Minhas escalas; não enviar ao cadastro inteiro. |
| Troca de escala, impedimento ou equipe incompleta | Solicitante, substituto e/ou coordenação, conforme a etapa | Abrir a solicitação pertinente; motivos privados não vão para todos. |
| Mudança de horário ou cancelamento de limpeza/evento | Participantes afetados pela mudança | Abrir versão atual, indicar atualização e pedir reconfirmação quando necessária. |
| Campanha, necessidade de doação ou comunicado geral | Público explicitamente selecionado e autorizado | Abrir campanha/comunicado; divulgação opcional pode ser silenciada no push. |
| Estoque mínimo, material insuficiente ou validade próxima | Responsáveis por estoque/compras | Abrir item ou solicitação. Avisar por ocorrência ou resumo, sem disparar a cada leitura de saldo. |
| Despesa aguardando aprovação, reembolso ou conta a vencer | Aprovador, solicitante ou tesouraria pertinente | Abrir operação autorizada; recebimento do aviso não é aprovação. |
| Pendência de fechamento, integração ou segurança de acesso | Pessoas com responsabilidade e permissão aplicáveis | Abrir fila interna. Não divulgar informações técnicas sensíveis a todos. |

Um usuário com mais de uma função deve receber um registro por evento/versão/finalidade, sem duplicações apenas por acumular papéis. Quando os conteúdos e escopos forem diferentes, tratá-los como avisos distintos e justificados. Revalidar a permissão e o vínculo antes da criação, do disparo e da consulta.

#### 9.2.3 Requisitos funcionais

| ID | Requisito | Critério objetivo |
|---|---|---|
| NOT-01 | Aplicar exclusividade de canal em todo o produto. | APIs, configurações e rotinas não permitem selecionar WhatsApp, e-mail, SMS, outro mensageiro ou push de desktop. Falha não muda essa regra. |
| NOT-02 | Persistir central interna por usuário e organização. | Um aviso autorizado pode ser recuperado no aplicativo mesmo sem push, enquanto estiver no prazo de retenção. |
| NOT-03 | Vincular push a instalação móvel autorizada. | Registrar e revogar endpoints/tokens por aparelho/conta; não usar lista de telefones como autorização nem habilitar inscrições de desktop no cliente suportado. |
| NOT-04 | Solicitar permissão com explicação e escolha do usuário. | Negação não bloqueia finanças, escalas ou central; exibir orientação dentro do app sem insistência abusiva. |
| NOT-05 | Mostrar somente dados pertinentes ao destinatário. | Segmentação e abertura verificam organização, vínculo e permissão no servidor; não utilizar tópicos públicos para conteúdo pessoal. |
| NOT-06 | Separar registro, transporte, abertura e ação. | Aceite do provedor não vira “lida”; abertura não confirma presença, ciência, pagamento ou aprovação. |
| NOT-07 | Publicar apenas após o evento de negócio persistido. | Falha antes da confirmação financeira não produz recibo/notificação de quitação. Retomada não duplica o registro interno. |
| NOT-08 | Deduplicar aviso e controlar tentativas. | Unicidade por evento, versão, destinatário e finalidade; tentativas por instalação com prazo e limite. Duplicações eventuais do transporte não repetem ações de negócio. |
| NOT-09 | Tratar mudanças, cancelamentos e aviso vencido. | Revalidar condição no envio; cancelar fila obsoleta; ao abrir, apresentar a versão atual sem aceitar resposta ao horário antigo. |
| NOT-10 | Respeitar horários e preferências. | Horário silencioso e categorias opcionais controlam push; a central mantém registros administrativos relevantes. Não contornar bloqueios do sistema operacional. |
| NOT-11 | Evitar excesso de avisos. | Agrupar lembretes compatíveis, limitar repetição por solicitação e suprimir lembrete resolvido ou sem utilidade atual. |
| NOT-12 | Manter pendências dentro do sistema. | Falta de resposta gera, quando configurado, aviso à coordenação no aplicativo; nunca migra para outro canal nem gera punição automática. |
| NOT-13 | Exigir ação explícita para ciência ou confirmação. | Botão “Estou ciente” tem registro próprio e versão; confirmar escala/autorizar gasto exige comando de negócio separado. |
| NOT-14 | Proteger conteúdo de tela bloqueada. | Push padrão genérico, sem valores, nomes de terceiros, comprovantes, filiação ou justificativas; detalhe somente após autenticação. |
| NOT-15 | Tratar troca de aparelho, saída e perda de acesso. | Revogar envios futuros ao endpoint antigo, limpar dados locais conforme a capacidade do cliente e negar consultas sem autorização. |
| NOT-16 | Centralizar comunicados administrativos. | Autor autorizado escolhe público, revisa prévia e publica; edição relevante preserva versão e informa atualização. |
| NOT-17 | Receber respostas no aplicativo. | Pedidos de informação, impedimentos e esclarecimentos ficam vinculados ao registro, com autor e permissão; não abrir WhatsApp nem criar chat irrestrito por consequência. |
| NOT-18 | Disponibilizar diagnóstico sem vigilância excessiva. | Mostrar falha técnica, push não habilitado ou leitura não registrada conforme evidência; não inventar entrega nem divulgar diagnósticos entre membros. |
| NOT-19 | Prever ativação e recuperação sem mensagens externas. | Não depender de link/código enviado por e-mail ou SMS. Projetar passkey/códigos de recuperação ou atendimento presencial com verificação e auditoria; notificações não substituem autenticação. |
| NOT-20 | Homologar a experiência móvel antes do lançamento. | Testar aparelhos suportados, permissão negada, app em segundo plano, perda de internet, avisos atrasados, reinstalação e consulta do histórico. |

#### 9.2.4 Estados e significado do histórico

| Controle | Estados ou registros propostos | Limite da interpretação |
|---|---|---|
| Aviso na central | Ativo, atualizado/substituído, expirado ou arquivado pelo usuário | Arquivar não encerra tarefa nem apaga auditoria necessária. |
| Push por aparelho | Pendente, aceito pelo provedor, falha temporária, token inválido, expirado ou suprimido | Aceito pelo provedor não comprova exibição na tela [S14]. |
| Abertura registrada | Primeira abertura no app e marcação manual de lida separadas | Registro do cliente não demonstra compreensão; marcar em lote não equivale a abrir cada mensagem. |
| Ciência explícita | Usuário, comunicado, versão e instante | Não substitui aceite de escala, presença ou aprovação financeira. |
| Resposta de negócio | Confirmação de escala, pedido de troca, complemento ou aprovação | Usa o módulo de origem e suas permissões; repetição da requisição não duplica efeito. |

O painel pode informar “sem abertura registrada” ou “aguardando resposta”; não pode afirmar “não viu”, “ignorou” ou “recusou” a partir da falta de telemetria. Ler em um aparelho deve sincronizar o estado da central nos demais na próxima conexão, sem prometer remover imediatamente todos os avisos já exibidos pelo sistema operacional.

#### 9.2.5 Falhas, funcionamento móvel e dependências técnicas

Push não é garantia de entrega imediata. A documentação do FCM distingue aceitação para entrega de entrega efetiva e descreve atraso, indisponibilidade do aparelho e expiração [S14]. No Android, as notificações comuns de aplicativos nativos estão sujeitas à permissão do usuário a partir do Android 13 [S16]. Para a opção PWA, o WebKit documenta Web Push em apps adicionados à tela inicial no iOS/iPadOS desde 16.4, com solicitação de permissão a partir de uma interação do usuário [S15]. São requisitos de compatibilidade a testar nos dispositivos adotados, não uma homologação já executada.

“Tudo no próprio sistema” não significa funcionamento integral sem internet nem infraestrutura de push autônoma. O Web Push no iOS usa APNs [S15]; outras implementações usam serviços de transporte compatíveis com sua plataforma, como FCM. A lógica, os destinatários, o histórico e as respostas permanecem no aplicativo e no servidor da solução. Não exigir instalação de mensageiro nem conta pessoal em serviço de comunicação para ler os avisos.

Sem permissão, internet ou entrega do push, manter a mensagem na central e recuperá-la ao autenticar e sincronizar. Avisos obsoletos permanecem corretamente identificados como históricos, e não como convocações atuais. Não reenviar notificações indefinidamente, nem tentar impor som ou ignorar o modo silencioso do telefone.

Depois de um push ser aceito pelo provedor, não presumir cancelamento garantido de sua entrega. Usar validade, agrupamento/substituição quando suportados e texto genérico; ao abrir, consultar a informação atual. Logout e revogação bloqueiam envios futuros, mas um aviso já em trânsito pode ainda aparecer sem revelar detalhes.

A política controla os destinos cadastrados pelo aplicativo, não todo espelhamento feito pelo próprio aparelho. O WebKit informa que notificações de apps web podem aparecer em Apple Watch pareado [S15]. Não integrar envio direto a relógios ou computadores; não prometer impedir todo espelhamento definido pelo usuário no sistema operacional.

O uso assistido presencial continua disponível para quem não tem celular compatível: um operador identificado registra a interação administrativa. Isso não será descrito como notificação digital entregue e não autoriza canal externo. Decisões de ativação e recuperação devem permitir atendimento sem depender do push no aparelho perdido.

#### 9.2.6 Fluxo de referência

**Publicar escala → salvar versão e evento → criar avisos internos para designados → tentar push nos celulares habilitados → membro abre o aplicativo → autenticar e consultar versão atual → confirmar ou informar impedimento → atualizar escala → avisar a coordenação pelo próprio aplicativo quando necessário.**

Ao cancelar o turno, invalidar as ações e lembretes pendentes daquela versão e registrar o cancelamento para os afetados. O registro original continua identificado no histórico autorizado; nenhuma etapa usa WhatsApp, e-mail, SMS ou desktop.

## 10. Telas e ações do aplicativo

### 10.1 Área do membro

| Tela | Conteúdo | Ações |
|---|---|---|
| Início | Situação individual, próximas contribuições, próximas escalas e avisos autorizados. | Abrir mensalidade, pagar, enviar comprovante, consultar escala e comunicação. |
| Minhas mensalidades | Competência, valor, vencimento, ajustes, valor quitado e saldo. | Selecionar mês/meses e consultar histórico próprio. |
| Pagar | Total calculado, favorecido, referência e QR Code/copia e cola. | Iniciar pagamento e atualizar situação; sem botão de autoquitação. |
| Enviar comprovante | Arquivo, competências pretendidas, valor/data informados, pagador quando necessário. | Enviar, acompanhar análise e complementar informação. |
| Meus recibos | Recebimentos e destinações autorizadas. | Consultar recibo e correções, sem expor outros beneficiados no mesmo pagamento. |
| Doações | Campanhas e necessidades aprovadas. | Declarar intenção, pagar ou combinar entrega de material. |
| Solicitações | Pedidos de esclarecimento ou revisão. | Responder e acompanhar resultado dentro do aplicativo, sem encaminhamento a mensageiros. |
| Notificações | Histórico individual por categoria, não lidas, versões e ações pendentes. | Abrir contexto, marcar como lida, arquivar, registrar ciência quando exigida e responder na tela correspondente. |
| Avisos da casa | Comunicados autorizados para o público do membro. | Consultar versão vigente e responder somente quando essa opção estiver habilitada. |
| Meu celular e alertas | Aparelhos cadastrados, permissão informada, preferências e horários. | Habilitar push, testar aviso genérico e revogar aparelho antigo, respeitando os controles do sistema operacional. |
| Minhas escalas | Limpeza, turno, área, responsável, resposta e alteração pendente. | Confirmar, informar impedimento, solicitar troca e consultar o próprio histórico. |
| Atividade de limpeza | Instruções, tarefas, materiais e dados autorizados da equipe. | Informar conclusão de tarefa atribuída e pedir correção de participação; sem autovalidação de presença. |

A experiência deve funcionar com fonte legível, botões claros, leitor de tela e mensagens compreensíveis. Quando alguém sem acesso digital for atendido pela secretaria, registrar o operador e o beneficiário separadamente.

### 10.2 Área da tesouraria

Painel com créditos a identificar, comprovantes pendentes, divergências, contas a vencer, caixa e conciliação. Na tela de conferência, apresentar a prévia do arquivo ao lado dos dados informados, extraídos e bancários, sempre rotulados pela origem.

Ações: consultar transação, selecionar correspondência, distribuir valor, solicitar informação, encaminhar revisão e confirmar recebimento/alocação quando os requisitos forem atendidos. Não oferecer “aprovar e ignorar banco” como atalho escondido.

### 10.3 Área da gestão

Consolidados de arrecadação, despesas, saldo livre, fundos vinculados, compromissos e estoque crítico. Filas de aprovação com justificativas e impacto financeiro. Prestação de contas para membros deve ser uma versão sanitizada e separada dos relatórios internos.

### 10.4 Coordenação de limpeza

Calendário de ocorrências, formação de equipes de X pessoas, definição de público geral, turnos antes/depois de eventos, fila de respostas e trocas, revisão de presença, lista de tarefas e materiais. Destacar “selecionados”, “confirmados” e “participação verificada” em campos separados. A tela de publicação mostra o público efetivamente selecionado e alertas de conflito antes de comunicar a escala.

Exemplo de cartão do membro: **Limpeza do centro — sábado, 9h–11h; modalidade: equipe de 4; área: salão; responsável: coordenador da atividade; sua resposta: pendente.** Ações: “Confirmar”, “Informar impedimento” e “Solicitar troca”. O cartão não promete que uma solicitação de troca já foi aprovada.

### 10.5 Operação e configurações

Comunicações: criação e revisão de comunicados, público selecionado, histórico de publicação, pendências de resposta e diagnóstico de push conforme permissão. O painel de computador não solicita inscrição de push nem gera alertas de desktop; avisos aos operadores também são dirigidos ao aplicativo do celular.

Telas de estoque, compras, patrimônio, eventos, limpeza, escalas, usuários, políticas com vigência, contas recebedoras autorizadas, integrações, auditoria e fechamento. Exibir saúde da integração e data da última conferência para não dar aparência de saldo atualizado quando houver atraso.

## 11. Modelo de dados proposto

| Entidade | Campos e relacionamentos essenciais |
|---|---|
| Organizacao | Nome, configurações, moeda, fuso e políticas vigentes. |
| Pessoa / Vinculo | Identidade administrativa, contatos necessários, vínculo com a casa, início/fim e situação. |
| Usuario / Permissao | Identidade de acesso separada da pessoa, organização, papéis e permissões com vigência. |
| RegraContribuicao | Valor, público/vínculo aplicável, vencimento e início/fim de vigência. |
| Mensalidade | Vínculo, competência, valor original, ajustes e versão de concorrência. |
| IntencaoPagamento | Organização, destino pretendido, total calculado, conta e chave de idempotência. |
| CobrancaProvedor | Intenção, provedor, conta, identificador externo/txid, ambiente e estado. |
| EnvioComprovante | Autor, beneficiário autorizado, informações declaradas, estado e solicitações relacionadas. |
| ArquivoEvidencia | Objeto privado, original/derivada, hash, tipo, tamanho, resultado técnico e prazo de retenção. |
| ExtracaoDocumento | Arquivo, versão do extrator, campos, confiança por campo e correções auditadas. |
| TransacaoFinanceira | Conta, provedor/origem, identificadores canônicos, valor, direção, data e evidência da fonte. |
| Recebimento | Transação financeira ou recebimento de caixa, confirmação, disponibilidade e referências. |
| AlocacaoRecebimento | Recebimento, mensalidade/doação/crédito, valor e reversões. |
| Devolucao | Recebimento original, valor, autorização, identificador externo e confirmação. |
| CasoRevisao | Motivos, evidências, responsável, mensagens públicas/privadas e resultado. |
| LoteConciliacao | Conta, período, origem, itens, diferenças e revisão. |
| SessaoCaixa | Operador, abertura, movimentos, fechamento, contagem e revisor. |
| LancamentoFinanceiro | Evento de origem, conta, categoria/fundo, valor, tipo e reversão vinculada. |
| Campanha / Fundo | Finalidade, regras de uso, metas e vinculação de entradas/despesas. |
| Doacao | Tipo, promessa/recebimento, identificação necessária e itens/finalidade. |
| Despesa / Compra | Solicitante, fornecedor, documentos, aprovação, recebimento e pagamentos vinculados. |
| MovimentoEstoque | Item, unidade, lote/local quando aplicável, tipo, quantidade, origem e responsável. |
| Patrimonio | Bem, titularidade, origem, localização, responsável e histórico. |
| Evento | Título, datas, local, responsáveis, lista administrativa de participantes quando existente e estado operacional. |
| RegraLimpeza | Origem, recorrência, público, modalidade, horários, quantidades, seleção e vigência. |
| AtividadeLimpeza / TurnoLimpeza | Organização, regra/evento de origem, data, intervalo, responsável, modalidade, limites, estado e versão. |
| PublicacaoEscala | Turno, versão, regra do público, seleção registrada, data e autor da publicação. |
| EquipeOperacional / MembroEquipe | Grupo reutilizável, pessoas e vigência; sua composição não substitui o histórico de cada ocorrência. |
| DesignacaoLimpeza | Pessoa, turno, publicação de origem, forma de seleção, estado e substituição vinculada. |
| RespostaEscala | Designação, versão respondida, resposta, data, operador e origem da informação. |
| ParticipacaoLimpeza | Designação, resultado observado, verificador, data, pedido de correção e histórico. |
| Indisponibilidade / DispensaEscala | Pessoa, período/turno, solicitação, decisão, responsável e observação mínima restrita. |
| SolicitacaoTroca | Designação original, substituto, aceite, aprovação, estado e versão. |
| AreaLimpeza / TarefaLimpeza | Turno, local, descrição, responsáveis, restrições, caráter indispensável, execução e revisão. |
| MaterialAtividade | Atividade/turno, item, unidade e vínculos com reserva, retirada, consumo, perda e devolução, sem duplicar saldo. |
| Aprovacao | Operação e versão aprovadas, autor, motivo, instante e exigência de independência. |
| Fechamento | Período, saldos, pendências, revisor e versões dos relatórios. |
| EventoAuditoria | Autor, organização, ação, recurso, alterações mínimas necessárias, motivo e correlação. |
| EventoIntegracao | Provedor, evento externo, conteúdo protegido necessário, estado e tentativas. |
| Comunicado / VersaoComunicado | Organização, autor, conteúdo privado, público, publicação, atualização, ciência exigida e validade. |
| Notificacao | Evento/versão, destinatário, organização, categoria, referência de origem, conteúdo interno, prioridade e validade; unicidade por evento/versão/destinatário/finalidade. |
| EstadoNotificacaoUsuario | Notificação, abertura observada, marcação manual de lida, arquivo pessoal e versão; separado da ação de negócio. |
| CienciaComunicado | Comunicado/versão, usuário, instante e operação explícita; não equivale à presença ou aprovação. |
| InstalacaoMovelPush | Organização, usuário, instalação, plataforma, endpoint/token protegido, permissão informada, atualização e revogação. |
| PreferenciaNotificacao | Usuário, categorias de push, horário silencioso, fuso e alterações; não habilita canais externos. |
| SaidaNotificacao | Evento persistido, destinatário, finalidade, estado e deduplicação; destinos permitidos limitados à central interna e push móvel. |
| TentativaPush | Notificação, instalação, tentativa, identificador do provedor, resultado observado, validade e próxima tentativa. Aceite não é leitura. |

**Não fundir** Mensalidade, EnvioComprovante, TransacaoFinanceira e AlocacaoRecebimento numa tabela única de “pagamentos”. A distinção é necessária para pagamentos agrupados, reenvios e conciliação sem dupla receita.

**Não fundir** DesignacaoLimpeza, RespostaEscala, ParticipacaoLimpeza e TarefaLimpeza num único campo “fez a limpeza”. Uma publicação pode ser atualizada, uma pessoa pode pedir troca e uma tarefa pode ficar pendente mesmo com presença registrada.

Hash de arquivo não é identificador financeiro. Txid de cobrança também não substitui, em todos os casos, a identificação do recebimento efetivo. A identificação deve ser reconciliada conforme os dados reais do provedor.

## 12. Arquitetura proposta

Proposta, ainda não decisão final: API em C#/.NET, banco relacional PostgreSQL, aplicativo móvel inicialmente considerado como PWA, painel administrativo responsivo, armazenamento privado de arquivos e processo de trabalho para análise documental e integrações. PWA permite uma experiência instalável com tecnologias web; suporte varia por plataforma/navegador [S13]. Como notificações móveis são requisito da primeira versão, testar instalação, permissão e push nos aparelhos da casa antes de fechar a escolha PWA versus aplicativo nativo/multiplataforma. O projeto não presume que abrir um site em qualquer navegador seja suficiente para receber push [S15][S16].

Começar como uma aplicação modular, com responsabilidades separadas, sem exigir microserviços. Módulos: identidade, membros, contribuições, pagamentos, evidências, tesouraria, doações, estoque/compras, patrimônio/eventos, limpeza/escalas, comunicação/notificações móveis e auditoria.

O aplicativo cliente não decide confirmação financeira nem guarda segredo bancário. O processo de análise de arquivos não tem permissão para quitar obrigações. O processo de pagamento não precisa acessar anotações pessoais de membros.

Usar registro durável de eventos recebidos e de notificações a criar/enviar. Persistir o evento e processá-lo de forma retomável: criar primeiro o aviso na central e depois executar tentativas de push por instalação móvel. Uma mensagem só sai como “pago” depois de a confirmação e a alocação estarem persistidas. Se o aplicativo cair antes da notificação, o reenvio posterior não deve duplicar o recebimento nem o aviso interno. Não prometer entrega única ou imediata pelo provedor; revalidar contexto, validade e acesso, com consumidores idempotentes. Não implementar adaptadores para WhatsApp, e-mail, SMS ou push desktop neste escopo.

### Contratos de operação a detalhar na implementação

- Criar intenção de pagamento a partir de obrigações autorizadas, sem aceitar total arbitrário do cliente.
- Receber envio de comprovante e consultar processamento por identificador autorizado.
- Receber evento do provedor; consultar transação e processar de forma idempotente.
- Solicitar/confirmar conciliação com dados da fonte e versão esperada do registro.
- Distribuir recebimento entre destinos autorizados, com verificação de saldo concorrente.
- Solicitar/aprovar devolução e confirmar sua execução.
- Solicitar/aprovar despesa ou ajuste e invalidar aprovação após mudança relevante.
- Fechar/reabrir período com autorização, motivo e versão.
- Criar e publicar ocorrência/turno de limpeza, com quantidade-alvo ou público geral explícito.
- Responder à versão publicada, solicitar/aceitar/aprovar troca e revisar dispensas autorizadas.
- Registrar participação e conferir tarefas com permissões separadas da confirmação do participante.
- Vincular reserva, retirada e devolução de material; encerrar limpeza sem repetir movimentação.
- Revisar recorrência/evento e cancelar ocorrências com tratamento de lembretes, respostas e estoque.
- Registrar/revogar instalação móvel de push vinculada ao usuário autenticado, sem habilitar desktop.
- Publicar comunicado para público autorizado; listar/abrir avisos próprios e registrar leitura ou ciência de forma distinta.
- Gerar central interna por evento e enviar push com deduplicação, validade, cancelamento e tentativas limitadas.
- Atualizar preferências móveis e processar pendências de resposta sem fallback para outro canal.

Evitar uma operação genérica “alterar status para pago”. Os comandos devem representar ações de negócio e aplicar as mesmas regras em todos os canais.

## 13. Segurança, privacidade e auditoria

**SEG-01:** acessos individuais; autenticação reforçada para perfis financeiros; revogar sessões e revisar permissões periodicamente. Recuperação de acesso de tesoureiro exige procedimento documentado.

**SEG-02:** toda consulta e download verifica a organização e o titular/escopo autorizado. Um identificador difícil de adivinhar não substitui permissão. Testar acesso cruzado entre membros e, se houver expansão, entre casas [S6].

**SEG-03:** registrar aprovações, mudanças de favorecido, ajustes, reversões, acessos a evidências restritas, exportações e alterações de permissão. Não registrar senhas, tokens, comprovantes completos ou dados pessoais desnecessários em logs gerais.

**SEG-04:** manter auditoria com detecção de alterações e cópia separada de acesso restrito. Uma tabela editável pelo mesmo administrador não basta como evidência independente. A OWASP recomenda proteção e detecção de adulteração dos logs [S8]. Nenhum desses controles torna impossível toda fraude com comprometimento total de infraestrutura ou conluio.

**SEG-05:** backups protegidos, retenção definida e testes de restauração incluindo banco, arquivos e segredos necessários. Documentar perda máxima de dados tolerada e prazo de recuperação como decisões operacionais, não como garantias já cumpridas.

**PRIV-01:** vínculo religioso é dado pessoal sensível na LGPD. Mapear finalidade, base legal adequada, acesso e retenção; consentimento genérico não resolve todos os tratamentos [S9].

**PRIV-02:** não coletar CPF completo, biometria, localização ou documentos extras como antifraude padrão. Dados exigidos por banco/provedor devem ser analisados para finalidade específica. Não copiar informações religiosas para descrições de pagamento quando uma referência interna neutra bastar.

**PRIV-03:** nenhuma lista pública de inadimplentes ou suspeitos. Notificação em tela bloqueada usa por padrão conteúdo genérico, sem comprovantes, valores ou motivos privados; conteúdo completo depende de autenticação e autorização no aplicativo. Não prometer esconder nome/ícone do aplicativo ou todo espelhamento do dispositivo. Extratos de terceiros não são exigidos para provar crédito na conta da casa.

**PRIV-04:** arquivos ficam sujeitos a prazos de guarda e eliminação definidos, preservações legais aplicáveis e atendimento de direitos. “Histórico auditável” não significa guardar todos os dados pessoais para sempre.

**PRIV-05:** avaliar operadores externos de armazenamento, leitura de documentos e pagamentos. Não enviar comprovantes identificados a serviços públicos de análise por conveniência.

**PRIV-06:** escalas, participação e justificativas seguem acesso restrito por atividade e finalidade. O módulo não usa situação financeira para distribuir limpeza e não publica justificativas pessoais. Fotos do local são opcionais e privadas quando adotadas; não exigir localização ou biometria como padrão.

**PRIV-07:** endpoints/tokens de push e histórico de leitura têm acesso e retenção definidos. Não incluir dados pessoais desnecessários no transporte ou nos logs. Não tratar telemetria como prova de ciência ou permitir que todos consultem quem leu. Troca de conta revoga a associação anterior da instalação; acesso ao conteúdo é sempre validado no servidor.

## 14. Testes de aceitação obrigatórios

Usar dados sintéticos e amostras autorizadas. Os testes de integração devem distinguir sandbox e produção.

| Teste | Cenário | Resultado esperado |
|---|---|---|
| T01 | Imagem declara R$ 100; consulta confirma R$ 10. | Registrar somente os R$ 10 reais e encaminhar diferença; não quitar R$ 100. |
| T02 | Comprovante de agendamento sem crédito. | Manter aguardando confirmação, sem baixa. |
| T03 | Mesmo arquivo enviado novamente. | Relacionar evidência; não criar segunda entrada. |
| T04 | Arquivo visualmente diferente referencia transação já destinada. | Manter um único recebimento e impedir excesso de alocação. |
| T05 | Pagamento é feito pelo cônjuge ou responsável autorizado. | Permitir quitação do membro correto sem concluir fraude pelo nome diferente. |
| T06 | R$ 150 pagam três mensalidades de R$ 50. | Três alocações; uma entrada; saldo disponível zero. |
| T07 | Duas pessoas têm pagamentos de mesmo valor e data. | Não escolher beneficiário sem evidência suficiente. |
| T08 | Evento de webhook é recebido cinco vezes. | Uma transação/entrada, sem duplicar recibos e notificações de quitação. |
| T09 | Dois tesoureiros aprovam simultaneamente. | Uma confirmação; segunda operação recebe resultado consistente/conflito tratado. |
| T10 | Banco está indisponível. | Pendência técnica e recuperação; não marcar como pago nem fraude. |
| T11 | Pagamento ocorre sem webhook. | Rotina de recuperação/consulta identifica e processa o crédito. |
| T12 | Devolução chega antes de evento atrasado de recebimento. | Consulta do estado financeiro e processamento sem ressuscitar quitação indevida. |
| T13 | Pagamento confirma após expiração local. | Registrar dinheiro real e encaminhar destinação, sem descartá-lo. |
| T14 | Membro tenta baixar arquivo de outro membro. | Acesso negado e registro de segurança apropriado. |
| T15 | Usuário modifica valor ou status na requisição. | Servidor ignora/rejeita alteração não autorizada; total vem das regras do servidor. |
| T16 | Arquivo proibido, corrompido ou análise indisponível. | Quarentena/reenvio seguro; nenhum conteúdo é executado. |
| T17 | Leitura automática erra dígito ou não encontra identificador. | Revisão humana; ausência não vira zero nem veredito de fraude. |
| T18 | PDF assinado por pessoa diferente do banco. | Não confundir assinatura válida com emissão bancária ou crédito. |
| T19 | Tesoureiro solicita o próprio reembolso. | Exigir aprovador independente. |
| T20 | Conta/favorecido muda depois da aprovação. | Invalidar aprovação e exigir nova revisão. |
| T21 | Importação de extrato sobrepõe dados obtidos por API. | Relacionar as mesmas transações, sem duplicar receita. |
| T22 | Doação material é recebida. | Aumentar estoque/patrimônio, sem alterar caixa. |
| T23 | Compra já paga é recebida no estoque. | Registrar materiais sem repetir saída financeira. |
| T24 | Duas retiradas concorrentes excedem o estoque. | Bloquear a operação excedente e preservar saldo consistente. |
| T25 | Depósito bancário transfere caixa já contabilizado. | Transferência interna, sem nova arrecadação. |
| T26 | Alteração em período fechado. | Bloqueio ou reabertura autorizada e rastreável. |
| T27 | Restauração de backup. | Recuperar vínculos entre recebimentos, mensalidades, documentos e auditoria conforme o procedimento testado. |
| T28 | Processo cai depois de salvar recebimento e antes de avisar membro. | Retomar notificação sem repetir recebimento. |
| T29 | Usuário é desligado ou perde acesso financeiro. | Revogar ações novas e sessões conforme política; preservar histórico autorizado. |
| T30 | Documento é contestado, mas há crédito bancário verdadeiro e alocação inequívoca. | Não apagar/ignorar dinheiro real; separar quitação financeira da revisão documental. |
| T31 | Equipe-alvo de 4 com 4 nomes selecionados e somente 3 confirmações. | Mostrar 4 selecionados, 3 confirmados e 1 pendente; não afirmar equipe inteiramente confirmada. |
| T32 | Duas inscrições simultâneas disputam a última vaga de turno com teto de 4. | Uma ocupa a vaga; a outra recebe indisponibilidade ou lista de espera configurada; nunca 5 vagas confirmadas. |
| T33 | Mutirão para 30 membros abrangidos, com 4 dispensas aprovadas. | Manter público original 30, dispensas 4 e previsão 26; não incluir fornecedores/visitantes nem presumir presença. |
| T34 | Novo membro entra depois de publicada escala geral. | Sugerir revisão explícita; registrar inclusão e aviso se aprovada, sem alterar silenciosamente a publicação anterior. |
| T35 | Troca é solicitada, mas o substituto não aceitou. | Manter troca pendente e cobertura incerta; não confirmar presença de ninguém nem ocultar impedimento. |
| T36 | Substituto aceita e coordenador aprova. | Substituir designação ativa com histórico; computar somente um ocupante da vaga. |
| T37 | Membro confirma que irá, mas a atividade não ocorreu. | Resposta confirmada; presença não verificada; nenhuma tarefa ou consumo automático. |
| T38 | Membro informa tarefa concluída e tenta validar a própria presença via API. | Permitir informar tarefa autorizada; negar autovalidação de presença sem permissão própria. |
| T39 | Mesma pessoa já está em turno incompatível no mesmo horário. | Sinalizar e impedir designação conflitante por padrão; exceção autorizada exige motivo e capacidade real de execução. |
| T40 | Evento muda de data depois de confirmações de limpeza. | Revisar turnos vinculados, notificar alterações e exigir reconfirmação quando aplicável; preservar respostas antigas. |
| T41 | Evento é cancelado após reserva de materiais e antes da limpeza. | Revisar/cancelar atividades vinculadas, interromper lembretes e liberar reservas abertas; não gerar faltas. |
| T42 | Tarefa é concluída duas vezes por repetição de requisição. | Um resultado consistente; nenhuma saída duplicada de material. |
| T43 | Foram retirados dois frascos e devolvido um intacto. | Registrar movimentos vinculados e consumo de um, sem devolver material já consumido. |
| T44 | Limpeza é cancelada após parte do trabalho e do consumo. | Preservar trabalho e consumo reais; liberar somente reservas remanescentes e registrar retornos efetivos. |
| T45 | Série recorrente é alterada após ocorrências concluídas. | Aplicar mudança somente às ocorrências futuras selecionadas; não reescrever histórico. |
| T46 | Membro sem aplicativo confirma presencialmente pela secretaria. | Registrar participante e operador distintos, origem presencial e data; sem compartilhar senha, presumir notificação digital entregue ou criar canal externo. |
| T47 | Mutirão tem subgrupos e uma pessoa participa de duas tarefas. | Contar uma pessoa única no turno, mantendo duas atribuições sem presença duplicada. |
| T48 | Coordenador encerra atividade com tarefa indispensável não concluída. | Bloquear conclusão integral; permitir pendência/encerramento identificado conforme regra aprovada. |
| T49 | Falta de confirmação ou impedimento é registrado. | Não gerar multa, desconto, dívida, bloqueio religioso ou ranking público. |
| T50 | Dois operadores alteram uma escala ou troca simultaneamente. | Detectar conflito de versão e preservar resultado consistente; resposta antiga não confirma horário novo. |
| T51 | Ocorrência e aviso são reprocessados após falha técnica. | Não duplicar atividade, designação nem aviso da mesma versão; cancelamento prevalece sobre lembrete pendente. |
| T52 | Membro consulta justificativa privada de outro participante ou outra casa. | Negar acesso; limitar dados ao escopo autorizado, preservando histórico próprio e canal de revisão. |
| T53 | Rotina tenta selecionar WhatsApp, e-mail, SMS ou push de desktop. | Rejeitar canal fora do escopo; gerar somente central interna e push móvel permitido. |
| T54 | Membro nega ou revoga permissão de push. | Preservar acesso ao aplicativo e avisos internos; orientar sem forçar permissão ou mudar de canal. |
| T55 | Provedor aceita push, mas não há confirmação de exibição ou abertura. | Registrar somente o estado comprovado; não marcar lida, ciência ou resposta. |
| T56 | Celular fica offline até depois do horário da limpeza. | Ao sincronizar, mostrar histórico e situação atual; não enviar lembrete inútil nem registrar falta por ausência de leitura. |
| T57 | Processo falha e reexecuta o mesmo evento três vezes. | Um aviso interno por destinatário/finalidade/versão; tentativas rastreáveis, sem repetir quitação ou confirmação. |
| T58 | Turno é cancelado com push ainda na fila ou já aceito pelo provedor. | Suprimir fila local obsoleta; não prometer recolher push em trânsito; abertura consulta cancelamento e bloqueia ação antiga. |
| T59 | Membro abre aviso, marca como lido ou marca todos como lidos. | Atualizar somente leitura; não confirmar presença, ciência explícita, pagamento ou despesa. |
| T60 | Membro confirma escala por ação explícita e repete a requisição. | Registrar uma resposta autorizada à versão vigente, sem duplicidade nem presença automática. |
| T61 | Usuário sai da conta, perde acesso ou troca de celular. | Revogar envios futuros ao aparelho desvinculado, negar conteúdo sem autorização e preservar histórico na conta correta. |
| T62 | Duas contas usam o mesmo aparelho em momentos diferentes. | Não encaminhar novos avisos da conta anterior à instalação da conta atual; push pendente não revela dados privados. |
| T63 | Membro tenta abrir ID de aviso de outro membro ou de outra organização. | Negar no servidor, inclusive após toque em link interno; não confiar no destinatário enviado pelo cliente. |
| T64 | Tesoureiro também tem perfil de dirigente e aparece duas vezes no público. | Um aviso por evento/versão/finalidade; somente conteúdos distintos justificados geram registros separados. |
| T65 | Mensalidade é quitada ou comprovante entra em revisão antes do lembrete programado. | Revalidar saldo e suspensão de lembrete antes de enviar; não cobrar automaticamente o que já foi resolvido. |
| T66 | Alteração exige nova confirmação de escala ou ciência de comunicado. | Preservar aceite anterior ligado à versão antiga; mostrar atualização e pedir ação explícita da nova versão. |
| T67 | Categoria opcional está desativada ou vigora horário silencioso. | Suprimir/adiar push conforme configuração; manter histórico interno aplicável e respeitar controles do telefone. |
| T68 | Aparelho/PWA não suporta ou não concluiu a configuração de push. | Informar limitação real e disponibilizar central; não simular envio nem ativar canal externo. |
| T69 | Usuário perde o celular e precisa recuperar acesso sem e-mail/SMS. | Usar mecanismo de recuperação aprovado ou atendimento presencial auditado; não depender do push no aparelho perdido. |
| T70 | Não há resposta até o prazo e o provedor permanece indisponível. | Exibir pendência e avisar responsável dentro do sistema quando cabível; nenhuma chamada a mensageiro externo nem punição automática. |

## 15. Implantação por etapas

### Etapa A — base e controles indispensáveis

Cadastro e permissões; mensalidades e ajustes; intenção de pagamento; arquivos seguros; fluxo manual de conferência real; recebimentos e alocações; caixa e despesas; doações; estoque básico; trilha de auditoria; conciliação e fechamento. Incluir desde aqui revisão de divergências e prevenção de duplicidade. Para limpeza: equipes manuais de X pessoas, mutirão com público explícito, turnos ligados ao evento, resposta, troca simples com aprovação, registro de participação, tarefas e avisos no aplicativo. Incluir a central persistida, comunicados básicos, push móvel homologado, tratamento de permissão negada, privacidade e recuperação de acesso sem canais externos já nesta etapa. Não adiar permissões ou histórico da escala.

Critério de saída: realizar um fechamento de teste reconciliável, com casos de pagamento parcial, agrupado e por terceiro, e nenhum pagamento confirmado apenas por upload. Executar também uma limpeza de equipe e um mutirão de teste, distinguindo convocação, resposta, presença e tarefas, com cancelamento e substituição rastreáveis. Para notificações, executar fluxo ponta a ponta em celulares suportados, sem depender de WhatsApp, e-mail ou SMS, e validar acesso à central sem push.

### Etapa B — integração bancária homologada

Selecionar o banco/provedor da conta autorizada, validar acesso e custos, implementar cobrança identificada, autenticidade do webhook, consulta, recuperação, devoluções, idempotência e conciliação cruzada. Rodar testes de falha e eventos fora de ordem. Uma verificação real controlada exige autorização da casa e operação pelo responsável — não se presume realizada neste documento.

Se o provedor estiver disponível desde o início, incluir essa integração no primeiro lançamento operacional. Sem ela, lançar somente o fluxo manual honesto, sem selo de automação bancária.

### Etapa C — gestão completa

Aprovações avançadas, compras/fornecedores, campanhas/fundos, adiantamentos, patrimônio, gestão completa de eventos, inventário, orçamento e prestação de contas segmentada. Ampliar limpeza com modelos recorrentes, equipes reutilizáveis, divisão avançada por áreas e integração detalhada de materiais.

### Etapa D — automações adicionais

Aperfeiçoamento dos lembretes e agrupamentos exclusivamente no aplicativo do celular, extração documental e alertas de divergência com métricas. Para limpeza, sugestão de rodízio revisada pela coordenação, sincronização assistida com listas do evento e relatórios de distribuição de participação. Ampliar a experiência móvel conforme os resultados de uso, sem adiar a homologação inicial das notificações; PWA ou aplicativo nativo/multiplataforma deve cumprir o requisito móvel aprovado. Não condicionar segurança essencial à existência de IA.

## 16. Critérios de liberação para uso real

Antes do lançamento: aprovar regras da casa e titularidade da conta; configurar permissões e recuperação de acesso; validar os testes críticos de dinheiro e documentos; executar restauração; verificar que membros não acessam dados alheios; distinguir homologação/produção; disponibilizar aviso de privacidade e canal de revisão; treinar tesouraria; documentar contingência e fechamento. Para limpeza, aprovar o significado de “todos”, responsáveis, regras de confirmação/troca/dispensa e testar a diferença entre participação declarada e verificada, cancelamento, capacidade e privacidade. Para comunicação, homologar aparelhos/instalação/permissão, central sem push, tentativas limitadas, revogação de dispositivos, abertura autenticada e ausência de fallback externo. Definir atendimento presencial e recuperação para quem perde o aparelho.

A importação inicial deve estabelecer data de corte, saldos de abertura, obrigações pendentes e fundos vinculados. Registros históricos sem prova suficiente ficam identificados como pendentes ou saldo inicial aprovado, nunca como transações bancárias automaticamente verificadas.

## 17. Decisões pendentes que não impedem este desenho

Banco e tipo de conta; titular recebedor autorizado; elegibilidade e custos da API; necessidade real de publicação em lojas de aplicativos; quantidade de usuários; responsáveis independentes por aprovação; limites de despesa e ajustes; regras de mensalidade e afastamento; prazos de análise, guarda e restauração; possibilidade de várias casas no futuro. Na limpeza: público de cada mutirão, quantidade-alvo por turno, mínimo/teto quando necessários, recorrência, áreas restritas, horários, coordenador e substituto, prazo de resposta, política de troca/dispensa, forma de revisão de tarefas, participação de voluntários e eventual equivalência entre mutirão e rodízio regular.

Na comunicação: aparelhos e versões suportados, PWA ou aplicativo nativo/multiplataforma, provedor técnico de push, prazo de validade/retenção, horários silenciosos, limites de lembrete, regras de ciência e recuperação de conta. A exclusividade no próprio aplicativo do celular e a ausência de WhatsApp e outros canais de mensagem já são decisões desta versão, não opções a habilitar pelo desenvolvedor.

Essas decisões parametrizam e validam a implementação. Não devem ser preenchidas pelo desenvolvedor com suposições apresentadas como regras já aprovadas.

## 18. Fontes primárias consultadas

Referências [S1]–[S13] preservadas da documentação anterior, sem nova validação nesta revisão. Para a versão 0.3, [S14]–[S16] foram consultadas em 21/09/2026 para limites de transporte, permissões e compatibilidade móvel. Requisitos, políticas e limites configuráveis são propostas de produto e não obrigações extraídas automaticamente dessas fontes. Os testes listados são critérios de aceitação, não execuções já realizadas.

- **[S1] Banco Central — Pix Agendado:** `https://www.bcb.gov.br/estabilidadefinanceira/pix-agendado`
- **[S2] Banco Central — Especificação oficial da API Pix:** `https://raw.githubusercontent.com/bacen/pix-api/master/openapi.yaml`
- **[S3] Instituto Nacional de Tecnologia da Informação — dúvidas sobre VALIDAR:** `https://validar.iti.gov.br/duvidas.html`
- **[S4] OWASP — File Upload Cheat Sheet:** `https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html`
- **[S5] OWASP — Web Security Testing Guide, Test Upload of Malicious Files:** `https://owasp.org/www-project-web-security-testing-guide/v42/4-Web_Application_Security_Testing/10-Business_Logic_Testing/09-Test_Upload_of_Malicious_Files`
- **[S6] OWASP — Authorization Cheat Sheet:** `https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html`
- **[S7] OWASP — Transaction Authorization Cheat Sheet:** `https://cheatsheetseries.owasp.org/cheatsheets/Transaction_Authorization_Cheat_Sheet.html`
- **[S8] OWASP — Logging Cheat Sheet:** `https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html`
- **[S9] Presidência da República — Lei nº 13.709/2018, LGPD, texto atualizado:** `https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm`
- **[S10] Efí — documentação de webhooks da API Pix:** `https://dev.efipay.com.br/docs/api-pix/webhooks/`
- **[S11] Microsoft — EF Core, transações:** `https://learn.microsoft.com/en-us/ef/core/saving/transactions`
- **[S12] Microsoft — EF Core, conflitos de concorrência:** `https://learn.microsoft.com/en-us/ef/core/saving/concurrency`
- **[S13] MDN — Progressive Web Apps:** `https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps`

- **[S14] Firebase — validade e entrega de mensagens FCM:** `https://firebase.google.com/docs/cloud-messaging/customize-messages/setting-message-lifespan`
- **[S15] WebKit — Web Push para apps na tela inicial do iOS/iPadOS:** `https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/`
- **[S16] Android Developers — permissão de notificações:** `https://developer.android.com/develop/ui/compose/notifications/notification-permission`
