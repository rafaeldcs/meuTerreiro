# Publicação, configuração e operação — v0.2

**Nenhuma implantação pública foi executada nesta entrega.** Use dados fictícios até concluir builds, integração, dispositivos e revisão do escopo.

## 1. Preparação e recursos

Utilize diretório, projeto Compose, volumes, subdomínio, contas e segredos diferentes dos da ShopAir. Não substitua o proxy atual, não remova volumes nem abra MongoDB na internet.

O Compose adiciona o processador de documentos e o ClamAV. A documentação oficial do ClamAV indica mínimo de 3 GiB e preferência de 4 GiB para o scanner, além dos demais serviços. O projeto limita o scanner a 4 GiB e recusa, no deploy padrão, menos de 6 GiB disponíveis. Isso deve ser revisado com a capacidade e carga reais da VPS — não é garantia de que qualquer servidor com 6 GiB sirva.

Referência: `https://docs.clamav.net/manual/Installing/Docker.html`.

Sem scanner/assinaturas carregadas, arquivos permanecem em quarentena. O painel não deve ser usado como prova de que um arquivo foi liberado pelo antimalware quando não há resultado.

## 2. Builds antes da divulgação

Execute o pipeline ou o roteiro `VALIDACAO.md`. Resolva todas as falhas. O pacote não contém `package-lock.json` inventado: instale, revise, versione o lock e passe a usar `npm ci`. Audite dependências e revise imagens/tags antes de publicar. O scanner usa a tag `stable`, que pode mudar; fixe digest após uma imagem ter sido testada na sua implantação.

## 3. Novo ambiente

```bash
node scripts/init.mjs --host hml.terreiro.example.com --port 8088
```

Substitua o domínio por um domínio autorizado. O script gera `.env` e `.secrets/mongo-key`; não registra domínio nem configura TLS. A origem HTTPS precisa coincidir com a origem usada no navegador. `Production` é bloqueado neste alpha.

No Caddy rodando no host, acrescente **somente** o novo site, após revisar a configuração existente:

```caddyfile
hml.terreiro.example.com {
    reverse_proxy 127.0.0.1:8088
}
```

Se o proxy roda em container, localhost é o próprio container: configure conscientemente a rede do gateway. Não abra MongoDB nem a API diretamente como atalho. DNS, Cloudflare, portas e certificado continuam dependendo da configuração autorizada do host.

```bash
bash scripts/deploy-hml.sh
```

Esse comando constrói e inicia apenas este projeto, verifica a saúde local e não apaga volumes. Não prova resolução do domínio externo, teste de negócio, autenticação no celular, scanner operacional ou recebimento de push.

## 4. Atualização de instalação anterior

Preserve `.env`, `.secrets` e todos os volumes. Faça backup e ensaio de restauração primeiro. Acrescente a nova chave privada sem sobrescrever as antigas:

```bash
node scripts/upgrade-config.mjs
```

Usuários do alpha anterior podem não ter o campo `Permissions`. A API recusa uma migração silenciosa. Após aprovação do responsável, em homologação, acrescente temporariamente `MIGRATE_LEGACY_PERMISSIONS=true` ao `.env` e reinicie a API da nova versão. A migração se limita a documentos legados da casa sem permissões: converte os perfis, registra auditoria e revoga sessões/push. Administradores legados recebem as permissões de bootstrap, que **precisam ser revisadas** antes de qualquer uso real. Remova a autorização de migração após a conclusão.

Não utilize esse mecanismo para redefinir uma conta nova, contornar uma aprovação ou reparar diretamente dados financeiros. Não há uma migração de produção homologada nesta entrega.

## 5. Configuração opcional de Pix

O banco definitivo da casa ainda não foi selecionado. O único adaptador de fonte nesta entrega é Efí **Sandbox**, desligado no Compose base. Não confundir isso com banco escolhido/aprovado pelo usuário ou com integração universal.

Após criar uma conta administrativa fictícia no app e obter credenciais autorizadas de Sandbox, use um override Compose privado com as variáveis `Pix__Enabled=true`, `Pix__Provider=Efi`, `Pix__Environment=Sandbox`, `Pix__ClientId`, `Pix__ClientSecret`, `Pix__CertificatePath`, `Pix__CertificatePassword`, `Pix__Key`, `Pix__AccountId` e `Pix__RecipientLabel`. Monte o certificado somente-leitura fora do repositório. Nunca envie essas credenciais na conversa.

O worker cria cobrança e consulta por polling autenticado. Não há webhook homologado nem execução automática de devolução. Toda confirmação desse caminho fica como `ProviderSandbox`. O código impede ambiente de dinheiro real. A adaptação/homologação para produção ainda é uma atividade de desenvolvimento, não apenas trocar uma flag.

## 6. Instalação e notificações no celular

Em navegador móvel compatível, abra a origem HTTPS e instale/adicione à tela inicial. No iPhone compatível, abra pelo ícone da PWA instalada antes de habilitar os avisos. Esta distribuição não é um APK/IPA nem publicação em loja.

Configuração Web Push: chaves VAPID geradas no host, origem HTTPS correta, permissão do usuário, transporte acessível e dispositivo compatível. Teste app em segundo plano, dados móveis, permissão negada, logout, reinstalação, troca de conta e aviso atrasado/cancelado. Sem entrega, a central permanece a referência. Nunca usar WhatsApp, e-mail ou SMS como fallback.

## 7. Backup e restauração

Os scripts foram escritos e passaram por análise sintática, **não foram executados com Docker/age**. Requerem revisão do operador e janela de manutenção. Não agendam execução nem trabalham em segundo plano por esta conversa.

Instale a ferramenta `age` por uma fonte oficial. Gere/guarde a identidade privada fora do servidor e use apenas o destinatário público para criptografar o backup. Não guardar chave privada junto do arquivo criptografado.

```bash
AGE_RECIPIENT=age1_SUA_CHAVE_PUBLICA bash scripts/backup.sh /pasta-externa-protegida
```

O script suspende API/app/processador que estiverem ativos, copia banco/arquivos/configurações, registra hashes, criptografa em `tar.age` e tenta retomar os serviços. Use armazenamento temporário e destino protegidos; arquivos temporários contêm dados em claro durante a cópia. Backup inclui segredos porque a restauração depende deles: controle acesso e retenção.

Ensaio em **diretório inexistente, projeto e volumes novos**, nunca sobre a instalação atual:

```bash
ACK_RESTORE_DRILL=yes RESTORE_HTTP_PORT=18088 bash scripts/restore-drill.sh \
  /backup/terreiro-DATA.tar.age /caminho-seguro/identidade.age /opt/terreiro-restore-NOVO
```

O ensaio verifica integridade, restaura dados e anexos, desabilita push e não ativa Pix. A saúde HTTP é apenas o primeiro critério: compare saldos, destinações, documentos e permissões. O script não apaga os volumes criados, não remove a instalação original e não mede sozinho prazo máximo de recuperação. Defina retenção, cópia externa e periodicidade com a casa.

## 8. Roteiro mínimo de aceite

Usar contas fictícias em dois celulares e um computador. Testar login/recuperação/perfis; geração/baixa parcial/duplicidade; aprovação independente/despesa; reconciliação; material reservado/consumido/devolvido; limpeza X/mutirão/troca; mensagens lidas sem confirmação; papel de coordenador sem documentos financeiros. Repetir em Wi-Fi/dados móveis e telas estreitas.

A compilação e a publicação não eliminam as lacunas descritas em `STATUS.md`. Só após resolução e aprovação documentada o projeto deve ter a restrição de produção reconsiderada.
