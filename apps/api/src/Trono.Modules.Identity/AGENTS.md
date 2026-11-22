# Identity — cadastro e sessões (F1)

Leia `AGENT.md`, `AGENTS.md` e `apps/api/AGENTS.md` da raiz antes de alterar código.

Escopo atual: cadastro público e login por e-mail/senha, sessão opaca revogável, refresh rotativo, logout e leitura da própria conta. Cadastro continua sem autenticar automaticamente.

- `Application`: comando, validação de entrada, resultado e port para criação de conta. Não referenciar EF, HTTP ou classes de Infrastructure.
- `Infrastructure`: ASP.NET Core Identity com IDs `Guid`, EF Core/Npgsql, schema `identity`, normalização e hash do framework. Índices únicos protegem cadastros concorrentes.
- `Presentation`: `POST /api/v1/auth/register`, DTO explícito, HTTPS, resposta mínima e ProblemDetails.
- `IdentityModule`: composição dos serviços e política de rate limiting por IP remoto, sem confiar em cabeçalhos encaminhados não configurados.
- `Domain`: ciclo de vida da sessão, independente de EF/HTTP. Acesso dura até 15 minutos; sessão/refresh têm limite absoluto de 7 dias. Reutilizar refresh consumido revoga a sessão inteira, inclusive em concorrência.

Guardar apenas SHA-256 dos tokens aleatórios de 256 bits, nunca tokens puros. Separar tipos de acesso/refresh. Autorizar `/api/v1/me` pela identidade autenticada, não por ID recebido. Logout revoga apenas a sessão corrente; mudança do SecurityStamp invalida todas as sessões antigas. Atualizações concorrentes exigem transações e locks por conta/sessão.

E-mail: trim, máximo 254 caracteres, formato validado. Senha: 12–128 caracteres, preservada integralmente, sem regras artificiais de composição. Nunca incluir senha, hash ou e-mail em mensagens de erro/log.

Não aplicar migrations automaticamente no startup. Gerar com EF CLI e testar aplicação em PostgreSQL real via Testcontainers. A conexão é fornecida por `ConnectionStrings__Identity`; não gravar valores reais no repositório.

Contratos em `docs/api/registration.md` e `docs/api/sessions.md`; testes em `RegistrationTests.cs`, `SessionTests.cs` e `SessionMigrationTests.cs` em `tests/api/Trono.IntegrationTests`.

Exclusão: `DELETE /api/v1/me/account` usa somente AccountId da sessão e exige senha atual, preservada integralmente. O lock de usuário e a transação protegem exclusão concorrente e cascatas existentes. Senha incorreta/bloqueio retornam 403; limite independente de 5 tentativas/minuto/IP. Não registrar senha, corpo ou tokens. Testes apenas com contas fictícias em PostgreSQL descartável. Novos dados de usuário em outros módulos exigem estender esta coordenação e seus testes antes de disponibilizá-los.

Retenção: `PruneExpiredSessions` remove em lotes somente famílias com ExpiresAt <= relógio UTC. Acesso expirado, revogação ou refresh consumido não bastam. Preservar usuários e histórico de famílias ainda no prazo. `SessionCleanupService` cria escopo por ciclo, está desativado por padrão e exige opt-in/configuração válida. Não executar em base persistente para demonstração. Configuração e limites em `docs/operations/session-retention.md`; testes usam PostgreSQL descartável.

Próprio perfil. `GET/PATCH /api/v1/me/profile` usam o titular da sessão. A mudança valida o apelido e atualiza somente `Account.Nickname`. JSON com `nickname: null` remove o valor. A migration adiciona coluna anulável. A exclusão da conta remove o campo com a própria linha. A mesma propriedade aparece em `GET /api/v1/me`. O contrato está em `docs/api/own-profile.md` e a cobertura HTTP em `OwnProfileTests.cs`.

Preferências da conta. `GET/PATCH /api/v1/me/settings` lê e altera `pushNotificationsEnabled`. O PATCH exige um booleano explícito e altera somente o campo do titular da sessão. O campo é gravado na conta, com padrão `false`, e eliminado com ela. A mesma propriedade está presente em `GET /api/v1/me`. Contrato e testes em `docs/api/own-settings.md` e `OwnSettingsTests.cs`.

A verificação de prontidão de Identity consulta o histórico de migrations via EF, com cancelamento após três segundos. Conexão indisponível ou migrations pendentes tornam `/health/ready` indisponível. Somente a sonda de prontidão executa essa verificação.
