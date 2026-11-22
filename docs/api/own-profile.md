# Próprio perfil

Os dois endpoints exigem HTTPS e a credencial de acesso `Authorization: Bearer <accessToken>`. As respostas usam `Cache-Control: no-store`. A sessão determina a conta. Campos ou parâmetros extras que contenham IDs não mudam o alvo da operação.

## GET `/api/v1/me/profile`

Retorna `200 OK` com o apelido atual. Uma conta recém-criada ou anterior à migration começa com `null`.

```json
{"nickname":null}
```

O mesmo campo é retornado em [GET `/api/v1/me`](sessions.md), usado pelo app ao carregar a conta.

## PATCH `/api/v1/me/profile`

O corpo JSON exige a propriedade `nickname`. Envie uma string para definir o apelido ou `null` para removê-lo.

```json
{"nickname":"  Guardiã do Trono 👑  "}
```

A API remove espaços em branco nas extremidades e aceita de 1 a 40 unidades UTF-16 em uma linha, sem caracteres de controle internos. O apelido é opcional e pode coincidir com o de outra conta.

Sucesso `200 OK` com o valor gravado.

```json
{"nickname":"Guardiã do Trono 👑"}
```

Uma alteração grava somente o apelido. Se duas alterações válidas da mesma conta forem gravadas, prevalece a última gravação no banco.

## Erros e privacidade

- `400` para HTTP sem TLS, corpo JSON inválido ou apelido inválido. Erros de validação do apelido usam a chave `nickname`.
- `401` para sessão ausente, inválida, revogada ou expirada. A operação também devolve `401` se a conta tiver sido excluída entre a autenticação e o acesso ao perfil.
- `415` para um corpo enviado com tipo de conteúdo incompatível com JSON.
- O apelido fica na linha da conta e é removido com a [exclusão da própria conta](account-deletion.md). A resposta do perfil contém somente o apelido e fica restrita ao titular.

## Migration

`AddAccountNickname` adiciona uma coluna anulável `identity.AspNetUsers.Nickname`. Aplique a migration explicitamente antes de iniciar a nova versão da API. O rollback da migration remove o campo e os apelidos salvos nele. O processo de atualização está descrito no [contrato de cadastro](registration.md).
