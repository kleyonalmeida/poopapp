# Preferências da conta

`GET /api/v1/me/settings` e `PATCH /api/v1/me/settings` exigem HTTPS e `Authorization: Bearer <accessToken>`. A sessão identifica a conta e as respostas usam `Cache-Control: no-store`.

## Consulta

`GET` retorna `200 OK` com a preferência atual. Contas novas e contas anteriores à migration começam com o valor `false`. O mesmo campo está disponível em [GET `/api/v1/me`](sessions.md).

```json
{"pushNotificationsEnabled":false}
```

## Alteração

`PATCH` exige a propriedade booleana `pushNotificationsEnabled`. Envie `true` para permitir notificações push e `false` para revogar a preferência. A resposta `200 OK` usa o mesmo formato e contém o valor gravado.

A alteração grava somente essa preferência. Repetir o mesmo valor tem o mesmo efeito, e em gravações concorrentes vale o último valor gravado. A escolha fica vinculada à conta e é removida junto com ela. O envio e a permissão nativa do sistema operacional pertencem à entrega de notificações.

## Erros

- `400` para HTTP sem TLS, JSON inválido, campo ausente, `null` ou outro tipo.
- `401` para sessão ausente, inválida ou expirada. A mesma resposta vale se a conta for excluída entre a autenticação e a operação.
- `415` para tipo de conteúdo incompatível com JSON.

## Migration

`AddPushNotificationPreference` adiciona uma coluna booleana em `identity.AspNetUsers` com `DEFAULT FALSE`. Aplique-a explicitamente antes de iniciar a nova versão da API. A reversão remove a coluna e as escolhas registradas. [Processo de atualização do banco](registration.md)
