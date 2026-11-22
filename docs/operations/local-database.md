# Banco local de desenvolvimento

Em 2026-11-22, após autorização explícita do usuário para aplicar as migrations, foi preparado o PostgreSQL persistente local do TRONO pelo Compose deste repositório. Antes da operação, não havia `.env`, conexão de Identity configurada, contêiner ou volume do TRONO. O banco foi criado novo, sem dados de usuário existentes; não foi necessário migrar ou remover dados anteriores.

## Ambiente preparado

- Serviço Compose `postgres`, contêiner `poopapp-postgres-1`, projeto `poopapp`.
- Banco e usuário `trono`, acesso pelo host em `127.0.0.1:5432`.
- Volume persistente `poopapp_trono-postgres`.
- Senha aleatória no `.env` local, ignorado pelo Git, com permissão `0600`. Não copiar seu conteúdo para documentação, logs ou commits.
- Limpeza automática de sessões permanece desativada.

Esse é o ambiente de desenvolvimento local, sem operação em produção. A configuração posterior de interface/API TLS em Docker está no [guia da prévia](local-preview.md). O Compose lê o `.env`; processos `dotnet run` no host precisam receber `ConnectionStrings__Identity` explicitamente, usando o endereço local e a credencial do `.env`. Não há leitura automática desse arquivo pelo ASP.NET Core.

## Migrations aplicadas

O build Release da API passou. O EF CLI aplicou Identity primeiro, seguido de Records, usando os contextos e projetos correspondentes:

| Schema | Migration |
|---|---|
| identity | `20261112151932_InitialIdentity` |
| identity | `20261112164701_AddRevocableSessions` |
| identity | `20261120021159_AddAccountNickname` |
| identity | `20261120023856_AddPushNotificationPreference` |
| records | `20261122185108_InitialPrivateRecords` |

A execução usou .NET 10.0.301 em `/tmp/trono-dotnet-sdk`, ferramenta EF fixada no repositório e `database update --configuration Release --no-build`, com `--context IdentityDbContext`/`RecordsDbContext`. A conexão foi passada pelo ambiente do processo, sem senha nos argumentos ou saída. Os históricos do banco confirmaram as cinco migrations.

## Verificação após aplicação

- PostgreSQL saudável no Compose.
- Tabelas e históricos dos schemas `identity` e `records` presentes.
- Contagens de contas, registros e recibos iguais a zero; nenhuma conta de demonstração criada.
- Instância temporária da API conectada ao banco local: `/health/ready` retornou `200 Healthy` com `no-store`; `/health/live`, `200 Healthy`.
- Registros por HTTP continuaram rejeitados com `400`, preservando a exigência de HTTPS.

A instância temporária da API foi encerrada depois da verificação. O PostgreSQL local permaneceu em execução. A regressão de código já realizada consta em [registros privados](../testing/private-records.md); não houve mudança de comportamento ou código de produção nesta operação.

Posteriormente, a prévia em Docker passou a iniciar a API e o gateway HTTPS permanentes de desenvolvimento, preservando esse banco e sua senha. A inicialização confirmou as migrations já atualizadas, sem alterações adicionais de schema. O comando atual é `docker compose up -d`, com migrations coordenadas pelo próprio Compose; consulte [a operação atual](local-preview.md).

## Continuidade

Para conferir o serviço, executar `docker compose ps`. Para interromper apenas o banco, `docker compose stop postgres`; os dados permanecem no volume. Não remover o volume nem executar reversões para atualizar o ambiente. Migrations futuras devem ser revisadas e aplicadas explicitamente, preservando a ordem dos módulos e os dados existentes.
