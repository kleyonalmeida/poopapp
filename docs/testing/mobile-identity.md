# Validação de identidade mobile — F1

Data: 2026-11-12. Status geral: **PARCIAL**, com código/VM/widgets validados e builds/armazenamento nativos pendentes.

## Entregue

- Home com acesso a cadastro e login; formulários com validação, confirmação de senha, estado de envio e mensagens seguras.
- Cadastro retorna ao login sem autenticar automaticamente. Login carrega apenas a própria conta; atualização e logout usam os contratos reais.
- Riverpod para estado/injeção, GoRouter com rota privada `/conta` e Dio com origem HTTPS explícita, timeouts e redirects desabilitados.
- Refresh/origem/expiração em uma única entrada de flutter_secure_storage; acesso/conta somente em memória. Sem persistência de senha, SharedPreferences ou fallback em texto puro.
- Restauração, login, leituras e logout serializados. Refresh salvo é consumido antes do envio; resposta incerta exige novo login, sem replay automático.
- Dados privados somem antes de aguardar logout; respostas antigas não restauram a conta na UI. Logout offline informa que a revogação remota não foi confirmada.
- Configuração Android/iOS para rede segura e storage, script de integração real cliente/API e etapa correspondente preparada no CI.

Arquivos: `apps/mobile/lib/features/auth`, `lib/app`, `lib/main.dart`, Home, pubspec/lock, manifests/entitlements e testes; `scripts/test_mobile_api.py`, workflow e documentação. Não houve alteração nos contratos ou no código backend nesta fatia.

## RED → GREEN

1. O teste inicial esperava o botão Entrar na Home e falhou porque ele não existia.
2. Os 11 cenários de coordenação e 6 de adapters foram escritos contra contratos/tipos mínimos, sem regras implementadas, e falharam por ausência dos métodos. Não foram usadas falhas de import/compilação como RED.
3. Oito cenários de formulário/login/logout falharam pela ausência da navegação/formulários. Após implementar os fluxos, a regressão identificou uma fila iniciada no contexto assíncrono da construção, que deixava a restauração pendente no teste de widget. A fila passou a começar na primeira operação; os asserts foram preservados.
4. Um assert mais específico reproduziu a falha incerta de refresh reportada como erro recuperável de rede; foi ajustada para exigir novo login, pois a credencial local já havia sido consumida.
5. Formatação/lint foram corrigidos sem afrouxar regras. Foi acrescentada regressão de navegação direta para a rota privada, antes e depois da sessão.

Resultado final: **29 testes de unidade/widgets aprovados**, incluindo os 2 testes iniciais da Home, sem ignorar testes. As entradas de senha são preservadas e os campos de cadastro não aceitam confirmação divergente. Texto ampliado em viewport 320 × 568 permanece utilizável.

## Casos de sessão e adapters

Verificados: login sem trim da senha; cadastro sem login automático; persistência somente do refresh; descarte de origem alheia/credencial expirada sem envio; consumo local antes da rede; uma renovação para duas operações simultâneas; reação a 401 uma única vez; ausência de retry após falha incerta; falha de escrita/remoção do storage; logout offline; saída durante refresh/leitura pendente; limpeza da conta e bloqueio de navegação privada.

Adapters verificam rotas, DTOs, Bearer apenas no cabeçalho, ausência de token na URL, HTTPS sem userinfo/path/query/fragment, redirects desabilitados, respostas inválidas, erros sem corpo/detalhes de Dio e remoção apenas da chave própria. Tokens e refreshes têm toString redigido.

Os testes do plugin usam `FlutterSecureStorage.setMockInitialValues`: comprovam serialização e chamadas da aplicação, **não** criptografia, biometria, Keychain/Keystore ou durabilidade real no dispositivo.

## Integração real por TLS

```sh
python3 scripts/test_mobile_api.py
```

