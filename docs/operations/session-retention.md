# Limpeza operacional de sessões

A rotina remove somente famílias de sessão que atingiram `ExpiresAt`, incluindo a igualdade. O usuário e seus dados de conta são preservados. As cascatas existentes removem o histórico de refresh correspondente; não há migration nova.

Expiração de acesso, revogação antecipada e refresh consumido não são critérios de exclusão. Uma família ainda no prazo absoluto permanece no banco, com os hashes necessários para detectar replay.

## Configuração

Desativada por padrão. Habilitar é uma decisão explícita do operador, após revisar o ambiente, aplicar migrations e confirmar a conexão Identity. Esta entrega executou limpeza apenas em fixtures descartáveis; não habilitou ou operou bases persistentes.

| Configuração da API | Padrão | Limite |
|---|---|---|
| `Identity__SessionCleanup__Enabled` | `false` | `true`/`false` |
| `Identity__SessionCleanup__BatchSize` | `100` | 1–1000 |
| `Identity__SessionCleanup__IntervalMinutes` | `15` | 1–1440 |

Habilitar sem conexão Identity ou fornecer lote/intervalo fora da faixa impede o startup. Alterações exigem reinício da API; não há toggle ou endpoint público de limpeza. Desativada, a rotina não acessa o banco nem impede o healthcheck sem conexão configurada.

No Compose, os nomes equivalentes em `.env` são `IDENTITY_SESSION_CLEANUP_ENABLED`, `IDENTITY_SESSION_CLEANUP_BATCH_SIZE` e `IDENTITY_SESSION_CLEANUP_INTERVAL_MINUTES`. Os valores de exemplo mantêm a rotina desativada. Não executar habilitação ou comandos contra produção sem autorização específica.

## Execução e concorrência

Quando habilitada, executa um lote ao iniciar e aguarda o intervalo após cada tentativa. Cada ciclo cria um escopo próprio para DbContext. A seleção usa prazo absoluto e ordem `ExpiresAt, Id`, com `LIMIT` e `FOR UPDATE SKIP LOCKED`; uma única instrução SQL remove o lote e suas dependências.

Uma transação que esteja usando uma linha não é aguardada pelo seletor: ela fica para outro ciclo. Duas instâncias podem trabalhar sem remover a mesma sessão duas vezes. Lote limitado pode deixar backlog; a rotina não promete remoção de todo o conjunto em um único ciclo ou prazo legal exato. Reexecução sem elegíveis retorna zero.

Os mesmos usuários continuam acessíveis. Sessões ainda renováveis e famílias revogadas dentro de seu prazo absoluto são preservadas. Sessões expiradas já não autenticavam antes da limpeza; removê-las não cria ou reativa credenciais.

## Falhas e observabilidade

Evento 6201 (`SessionCleanupCompleted`) registra somente a contagem de sessões removidas. Evento 6202 (`SessionCleanupFailed`) informa falha genérica, sem exceção, SQL, IDs, hashes, tokens ou detalhes de conexão na mensagem da rotina. Não habilitar logging sensível do EF.

Falha de banco ou schema ausente não encerra a API: a rotina aguarda o próximo intervalo. Ela não aplica migrations e não faz retry em loop. Cancelamento de shutdown é encaminhado à operação de banco/espera e não é registrado como falha normal de limpeza.

Monitorar avisos, capacidade por lote e acúmulo de expiradas antes de habilitar em ambiente compartilhado. Backups, logs de infraestrutura, contas e dados de outros módulos estão fora desta rotina. Ela não faz VACUUM, manutenção de disco ou exclusão física garantida de bytes.
