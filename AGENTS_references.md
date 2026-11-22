# Referências de implementação

- [Política operacional](AGENT.md)
- [Estado real e arquitetura](AGENTS.md)
- [Planejamento](TRONO_PLANEJAMENTO_COMPLETO.md)
- [Execução local](README.md)
- [Decisão de fundação](docs/decisions/ADR-0001-foundation.md)
- [Contrato de saúde e prontidão](docs/api/health.md)
- [Validação da prontidão](docs/testing/readiness.md)
- [Validação F0](docs/testing/foundation.md)
- [Decisão de cadastro](docs/decisions/ADR-0002-registration.md)
- [Contrato de cadastro](docs/api/registration.md)
- [Validação do cadastro](docs/testing/registration.md)
- [Módulo Identity](apps/api/src/Trono.Modules.Identity/AGENTS.md)
- [Decisão de sessão](docs/decisions/ADR-0003-sessions.md)
- [Contratos de sessão e própria conta](docs/api/sessions.md)
- [Validação de sessões](docs/testing/sessions.md)
- [Identidade mobile](apps/mobile/lib/features/auth/AGENTS.md)
- [Decisão de identidade mobile](docs/decisions/ADR-0004-mobile-identity.md)
- [Validação mobile/API](docs/testing/mobile-identity.md)
- [Contrato de exclusão](docs/api/account-deletion.md)
- [Decisão de exclusão](docs/decisions/ADR-0005-account-deletion.md)
- [Validação de exclusão](docs/testing/account-deletion.md)
- [Decisão de retenção de sessões](docs/decisions/ADR-0006-session-retention.md)
- [Operação da limpeza de sessões](docs/operations/session-retention.md)
- [Validação da limpeza](docs/testing/session-retention.md)
- [Contrato do próprio perfil](docs/api/own-profile.md)
- [Validação do próprio perfil](docs/testing/own-profile.md)
- [Contrato das preferências](docs/api/own-settings.md)
- [Validação das preferências](docs/testing/own-settings.md)

## Comandos previstos para os projetos da fundação

```sh
dotnet restore apps/api/Trono.slnx
dotnet tool restore
dotnet build apps/api/Trono.slnx --no-restore
dotnet test apps/api/Trono.slnx --no-build
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore
cd apps/mobile
flutter pub get
dart format --output=none --set-exit-if-changed lib test test_live
flutter analyze
flutter test
flutter build apk --debug
# Na raiz, com Docker/OpenSSL/.NET/Flutter disponíveis:
# python3 scripts/test_mobile_api.py
```

Os resultados efetivos, incluindo comandos bloqueados, pertencem ao relatório de validação; esta lista não declara aprovação.

Os testes de cadastro iniciam PostgreSQL via Testcontainers: Docker precisa estar ativo. Migrations são aplicadas automaticamente apenas ao banco descartável dos testes, nunca no startup da API. Ferramenta EF fixada em `.config/dotnet-tools.json`.
