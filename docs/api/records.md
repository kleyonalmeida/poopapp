# Registros privados

Todas as rotas exigem HTTPS, sessão Bearer válida e respondem com `Cache-Control: no-store`, inclusive em erros. A conta é determinada exclusivamente pela sessão. Não há publicação social nesta entrega.

## Conteúdo

POST e PATCH recebem todas as propriedades abaixo. Valores opcionais são representados por `null`; no PATCH, isso remove o valor anterior.

```json
{
  "occurredAt": "2026-11-22T12:00:00Z",
  "durationSeconds": null,
  "type": "torazona",
  "notes": "Uma história épica, só minha."
}
```

- `occurredAt`: timestamp com fuso explícito, nos últimos 30 dias, tolerando até cinco minutos à frente do relógio do servidor. UTC no retorno. Editar uma data já fora da janela é permitido somente mantendo o instante original.
- `durationSeconds`: inteiro de 1 a 86400 ou null. Não concede XP nem incentiva permanecer no vaso.
- `type`: `bode`, `serpentao`, `torazona`, `cachoeira`, `pedregulhos`, `erupcao` ou null.
- `notes`: até 280 unidades UTF-16 ou null. Espaços nas extremidades são removidos, vazio vira null. Controles especiais são rejeitados, exceto quebras de linha.

Datas de exemplos são ilustrativas e precisam respeitar a janela na execução real. A resposta do registro contém somente `id` e as quatro propriedades do conteúdo; não expõe conta, credenciais ou chave de envio.

## Rotas

| Método e rota | Resultado |
|---|---|
| POST `/api/v1/records` | `201` com registro e `Location`; exige header `Idempotency-Key: <uuid>` |
| GET `/api/v1/records/{id}` | `200` com o próprio registro |
| PATCH `/api/v1/records/{id}` | `200` com conteúdo completo atualizado |
| DELETE `/api/v1/records/{id}` | `204`; remoção definitiva do registro |
| GET `/api/v1/records?offset=0&limit=20` | `200` com `{"items":[],"hasMore":false}` |

Paginação: offset de 0 a 100000, limit de 1 a 50 (padrão 20). Ordem decrescente por `occurredAt` e ID. Gravações concorrentes podem deslocar páginas: atualizar reinicia a listagem. Não retorna registros de terceiros, mesmo com IDs de conta extras em query ou corpo.

## Reenvio e erros

POST repetido com a mesma chave e conteúdo normalizado retorna `200` com o estado atual do registro, sem duplicar. Conteúdo diferente com a mesma chave retorna `409`. Registro já excluído retorna `410` quando seu envio original é repetido; o recibo permanece até a exclusão da conta e não contém a história em texto puro. A chave é específica da conta. Não repetir automaticamente escritas em caso de resposta incerta.

`400` para conteúdo/paginação/chave inválidos ou HTTP sem TLS; `401` para credencial ausente, inválida, expirada ou conta removida durante a criação; `404` indistinto para registro inexistente ou de outra conta; `415` para tipo de conteúdo incompatível. Falhas usam ProblemDetails. PATCH válido concorrente segue última gravação; DELETE repetido retorna 404. Rotas por ID usam UUID.

Excluir a conta elimina registros e recibos na mesma transação, por FKs em cascata. Excluir um registro remove seu conteúdo, preservando somente o recibo de deduplicação. Não há mídia, local, publicação ou recompensa dependente nesta fatia.

## Migration e execução

No Compose Development, `docker compose up -d` executa o serviço separado `migrate`, aplicando Identity e depois Records antes de liberar a API. Fora dele, aplicar explicitamente nessa mesma ordem no banco local. Não há migration automática no processo da API. Exemplo de aplicação manual de Records:

```sh
dotnet ef database update --context RecordsDbContext \
  --project apps/api/src/Trono.Modules.Records \
  --startup-project apps/api/src/Trono.Api
```

A connection string continua em `ConnectionStrings__Identity`. `/health/ready` exige os históricos dos dois schemas atualizados. `InitialPrivateRecords` cria somente tabelas/índice de Records e FKs para a conta. Reverter remove registros e recibos; não é rollback seguro de produção. Após a validação em bancos descartáveis, as migrations foram aplicadas com autorização ao [banco persistente local de desenvolvimento](../operations/local-database.md), sem operação em produção.
