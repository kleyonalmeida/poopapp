# Validação de login e sessões — F1

Data: 2026-11-12. Status desta fatia backend: **CONCLUÍDA**. F1 permanece parcial pela interface mobile e demais fluxos de conta pendentes.

## Entrega

Login por e-mail/senha, refresh rotativo, logout e consulta da própria conta, com autenticação Bearer do ASP.NET Core e persistência em PostgreSQL. Sessão com acesso de até 15 minutos e prazo absoluto de 7 dias; tokens utilizáveis não são armazenados no banco.

Alterações principais:

- `Domain/AuthSession.cs`: validade, revogação e limite absoluto da sessão, sem referências a EF/HTTP.
- `Application`: ports, casos de login/refresh, DTOs de resultado e validadores separados; validação de e-mail compartilhada com cadastro.
- `Infrastructure/AccountSessions.cs`: credenciais Identity, transações, locks de conta/sessão, hashes e persistência.
- `Presentation/SessionAuthenticationHandler.cs` e `SessionEndpoints.cs`: Bearer, desafios, autorização e contratos.
- `Program.cs`: composição da autenticação/autorização e filtro de logs de resumo de requisições.
- Migration `AddRevocableSessions`, snapshot e testes HTTP/migration.

## Evidência RED → GREEN → REFACTOR

1. Os primeiros 23 cenários de sessão produziram 20 falhas pela ausência das rotas (404), enquanto 3 proteções HTTP já existentes passaram. Um cenário adicional de renovação no último minuto também falhou porque login ainda retornava 404.
2. Após implementar sessão e gerar/aplicar a migration no PostgreSQL descartável, os 42 testes totais passaram.
3. A revisão adicionou um teste de logs: uma credencial enviada em query string, embora rejeitada, aparecia nos resumos padrão do Hosting.Diagnostics. O teste falhou antes da correção. O filtro de categoria removeu esse vazamento sem habilitar logging de corpos ou dados sensíveis.
4. Foi acrescentada validação de upgrade/rollback da migration preservando usuários. Os 44 testes passaram.
5. Com a suíte verde, a validação de login/refresh foi separada dos casos de uso/endpoints. A regressão final em Release permaneceu verde: **44 aprovados, nenhum ignorado**.

Asserts não foram removidos, relaxados ou substituídos por mocks. Configuração/compilação quebrada não foi considerada evidência RED.

## Comportamentos verificados

- Login normaliza e-mail e preserva a senha, inclusive espaços externos; falhas de usuário/senha/bloqueio usam resposta genérica.
- Banco contém apenas SHA-256 do acesso/refresh; hash de senha segue verificação Identity existente. Login não emite cookie.
- `/api/v1/me` retorna somente UUID, e-mail e confirmação do titular; um ID alheio em query não muda a identidade consultada.
- Me/logout exigem Bearer e retornam 401 ProblemDetails com desafio quando não autenticados.
- Troca de tipos, token adulterado/desconhecido e credencial em query não autenticam.
- Logout revoga ambos os tokens da sessão corrente, preservando outra sessão da mesma conta.
- Refresh substitui acesso e refresh; replay de credencial consumida revoga também tokens renovados.
- Duas renovações concorrentes têm somente um sucesso; o replay invalida a família, impedindo duas credenciais renováveis ativas.
- Acesso expira aos 15 minutos e refresh aos 7 dias. Renovar no último minuto limita também o novo acesso ao prazo original. Testes usam TimeProvider controlado, sem sleeps.
- Mudar SecurityStamp invalida acesso/refresh antigos; as consultas verificam o banco por requisição.
- Cinco falhas de senha simultâneas bloqueiam a conta. Rate limits de login e refresh são independentes de cadastro/healthcheck e não são contornados por X-Forwarded-For.
- Sessões persistem ao recriar o host da aplicação.
- Credenciais não aparecem nos logs capturados dos fluxos testados, inclusive na tentativa de query token.
- Todas as novas rotas rejeitam HTTP e não permitem cache.

## Migration

Gerada por `dotnet ef migrations add AddRevocableSessions`, usando a ferramenta local fixada em 10.0.12. Adiciona somente Sessions/SessionRefreshTokens, seus índices e FKs no schema `identity`; não altera usuários existentes.

O teste de migration cria PostgreSQL/PostGIS 17-3.5, aplica InitialIdentity, grava uma conta fictícia, aplica a nova migration, reverte apenas a migration de sessão e a reaplica. UUID, e-mail e SecurityStamp permanecem. Nenhum banco persistente foi migrado ou revertido nesta entrega. O SQL incremental foi gerado em `/tmp/trono-session-up.sql` para revisão.

`Down` elimina sessões e refreshes; uma reversão operacional precisa coordenar a versão da API e os logins. O teste em banco descartável não autoriza executar essa operação em ambientes compartilhados.

## Comandos e resultados

Executados com .NET SDK 10.0.301, `DOTNET_CLI_HOME=/tmp/trono-dotnet`; sockets e Docker exigiram execução fora do sandbox, sem mudar permissões do socket.

```sh
dotnet format apps/api/Trono.slnx --no-restore
dotnet build apps/api/Trono.slnx --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet test apps/api/Trono.slnx --no-build --configuration Release -m:1
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore
# ConnectionStrings__Identity fictícia para instanciar o modelo, sem consultar o banco:
dotnet ef migrations has-pending-model-changes \
  --project apps/api/src/Trono.Modules.Identity \
  --startup-project apps/api/src/Trono.Api --configuration Release --no-build
```

Resultados: build sem warnings/erros; **44 testes aprovados** em PostgreSQL real; formatação aprovada; snapshot sem alterações pendentes. Nenhuma dependência nova foi adicionada nesta fatia. Testes anteriores de cadastro permanecem na regressão.

O build da imagem e `docker compose up --build --detach --wait` passaram no projeto isolado `trono-session-check`, com credencial temporária fora do repositório. O Compose não instala TLS nem aplica migrations automaticamente; autenticação funcional foi exercitada pelo host HTTP de testes em HTTPS lógico, com PostgreSQL real, sem alegar teste de handshake TLS ou dispositivo.

Smoke final: `/health/live` retornou `Healthy`; PostGIS retornou `3.5 USE_GEOS=1 USE_PROJ=1 USE_STATS=1`; login/refresh/logout/me por HTTP retornaram 400 com no-store. `docker compose down` encerrou somente os contêineres temporários e sua rede, preservando o volume.

## Pendências

UI Flutter e armazenamento seguro dos tokens, confirmação/recuperação, perfil/preferências/exclusão e limpeza operacional do histórico de sessão antes do beta. Contas não confirmadas podem fazer login, sem alegar endereço verificado. O cliente deverá serializar refreshes para respeitar a política estrita de replay.

Os logs de proxy/infra externos dependem da implantação e não foram avaliados pelo teste do logger da aplicação. Android SDK/dispositivo e execução remota do CI continuam pendentes de F0. Flutter não foi alterado e seus testes não foram repetidos. `.git` continua sem metadados utilizáveis; não houve commit/push.
