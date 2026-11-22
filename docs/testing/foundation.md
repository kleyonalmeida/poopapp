# Validação da fundação F0

Data: 2026-11-12. Status geral: **PARCIAL**. Este documento registra execuções reais, não substitui a execução do CI.

## Critérios desta entrega

- API inicia e responde ao healthcheck público sem expor dados internos.
- Tela Flutter apresenta TRONO e privacidade por padrão.
- Conteúdo legível com texto em escala 2 e viewport 320 × 568, sem overflow.
- SDKs fixados, comandos documentados e CI preparado para API, mobile e infraestrutura.
- Build nativo, PostGIS real e demonstração em emulador precisam de ambiente apropriado.

## Evidência RED → GREEN

| Área | RED observado antes da implementação | GREEN observado |
|---|---|---|
| API | `Liveness_is_public_and_returns_only_process_status`: esperado `OK`, recebido `NotFound` | 1 teste aprovado após `AddHealthChecks` e `MapHealthChecks` |
| Flutter | Teste de apresentação não encontrou o texto `TRONO`; teste de leitura não encontrou a mensagem de compartilhamento no contador do template | 2 testes de widgets aprovados após implementação da tela inicial |

Erros iniciais de sockets/cache/SDK foram corrigidos antes dessas evidências; não foram considerados RED de comportamento. Os asserts foram preservados.

## Comandos executados

O ambiente impede escrita nos caches originais e restringe sockets. Para a API, foi usado `DOTNET_CLI_HOME=/tmp/trono-dotnet`. O SDK Flutter instalado foi copiado para `/tmp/trono-flutter-sdk`; os pacotes necessários e seus hashes, para `/tmp/trono-pub-cache`, com `HOME=/tmp/trono-flutter-home`. Esses caminhos são adaptações locais, não dependências do projeto.

Restauração .NET offline:

```sh
dotnet restore apps/api/Trono.slnx --source /home/kley/.nuget/packages -p:NuGetAudit=false --disable-parallel -m:1
```

Resultado: aprovado, sem warnings ou erros. A auditoria de vulnerabilidades não foi executada offline; ela permanece habilitada por padrão na restauração normal do CI.

Backend:

```sh
dotnet build apps/api/Trono.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test apps/api/Trono.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build apps/api/Trono.slnx --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet test apps/api/Trono.slnx --no-build --configuration Release -m:1
```

Resultado: builds Debug e Release aprovados sem warnings ou erros; 1 teste HTTP aprovado em cada configuração; formatação aprovada. Testes e análise do workspace exigiram execução fora do sandbox por comunicação local do runner/build host.

Mobile (em `apps/mobile`, usando os caches temporários):

```sh
flutter pub get --offline
dart format lib test
flutter analyze
flutter test
flutter pub get --offline --enforce-lockfile
dart format --output=none --set-exit-if-changed lib test
flutter build apk --debug --no-pub
```

Resultado: dependências restauradas, inclusive com `--enforce-lockfile`; análise sem problemas; 2 testes aprovados; checagem final de formatação sem alterações. O build APK foi tentado e bloqueado: `No Android SDK found`. Testes exigiram liberação de comunicação em localhost. O lockfile preserva os hashes do cache original.

Configuração: `docker-compose.yml`, `.github/workflows/ci.yml` e `apps/mobile/pubspec.yaml` foram parseados com `yaml.safe_load` sem erros. Isso não equivale à validação semântica do Compose nem à execução do GitHub Actions.

## Pendências para encerrar F0

- **Resolvido na continuação:** Docker Desktop estava parado. Foi iniciado, disponibilizando o daemon e a integração WSL. Compose validado semanticamente; imagem da API compilada, serviços iniciados com `--wait`, `/health/live` retornou `Healthy` e `SELECT PostGIS_Version()` retornou `3.5 USE_GEOS=1 USE_PROJ=1 USE_STATS=1`. A imagem foi reconstruída e revalidada após adicionar Identity.
- Configurar Android SDK, executar build debug e demonstrar o aplicativo em emulador ou aparelho. Nenhum dispositivo foi usado nesta entrega.
- Build/teste iOS exige macOS/Xcode; não foi executado neste ambiente Linux. Assinatura de distribuição não foi configurada.
- Executar o workflow remoto. O diretório `.git` disponível está vazio e somente leitura; `git status` não reconhece um repositório. Não houve commit ou push.
- Ainda não há `integration_test` mobile nem relações entre módulos de domínio para validar. A continuação iniciou F1 com migration Identity e testes HTTP/persistência reais, documentados em [registration.md](registration.md).

## Segurança e privacidade

Nenhum dado pessoal é coletado pela tela. O healthcheck retorna apenas `Healthy` e não consulta dados de usuários. Compose exige senha fornecida localmente, usa volume persistente e vincula as portas a loopback. O smoke da infraestrutura usou projeto isolado `trono-f0-check` e senha temporária em `/tmp`, sem valor no repositório. Não foram criados banco de produção, endpoint social, upload ou regras de recompensas. Cadastro e contas fictícias de teste da continuação são descritos em [registration.md](registration.md); autorização de dados ainda não foi implementada.
