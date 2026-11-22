# Exclusão da própria conta

`DELETE /api/v1/me/account`, com HTTPS, `Authorization: Bearer <accessToken>` e `Content-Type: application/json`.

```json
{"password":"a senha atual exata"}
```

A conta é selecionada exclusivamente pela sessão autenticada. Não há campo de ID de destino; IDs adicionais no corpo/query não mudam a conta excluída. Senha não sofre trim, exige valor não vazio e aceita até 128 caracteres. O corpo de DELETE é explicitamente vinculado com FromBody.

| Resultado | HTTP |
|---|---|
| Conta removida e transação confirmada | 204, sem corpo |
| Campos inválidos | 400 ValidationProblemDetails |
| Credencial ausente/inválida ou conta já removida | 401 ProblemDetails |
| Senha recusada ou conta temporariamente bloqueada | 403 ProblemDetails genérico |
| Falha de concorrência reportada pelo Identity | 409 ProblemDetails |
| Mais de 5 tentativas/minuto/IP/processo | 429 ProblemDetails |

Respostas não podem ser armazenadas (`no-store`). HTTP é recusado com 400 antes do caso de uso. Headers encaminhados enviados pelo cliente não alteram o limite; a configuração de proxies confiáveis continua explícita.

## Efeito atual

UserManager verifica a senha e remove o usuário em uma transação, com lock de linha para serializar solicitações. As FKs removem sessões, histórico de refresh, claims/logins/tokens Identity, registros privados e recibos de criação de Records. Depois do commit, nenhum acesso ou refresh antigo autentica. A exclusão não afeta contas alheias. Uma nova conta com o mesmo e-mail recebe outro UUID e não reativa credenciais anteriores.

Uma chamada duplicada depois da remoção recebe 401: não há mais identidade que a autorize. A remoção tem efeito único; não há novo evento/recompensa associado. Outras sessões da mesma conta, mesmo renovando em paralelo, não sobrevivem à exclusão. Requisições que já haviam sido autorizadas antes do commit não são canceladas retroativamente.

Senha incorreta incrementa o contador de falhas existente, sem excluir conta nem revogar suas sessões. Cinco falhas bloqueiam a conta por 5 minutos. O limite de requisições de exclusão é independente dos de login, refresh, cadastro e healthcheck.

## Cliente

Em Minha conta, abrir Excluir minha conta. A tela exige senha e consentimento; o diálogo final permite cancelar sem requisição. Durante o envio, o botão é desabilitado. Senha permanece somente em memória do formulário e no corpo HTTPS; não é persistida ou registrada em logs.

A leitura de verificação anterior pode renovar acesso; o DELETE nunca é repetido automaticamente. Um 403 é uma recusa, não um gatilho de refresh. 400/403/409/429 permitem corrigir/aguardar sem declarar exclusão. Se a resposta for incerta, o app limpa o acesso local quando possível e informa que a remoção não foi confirmada, exigindo nova verificação por login.

Confirmar remoção no servidor e limpar storage são resultados independentes: se uma falhar, a mensagem não afirma o sucesso dessa etapa. Mesmo com falha de limpeza, a UI esconde os dados privados; somente a confirmação 204 autoriza informar que a conta foi excluída.

## Limites

Os dados persistidos atuais abrangem Identity/sessão e registros privados/recibos de Records, todos incluídos na exclusão operacional. Não existem perfis sociais, mídia ou recompensas. Novos módulos deverão estender o fluxo de exclusão antes de sua disponibilização. Não há promessa de remoção de backups/logs de infraestrutura ou apagamento físico de bytes do disco. [Escopo de Records](records.md).

Não há undo. Esta implementação não substitui políticas de retenção e operação que ainda precisam ser definidas para beta/produção. Os testes executados pelo agente excluem apenas contas fictícias em bancos descartáveis; nenhuma conta real ou banco persistente foi alterado.