O script cria PostgreSQL/PostGIS descartável, gera certificado temporário para localhost, restaura/compila a API .NET em Release, aplica suas migrations somente nesse banco e inicia Kestrel HTTPS. Em seguida executa `apps/mobile/test_live/auth_api_live_test.dart` com o cliente Dio real. O certificado é confiado explicitamente apenas no contexto de teste; não há callback que aceite certificado inválido.

Resultado: **1 teste de integração real aprovado**, cobrindo cadastro, login, me, recriação do coordenador, rotação na restauração, logout e rejeição do refresh após sair. Usa vault em memória para isolar o protocolo. O processo da API, contêiner/volume de teste e arquivos temporários foram encerrados/removidos no finally. Nenhum banco persistente ou ambiente externo foi alterado.

A suíte backend de 44 testes pertence à entrega anterior e não foi reexecutada nesta fatia sem mudança de backend. A API foi compilada novamente e seus endpoints foram usados pelo teste Dart real. Não houve deploy, commit ou push; a execução remota do workflow permanece pendente.

## Comandos executados

SDK Flutter 3.44.2 copiado para `/tmp/trono-flutter-sdk`, `HOME=/tmp/trono-flutter-home` e `PUB_CACHE=/tmp/trono-pub-cache`, preservando o SDK/cache original. Testes e análise precisaram de execução fora do sandbox por sockets locais.

```sh
flutter pub get
flutter pub get --offline --enforce-lockfile
dart format lib test test_live
flutter analyze
flutter test
dart format --output=none --set-exit-if-changed lib test test_live
flutter build apk --debug --no-pub
```

Resultados: dependências/lock aprovados; análise sem problemas; 29 testes aprovados; checagem de formatação sem alterações. Build APK bloqueado: `No Android SDK found`.

Integração local foi executada assim, com caminhos temporários específicos deste ambiente:

```sh
DOTNET_CLI_HOME=/tmp/trono-dotnet \
NUGET_PACKAGES=/home/kley/.nuget/packages \
NUGET_HTTP_CACHE_PATH=/tmp/trono-nuget-http \
HOME=/tmp/trono-flutter-home PUB_CACHE=/tmp/trono-pub-cache \
python3 scripts/test_mobile_api.py --flutter /tmp/trono-flutter-sdk/bin/flutter
```

Python, YAML do CI, manifesto XML e entitlements foram parseados; as três configurações Xcode referenciam Runner.entitlements. Essas verificações não equivalem a um build nativo. O minSdk 24 do Flutter já atende o requisito do plugin. Android declara INTERNET, bloqueia cleartext e desabilita backup; iOS configura Keychain limitado ao dispositivo desbloqueado, sem sincronização.

## Pendências e limites

- Configurar Android SDK, compilar APK e executar em emulador/aparelho; validar rede/certificado e Keystore de verdade.
- macOS/Xcode/assinatura para build iOS, entitlements e Keychain real, inclusive bloqueio/desbloqueio e reinício.
- Verificar crash/encerramento do processo durante rotação no dispositivo. A ordem de consumo é testada em unidade; durabilidade nativa não foi comprovada.
- TLS real já foi validado em VM, mas isso não prova confiança do certificado no dispositivo. O app exige `TRONO_API_URL` e certificado aceito pela plataforma; não relaxar validação TLS.
- Ainda não existe `integration_test` em dispositivo. `test_live` é integração VM/servidor e não deve ser relatado como E2E Android/iOS.
- Confirmação/recuperação, perfil/preferências/exclusão e limpeza operacional de sessões continuam pendentes de F1.

Sem configuração de API, a Home funciona e o formulário informa indisponibilidade, sem enviar credenciais. Se uma renovação perder a resposta, o refresh antigo não é repetido e o usuário precisa entrar novamente. Se a saída não alcançar o servidor, a sessão remota só é confirmada como revogada quando a API responde; o app não declara essa garantia ao ficar offline.
