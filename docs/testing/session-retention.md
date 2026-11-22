# Validação da limpeza de sessões expiradas

Data: 2026-11-13. Implementação backend validada; rotina desativada por padrão e não habilitada em bancos persistentes.

## Entrega

`PruneExpiredSessions` usa relógio UTC e porta de retenção. `SessionRetentionStore` remove apenas famílias com `ExpiresAt <= cutoff`, em lote ordenado, com `FOR UPDATE SKIP LOCKED` e cascatas existentes para refresh. `SessionCleanupService` cria escopo por ciclo, executa um lote por intervalo e trata falha/cancelamento. Configuração validada no startup; habilitar exige conexão Identity.

Arquivos principais: `Application/PruneExpiredSessions.cs`, `Infrastructure/SessionRetentionStore.cs`, `Infrastructure/SessionCleanupService.cs`, composição Identity, `SessionCleanupTests.cs` e `SessionCleanupServiceTests.cs`. Compose e `.env.example` expõem configuração com habilitação `false`. Sem contrato HTTP, dependência ou migration novos.

## RED → GREEN

- Retenção e limites: 8 testes falharam pela ausência de remoção/validação; o cenário de preservação já passava. Falhas de compilação de scaffolding não foram contadas como RED.
- Serviço/configuração: 6 falhas e 1 aprovação antes da implementação, por ausência de execução e rejeição de limites inválidos.
- Habilitação sem conexão: falha específica por ausência de `OptionsValidationException`.
- Após implementação: **71 testes backend aprovados em Debug e Release**, sem falhas ou testes ignorados. Asserts não foram afrouxados.

## Cobertura

- Prazo exato é elegível; usuários são preservados; refresh correspondente é removido em cascata; reexecução retorna zero.
- Acesso vencido, refresh consumido e revogação antecipada não removem famílias ainda no prazo absoluto, preservando detecção de replay.
- Lote limitado e ordem por expiração; execuções sucessivas progridem.
- PostgreSQL real: linha bloqueada é pulada; dois executores removem quatro famílias sem dupla contagem.
- Host desativado não limpa; host explicitamente habilitado executa somente o lote configurado.
- Limites inválidos e habilitação sem conexão impedem startup.
- Falha emite aviso genérico sem dados sensíveis da exceção, aguarda intervalo e mantém serviço ativo. Cancelamento chega ao armazenamento e não vira aviso de falha.

As operações de remoção usaram somente contas fictícias em PostgreSQL/PostGIS descartável. Nenhuma conta real, base persistente ou volume de desenvolvimento foi limpo. O cenário de concorrência usa locks/transações reais, sem EF InMemory.

## Verificações executadas

```sh
dotnet test apps/api/Trono.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet format apps/api/Trono.slnx --no-restore
dotnet build apps/api/Trono.slnx --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet test apps/api/Trono.slnx --no-build --configuration Release -m:1
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore
dotnet ef migrations has-pending-model-changes --project apps/api/src/Trono.Modules.Identity --startup-project apps/api/src/Trono.Api --configuration Release --no-build
```

Build Release, 71 testes e formatação aprovados; EF confirmou modelo sem diferenças desde a última migration. A verificação de modelo usou conexão fictícia de design, sem aplicar migrations. `docker compose config --quiet` passou com senha fictícia e limpeza desativada; não iniciou serviços. O ambiente usa `DOTNET_CLI_HOME=/tmp/trono-dotnet`; execução com acesso a sockets locais/Docker para testes descartáveis.

A imagem `trono-session-retention:check` compilou com `docker build --file infrastructure/api.Dockerfile --tag trono-session-retention:check .`, sem publicação ou execução contra base persistente.

Regressão real do cliente:

```sh
DOTNET_CLI_HOME=/tmp/trono-dotnet \
NUGET_PACKAGES=/home/kley/.nuget/packages \
NUGET_HTTP_CACHE_PATH=/tmp/trono-nuget-http \
HOME=/tmp/trono-flutter-home PUB_CACHE=/tmp/trono-pub-cache \
Identity__SessionCleanup__Enabled=false \
python3 scripts/test_mobile_api.py --flutter /tmp/trono-flutter-sdk/bin/flutter
```

**1 teste Flutter/API por HTTPS aprovado**, com certificado verificado e banco descartável. O fluxo existente de identidade e exclusão de conta permaneceu compatível; a rotina ficou desativada nessa regressão. O script encerrou os recursos descartáveis. O vault desse teste é em memória, sem validar criptografia nativa.

## Limites e continuidade

Nenhuma mudança mobile nesta fatia. O último resultado de unidade/widgets mobile permanece 41 testes aprovados, registrado no [relatório de exclusão](account-deletion.md). SDK/dispositivo Android, Keychain/Keystore nativos e CI remoto continuam pendentes; F0/F1 permanecem parciais. Confirmação/recuperação e perfil/preferências ainda não foram implementados.

Habilitação em ambiente compartilhado exige revisão operacional de capacidade, backlog e alertas. A rotina não garante prazo de esvaziamento, remoção de backups ou apagamento físico. Ver [operação](../operations/session-retention.md) e [ADR-0006](../decisions/ADR-0006-session-retention.md). Sem deploy, commit ou push nesta entrega.
