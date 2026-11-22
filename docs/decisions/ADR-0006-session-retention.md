# ADR-0006 — Limpeza limitada de sessões expiradas

Status: adotada e validada em fixtures descartáveis em 2026-11-13. Operação em ambientes persistentes não realizada.

## Decisão

Remover sessões somente quando o prazo absoluto `ExpiresAt` já foi atingido. Expiração do acesso, uso de refresh ou revogação antecipada não bastam: preservar a família e o histórico até seu prazo absoluto mantém a detecção de replay.

Executar um lote de até 100 sessões por ciclo, configurável entre 1 e 1000. Selecionar as mais antigas com lock de linha e SKIP LOCKED; uma única instrução transacional remove o lote e as FKs eliminam seu histórico de refresh. Não remover usuários, claims ou outros dados de conta. Execuções repetidas ou concorrentes devem ser seguras.

Hospedar um BackgroundService no processo da API, com novo escopo por ciclo. Não criar módulo Worker vazio, fila ou biblioteca de agendamento. Um lote por intervalo de 15 minutos (configurável entre 1 e 1440 minutos) limita carga; backlog e linhas bloqueadas podem exigir vários ciclos. Falha do ciclo gera aviso genérico e aguarda o próximo intervalo; cancelamento de shutdown é propagado sem erro ou loop de retry.

A rotina fica desativada por padrão e requer habilitação explícita, conexão configurada e migrations já aplicadas. Não há endpoint HTTP de limpeza. O agente executa exclusões apenas sobre fixtures descartáveis; nenhuma base persistente é operada nesta entrega.

## Limites

O índice de ExpiresAt e as cascatas já existem, sem migration nova. A política cobre apenas tabelas operacionais de sessões/refresh; não promete apagamento físico, limpeza de backups ou prazo legal garantido. Logs registram contagens/estado técnico, sem IDs, tokens ou exceções com detalhes de conexão. Telemetria de backlog e alertas de operação poderão ser ampliados antes de produção.

## Fontes

- [PostgreSQL: SELECT, limites e locking clauses](https://www.postgresql.org/docs/17/sql-select.html)
- [.NET: escopos em BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service)
