# Registros privados e regressão oficial

Data: 2026-11-22. Primeira fatia manual de F2 implementada e validada em PostgreSQL/HTTPS/VM/widgets. F0/F1/F2 continuam parciais quanto à execução nativa, CI remoto e histórias restantes.

## Consolidação da base

O ambiente inicialmente oferecia .NET 10.0.114, Flutter 3.47.1, Docker Desktop parado e nenhum Android SDK localizado. Foram disponibilizados .NET 10.0.301 e Flutter 3.44.2/Dart 3.12.2 em `/tmp/trono-dotnet-sdk` e `/tmp/trono-flutter-sdk`, preservando as versões do projeto. Docker Desktop foi iniciado, sem alteração das permissões do socket. Restore normal NuGet com auditoria e `flutter pub get --enforce-lockfile` passaram. Não foram usados adapters substitutos ou versões alternativas de dependências nesta execução.

A primeira regressão backend encontrou 81 aprovações e uma falha: `SessionTests` ainda limitava `/me` a três campos, anterior às entregas de apelido e push. O teste passou a verificar exatamente os cinco nomes permitidos e os valores iniciais dos dois campos, preservando a verificação de privacidade. O formatter também encontrou BOM em duas migrations anteriores; somente a codificação foi normalizada, sem mudar SQL ou operações das migrations.

Antes de Records, os 47 testes Flutter oficiais, a análise e a integração real de identidade passaram. Uma execução intermediária .NET teve duas falhas `ObjectDisposedException` em testes de startup inválido, durante verificações concorrentes. A execução completa final com build em um processo passou nos 92 cenários, mantendo os asserts de `OptionsValidationException`. Houve também falha dos nós paralelos do MSBuild; o build com `-m:1 -p:UseSharedCompilation=false` passou. Esses erros de ferramenta/execução não foram contados como RED de funcionalidade.

## RED → GREEN

- Nove cenários HTTP novos falharam pela ausência de `/api/v1/records`: 404 no lugar de 401, 400 e 201, incluindo CRUD, validação e reenvio concorrente. Depois do módulo e da migration gerada pelo EF, os cenários passaram em PostgreSQL real descartável.
- Três testes de widgets falharam porque o acesso aos registros, as telas e a guarda de rota ainda não existiam. Após implementar histórico/editor, passaram CRUD, confirmação de exclusão e resposta incerta de salvamento.
- O teste de escrita autenticada falhou com a operação ainda sem implementação. Depois, comprovou um único envio, nenhuma renovação/repetição após 401 e exigência de novo login.
- A revisão acrescentou teste de exclusão da conta concorrente à criação, verificação de logs, descarte de resposta de histórico depois do logout e viewport 320 × 568 com texto em escala 2. Os localizadores dos testes foram ajustados para o botão de login e a lista com construção sob demanda; os critérios de privacidade/acessibilidade foram preservados.

## Resultados finais executados

| Verificação | Resultado |
|---|---|
| Restore NuGet com `NuGetAuditMode=all` | Aprovado |
| Build Release da solução | Aprovado, sem warnings/erros |
| Regressão backend completa | 92 aprovados, zero falhas/ignorados |
| `dotnet format --verify-no-changes` | Aprovado |
| Restore Flutter com lockfile oficial | Aprovado |
| Dart format e Flutter analyze | Sem alterações/problemas |
| Regressão Flutter completa | 53 aprovados |
| Cliente real por TLS verificado | 1 teste aprovado, incluindo CRUD e reenvio de Records |
| Imagem `trono-private-records:check` | Build/publish Docker aprovados, sem publicação em registry |
| EF Records: modelo x snapshot | Sem alterações pendentes |
| EF Identity: modelo x snapshot | Sem alterações pendentes |
| SQL de aplicação/reversão Records | Gerado e revisado |
| Build APK | Bloqueado: `No Android SDK found` |

Os comandos principais, a partir da raiz:

