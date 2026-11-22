# TRONO — estado da implementação

Leia primeiro [AGENT.md](AGENT.md). O [plano mestre](TRONO_PLANEJAMENTO_COMPLETO.md) descreve o produto proposto, não funcionalidades entregues.

## Escopo atual

F0 possui API ASP.NET Core .NET 10 com sondas separadas de processo e prontidão, aplicativo Flutter e CI preparado. F1 possui cadastro, login, refresh rotativo, logout, própria conta e exclusão confirmada por senha no backend/mobile. F1 também inclui consulta e edição do apelido opcional e da preferência de notificações push, desativada por padrão. Confirmação/recuperação, preferências adicionais, avatar, registros, publicação social e recompensas seguem pendentes.

API e Flutter passaram em RED → GREEN. A regressão backend foi ampliada para 71 testes aprovados com limpeza limitada de sessões expiradas. Nas fatias de perfil e preferências, o build Release e as verificações HTTP em processo passaram. A execução mais recente de Flutter aprovou 45 testes com dependências locais alternativas. As verificações e os bloqueios da regressão padrão constam em `docs/testing/own-profile.md` e `docs/testing/own-settings.md`. A regressão anterior de 41 testes mobile está preservada nos relatórios históricos. F0/F1 permanecem parciais, com execução em dispositivo, Keychain/Keystore e CI remoto pendentes.

## Arquitetura

- `apps/api/src/Trono.Api`: composition root, saúde, ProblemDetails e composição de Identity.
- `apps/api/src/Trono.Modules.Identity`: ciclo de sessão em `Domain`, casos de uso/ports/validadores em `Application`, Identity/EF e tokens em `Infrastructure`, contratos HTTP/auth handler em `Presentation`. Schema `identity`, migrations explícitas, HTTPS e limites independentes de cadastro/login/refresh. Cadastro não autentica.
- `tests/api/Trono.IntegrationTests`: contratos HTTP executados contra o host real em memória.
- `apps/mobile`: Flutter feature-first; Dio, Riverpod e GoRouter para identidade. Refresh persistido com flutter_secure_storage; acesso/conta somente em memória. Configuração da API por `TRONO_API_URL`, exclusivamente HTTPS.
- `infrastructure`: configuração de desenvolvimento; não aplicar em produção.

Criar módulos de domínio somente conforme histórias implementadas. Registros serão privados por padrão; operações sociais e recompensas dependerão de autorização no servidor.

## Validação e continuidade

Aplicar RED → GREEN antes de comportamento novo. Comandos e documentos em [AGENTS_references.md](AGENTS_references.md). Evidências e limitações da fundação e da prontidão em `docs/testing/foundation.md` e `docs/testing/readiness.md`.

Identity possui limpeza de sessões por prazo absoluto, em lotes e com opt-in explícito, desativada por padrão. Configuração e limites em `docs/operations/session-retention.md`; nenhuma base persistente foi limpa pelo agente.

Próximos passos incluem validação nativa, confirmação/recuperação, preferências adicionais e preparação operacional para ambientes compartilhados. Novos módulos com dados de usuário devem estender a exclusão antes de sua disponibilização. Nunca tratar cadastro, UUID, tela inicial ou healthcheck como credencial.
