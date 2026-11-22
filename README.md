# TRONO

Aplicativo Flutter Android/iOS e API ASP.NET Core, com identidade e a primeira fatia de registros privados do TRONO. O escopo de produto está no [plano mestre](TRONO_PLANEJAMENTO_COMPLETO.md); o estado implementado está em [AGENTS.md](AGENTS.md).

## Abrir a interface no localhost

Com Docker Compose, a partir da raiz:

```sh
docker compose up -d
```

Abra **https://localhost:8443** e autorize o certificado de desenvolvimento local no navegador. O Compose compila as imagens ausentes, prepara o certificado, aguarda o PostgreSQL, aplica as migrations locais e só então inicia a API e a interface. Não precisa instalar Python/Flutter/.NET/Android SDK no host para esta prévia.

Em uma cópia nova, prepare `.env` a partir de `.env.example` e defina `POSTGRES_PASSWORD` uma vez. Neste ambiente o arquivo já está configurado. Depois de alterar o código, use `docker compose up -d --build` para reconstruir as imagens.

As telas são as mesmas do app mobile. Use a visualização de dispositivo do navegador em uma largura próxima de 390 px. A sessão da prévia fica somente em memória: recarregar exige novo login; Android/iOS continuam com armazenamento seguro nativo. [Comandos, certificado e limites](docs/operations/local-preview.md).

## Pré-requisitos

Para desenvolvimento e builds fora da prévia Docker:

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

`/health/live` retorna `Healthy` quando o processo HTTP responde. `/health/ready` retorna `200` quando o PostgreSQL está acessível e as migrations de Identity e Records da versão estão aplicadas, ou `503` se alguma dependência estiver indisponível. HTTP em loopback é destinado às sondas de desenvolvimento. Identidade, `/api/v1/me` e registros exigem HTTPS. [Contrato das sondas](docs/api/health.md)

O endpoint `POST /api/v1/auth/register` cria conta por e-mail e senha e retorna apenas o UUID, sem autenticar. Configuração da conexão, migration e execução HTTPS estão no [contrato de cadastro](docs/api/registration.md).

Login (`POST /api/v1/auth/login`) emite acesso de até 15 minutos e refresh de prazo absoluto de 7 dias. Refresh troca ambos os tokens, logout revoga a sessão corrente e `GET /api/v1/me` lê apenas a própria conta. Consulte os [contratos de sessão](docs/api/sessions.md), incluindo a regra de replay: renovações devem ser serializadas no cliente.

`DELETE /api/v1/me/account` exclui a própria conta, dados de identidade/sessão e registros privados/recibos, com confirmação da senha atual. No app, Minha conta → Excluir minha conta exige consentimento e diálogo final. Não há undo ou retry automático do DELETE. [Contrato e escopo](docs/api/account-deletion.md).

Minha conta → Meus registros permite registrar uma cagada, escolher descrição humorística, acrescentar história/duração opcionais e consultar, editar ou excluir no histórico privado. Ainda não há cronômetro ou publicação. O servidor impede duplicação de criação e a exclusão da conta também elimina registros/recibos. [Contrato](docs/api/records.md).

Identity e Records usam o mesmo PostgreSQL, com schemas e históricos de migrations próprios. O Compose de desenvolvimento coordena a aplicação. Para executar a API fora dele, aplicar Identity conforme o contrato de cadastro e depois Records:

```sh
dotnet ef database update --context RecordsDbContext \
  --project apps/api/src/Trono.Modules.Records \
  --startup-project apps/api/src/Trono.Api
```

Não executar esse comando em produção sem plano/autorização. `/health/ready` só retorna saudável com ambos os módulos migrados.

O banco de desenvolvimento local foi preparado e migrado com autorização em 2026-11-22. Credencial no `.env` local ignorado pelo Git; serviço PostgreSQL em `127.0.0.1:5432`. Consulte o [estado do banco local](docs/operations/local-database.md), incluindo a configuração necessária para executar a API no host.

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
docker compose up -d
# Para reconstruir após alterações de código:
docker compose up -d --build
curl --fail http://127.0.0.1:5080/health/live
docker compose exec postgres psql -U trono -d trono -c 'SELECT PostGIS_Version();'
docker compose down
```

As portas estão vinculadas a loopback. O banco e o certificado mantêm volumes próprios; desligar os serviços sem `--volumes` preserva ambos. O gateway serve interface/API em HTTPS 8443 e verifica TLS até a API. HTTP 5080 permanece para sondas e rejeita identidade/registros. O serviço de inicialização `migrate`, exclusivo de Development, aplica Identity antes de Records; se falhar, API/interface não iniciam. O processo da API não aplica migrations no próprio startup. [Operação da prévia](docs/operations/local-preview.md).

A limpeza de sessões expiradas está implementada e **desativada por padrão**. Habilitação explícita, tamanho de lote e intervalo estão no [guia operacional](docs/operations/session-retention.md). Não remove usuários nem famílias ainda no prazo absoluto; não foi executada sobre bancos persistentes nesta entrega.

## Verificação e próximos passos

O workflow `.github/workflows/ci.yml` configura build/testes da API, análise/testes/build Android e smoke da prévia em projeto Compose descartável. `python3 scripts/test_docker_preview.py` verifica interface, TLS, API e PostGIS sem usar o banco persistente. A execução remota desse workflow permanece pendente até haver um repositório GitHub configurado.

Consulte a [validação da prévia Docker](docs/testing/local-preview.md), a [regressão de registros privados](docs/testing/private-records.md) e os relatórios históricos de [preferências](docs/testing/own-settings.md), [próprio perfil](docs/testing/own-profile.md), [F0](docs/testing/foundation.md), [sessões](docs/testing/sessions.md), [mobile/API](docs/testing/mobile-identity.md), [exclusão](docs/testing/account-deletion.md) e [retenção](docs/testing/session-retention.md). Cronômetro resiliente, validação nativa, confirmação/recuperação e preparação operacional para ambientes compartilhados seguem pendentes.