```sh
dotnet restore apps/api/Trono.slnx -p:NuGetAuditMode=all
dotnet tool restore
dotnet test apps/api/Trono.slnx --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore
python3 scripts/test_mobile_api.py --flutter /tmp/trono-flutter-sdk/bin/flutter
docker build --file infrastructure/api.Dockerfile --tag trono-private-records:check .
```

Nesta execução, `dotnet` nos comandos acima correspondeu a `/tmp/trono-dotnet-sdk/dotnet`; o script recebeu esse diretório no PATH e DOTNET_ROOT correspondente. Flutter/Dart foram executados pelo SDK temporário fixado:

```sh
# Em apps/mobile:
flutter pub get --enforce-lockfile
dart format --output=none --set-exit-if-changed lib test test_live
flutter analyze --no-pub
flutter test --no-pub
flutter build apk --debug --no-pub
```

As verificações precisaram de acesso de rede, sockets locais, cache de ferramentas e Docker fora do sandbox. Nenhuma versão do pubspec/lockfile/global.json foi alterada.

## Persistência, isolamento e migration

`RecordTests` verificou criação/consulta/edição/exclusão pelo titular, 404 para terceiros, lista isolada por conta, remoção explícita de campos opcionais, rejeição de edição inválida sem perder o valor anterior, validação de data/fuso/duração/catálogo/história e limites de paginação.

Dois POSTs simultâneos com a mesma chave criaram um único registro: um 201 e um 200 com o mesmo ID. Outro conteúdo com a chave foi 409; repetir após exclusão foi 410 sem recriar. A exclusão da conta removeu registros e recibos, preservando outra conta. A corrida criação/exclusão não deixou órfãos nem revelou história, senha ou acesso nos logs capturados.

`InitialPrivateRecords` foi gerada pelo EF CLI, com schema/histórico próprios, tipos UTC/UUID, índice de conta/data/ID e FKs em cascata para o contrato de ID de Identity. A aplicação ocorreu somente em bancos de Testcontainers e do script descartável. SQL de ida e volta em `/tmp/trono-private-records-up.sql` e `/tmp/trono-private-records-down.sql` foi revisado: a volta remove somente tabelas de Records e sua entrada de migration, com perda dos registros. Não foi executada reversão em banco persistente. A prontidão foi testada como 503 com Records pendente e 200 depois de ambos os módulos migrados.

## Integração e limites

O script TLS cria seu próprio PostgreSQL e certificado, migra Identity e Records, executa o adapter Dio real e encerra os recursos no finally. O fluxo confirmou registro manual, reenvio com a mesma chave, listagem, edição e exclusão, além de identidade/perfil/preferências/sessão/exclusão de conta já existentes. O vault desse teste permanece em memória.

Android SDK/dispositivo/emulador e macOS/Xcode não foram disponibilizados. A consulta `flutter devices` não concluiu a listagem detalhada e foi encerrada; não constitui validação de dispositivo. Keychain/Keystore real, builds nativos, CI remoto, revisão visual em aparelho e durabilidade nativa permanecem pendentes.

Não há cronômetro, publicação social, foto, mapa, XP ou persistência offline nesta entrega. Rascunhos existem somente enquanto o formulário está aberto; perder o processo pode perdê-los. PATCH segue última gravação válida e paginação por offset pode deslocar resultados durante alterações concorrentes. A retenção de recibos vai até excluir a conta; não há promessa de remoção de backups/logs externos. Nenhum deploy, commit, push, publicação em loja ou alteração de banco persistente foi executado.

## Operação posterior autorizada

Após a entrega descrita acima, o usuário autorizou aplicar as migrations em 2026-11-22. Foi criado o banco persistente local de desenvolvimento, aplicadas as migrations de Identity e Records e validada a prontidão `200 Healthy`. Essa operação posterior está registrada em [banco local](../operations/local-database.md); as evidências de testes descartáveis acima permanecem históricas. Nenhuma base de produção foi alterada.
