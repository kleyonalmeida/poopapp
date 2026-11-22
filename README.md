# TRONO

Fundação do aplicativo Flutter Android/iOS e da API ASP.NET Core. O escopo de produto está no [plano mestre](TRONO_PLANEJAMENTO_COMPLETO.md); o estado implementado está em [AGENTS.md](AGENTS.md).

## Pré-requisitos

- .NET SDK 10.0.301 (fixado em `global.json`).
- Flutter 3.44.2 e Dart 3.12.2 (`apps/mobile/.flutter-version`).
- Android SDK e Java 17 para compilar Android; macOS/Xcode para iOS.
- Docker com Compose para executar PostgreSQL/PostGIS e a API em contêineres.

O Docker também é necessário para os testes de cadastro: Testcontainers cria um banco isolado e aplica a migration. O banco de desenvolvimento não é usado pela suíte.

## API

```sh
dotnet restore apps/api/Trono.slnx
dotnet build apps/api/Trono.slnx --no-restore
dotnet test apps/api/Trono.slnx --no-build
dotnet run --project apps/api/src/Trono.Api --urls http://127.0.0.1:5080
```

Em outro terminal:

```sh
curl --fail http://127.0.0.1:5080/health/live
```

`/health/live` retorna `Healthy` quando o processo HTTP responde. `/health/ready` retorna `200` quando o PostgreSQL de Identity está acessível e as migrations da versão estão aplicadas, ou `503` se a dependência estiver indisponível. HTTP em loopback é destinado às sondas de desenvolvimento. Identidade e `/api/v1/me` exigem HTTPS. [Contrato das sondas](docs/api/health.md)

O endpoint `POST /api/v1/auth/register` cria conta por e-mail e senha e retorna apenas o UUID, sem autenticar. Configuração da conexão, migration e execução HTTPS estão no [contrato de cadastro](docs/api/registration.md).

Login (`POST /api/v1/auth/login`) emite acesso de até 15 minutos e refresh de prazo absoluto de 7 dias. Refresh troca ambos os tokens, logout revoga a sessão corrente e `GET /api/v1/me` lê apenas a própria conta. Consulte os [contratos de sessão](docs/api/sessions.md), incluindo a regra de replay: renovações devem ser serializadas no cliente.

`DELETE /api/v1/me/account` exclui a própria conta e seus dados de identidade/sessão, com confirmação da senha atual. No app, Minha conta → Excluir minha conta exige consentimento e diálogo final. Não há undo ou retry automático do DELETE. [Contrato e escopo](docs/api/account-deletion.md).

## Mobile

```sh
cd apps/mobile
flutter pub get --enforce-lockfile
dart format --output=none --set-exit-if-changed lib test test_live
flutter analyze
flutter test
flutter run --dart-define=TRONO_API_URL=https://api.example.com
# Compilação local Android:
flutter build apk --debug
```

A tela inicial abre cadastro e login. Após autenticar, o app consulta a própria conta, restaura sessão por refresh, permite atualizar e sair. Cadastro volta ao login sem autenticação automática. Só o refresh é persistido no armazenamento seguro, vinculado à origem configurada. Senha não é persistida; acesso e conta ficam em memória.

`TRONO_API_URL` deve ser uma origem HTTPS (sem path/query/fragment/credenciais), com certificado aceito pelo dispositivo e banco migrado. O endereço de exemplo deve ser substituído. Sem configuração, o app mantém a tela inicial e informa indisponibilidade do acesso à conta; não tenta HTTP nem desabilita validação TLS. Os projetos Android/iOS ainda precisam de builds e validação em dispositivo.

No Windows/WSL, execute Flutter/Android Studio no sistema onde o SDK e o emulador estão configurados. Valide conectividade e certificado do endpoint HTTPS no dispositivo; o acesso ao host/emulador e encaminhamento ao WSL dependem da configuração local. Não usar o HTTP de smoke do Compose para autenticação.

Teste reproduzível do cliente real com API/PostgreSQL descartáveis, TLS verificado e limpeza automática (Linux/WSL com Docker, OpenSSL, .NET e Flutter):

```sh
python3 scripts/test_mobile_api.py
# SDK Flutter fora do PATH:
python3 scripts/test_mobile_api.py --flutter /caminho/para/flutter
```

O teste usa vault em memória para exercitar o protocolo; Keychain/Keystore precisam de teste nativo. Consulte [evidências de identidade mobile](docs/testing/mobile-identity.md).

## Contêineres de desenvolvimento

```sh
cp .env.example .env
# Edite .env e defina uma senha local em POSTGRES_PASSWORD.
docker compose config --quiet
docker compose up --build --detach --wait
curl --fail http://127.0.0.1:5080/health/live
docker compose exec postgres psql -U trono -d trono -c 'SELECT PostGIS_Version();'
docker compose down
```

As portas estão vinculadas a loopback. O banco mantém dados no volume `trono-postgres`; desligar os serviços não remove esse volume. O Compose fornece a conexão de Identity para a API, mas não aplica migrations nem configura TLS. Aplicação da migration local e uso do cadastro estão documentados no [contrato](docs/api/registration.md).

A limpeza de sessões expiradas está implementada e **desativada por padrão**. Habilitação explícita, tamanho de lote e intervalo estão no [guia operacional](docs/operations/session-retention.md). Não remove usuários nem famílias ainda no prazo absoluto; não foi executada sobre bancos persistentes nesta entrega.

## Verificação e próximos passos

O workflow `.github/workflows/ci.yml` configura build/testes da API, análise/testes/build Android e smoke test dos contêineres. A execução remota desse workflow permanece pendente até haver um repositório GitHub configurado.

Consulte [preferências](docs/testing/own-settings.md), [próprio perfil](docs/testing/own-profile.md), [F0](docs/testing/foundation.md), [sessões](docs/testing/sessions.md), [mobile/API](docs/testing/mobile-identity.md), [exclusão](docs/testing/account-deletion.md) e [retenção](docs/testing/session-retention.md). A validação nativa, confirmação/recuperação, preferências adicionais e preparação operacional para ambientes compartilhados seguem pendentes.
