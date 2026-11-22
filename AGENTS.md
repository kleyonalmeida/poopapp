# TRONO — estado da implementação

Leia primeiro [AGENT.md](AGENT.md). O [plano mestre](TRONO_PLANEJAMENTO_COMPLETO.md) descreve o produto proposto, não funcionalidades entregues.

## Escopo atual

F0 possui API ASP.NET Core .NET 10 com sondas separadas de processo e prontidão, aplicativo Flutter e CI preparado. F1 possui cadastro, login, refresh rotativo, logout, própria conta e exclusão confirmada por senha no backend/mobile. F1 também inclui consulta e edição do apelido opcional e da preferência de notificações push, desativada por padrão. F2 iniciou com registros manuais privados: criação, histórico paginado, consulta, edição e exclusão; descrição humorística, história e duração são opcionais. Confirmação/recuperação, preferências adicionais, avatar, cronômetro resiliente, publicação social e recompensas seguem pendentes.

API e Flutter passaram em RED → GREEN. A regressão de 2026-11-22 aprovou 92 testes backend em PostgreSQL descartável, 53 testes Flutter com as dependências oficiais do lockfile e um fluxo cliente/API por HTTPS incluindo registros. Build Release, análise Flutter e imagem Docker passaram. Evidências e limitações atuais em `docs/testing/private-records.md`; relatórios anteriores preservam os resultados e bloqueios de cada entrega. F0/F1/F2 permanecem parciais, com execução em dispositivo, Keychain/Keystore, CI remoto e demais histórias pendentes.

A fatia posterior de prévia Docker aprovou 55 testes Flutter, análise/formatação, build web Release e smoke em Compose descartável com TLS, autenticação, PostGIS e privacidade dos logs. O navegador 390×844 confirmou login, registro privado, conteúdo ao reabrir editor e recarga sem credenciais persistidas. O ambiente local está disponível por HTTPS; evidências em `docs/testing/local-preview.md`. Isso não substitui a validação Android/iOS.

## Arquitetura

- `apps/api/src/Trono.Api`: composition root, saúde, ProblemDetails e composição de Identity.
- `apps/api/src/Trono.Modules.Identity`: ciclo de sessão em `Domain`, casos de uso/ports/validadores em `Application`, Identity/EF e tokens em `Infrastructure`, contratos HTTP/auth handler em `Presentation`. Schema `identity`, migrations explícitas, HTTPS e limites independentes de cadastro/login/refresh. Cadastro não autentica.
- `apps/api/src/Trono.Modules.Records`: registros privados, schema e migrations `records`, no mesmo PostgreSQL de Identity. Somente ID da conta pelo contrato explícito de persistência. FKs em cascata integram a exclusão atômica da conta. POST idempotente por chave/conta; recibos preservados até excluir a conta. Migrar Identity antes de Records; prontidão exige ambos atualizados.
- `tests/api/Trono.IntegrationTests`: contratos HTTP executados contra o host real em memória.
- `apps/mobile`: Flutter feature-first; Dio, Riverpod e GoRouter para identidade. Refresh persistido com flutter_secure_storage; acesso/conta somente em memória. Configuração da API por `TRONO_API_URL`, exclusivamente HTTPS.
- `infrastructure`: configuração de desenvolvimento; não aplicar em produção.
- Prévia local: Flutter Web com `main_preview.dart`, Nginx e API HTTPS pela mesma origem em `https://localhost:8443`. `docker compose up -d` coordena PostgreSQL, certificado e migrations locais em serviço de inicialização; a API aguarda `migrate` terminar com sucesso. Migração automática restrita a esse Compose Development, sem migrations no processo da API ou autorização para produção. Sessão da prévia somente em memória. Native `main.dart`/Keychain/Keystore permanecem separados. Decisão em ADR-0008; operação em `docs/operations/local-preview.md`.

Criar módulos de domínio somente conforme histórias implementadas. Registros são privados; operações sociais e recompensas ainda não existem e dependerão de autorização no servidor. A feature mobile `records` guarda dados somente em memória e envia escritas uma vez, após verificação segura da sessão.

## Validação e continuidade

Aplicar RED → GREEN antes de comportamento novo. Comandos e documentos em [AGENTS_references.md](AGENTS_references.md). Evidências e limitações da fundação e da prontidão em `docs/testing/foundation.md` e `docs/testing/readiness.md`.

Identity possui limpeza de sessões por prazo absoluto, em lotes e com opt-in explícito, desativada por padrão. Configuração e limites em `docs/operations/session-retention.md`; nenhuma base persistente foi limpa pelo agente.

Após autorização do usuário em 2026-11-22, o PostgreSQL local persistente foi criado pelo Compose e recebeu as quatro migrations de Identity e `InitialPrivateRecords`. Banco `trono` em loopback, sem contas/registros, pronto para desenvolvimento; `/health/ready` foi validado como `200 Healthy`. Credencial somente no `.env` ignorado pelo Git. A aplicação foi local, sem operação em produção ou habilitação da limpeza de sessões. Estado e reprodução em `docs/operations/local-database.md`.

Próximos passos incluem cronômetro resiliente de F2, validação nativa, confirmação/recuperação e preparação operacional para ambientes compartilhados. Novos módulos com dados de usuário devem estender a exclusão antes de sua disponibilização. Nunca tratar cadastro, UUID, tela inicial ou healthcheck como credencial.
