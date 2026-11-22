# ADR-0004 — Integração mobile de identidade

Status: implementada e validada em VM/widgets; validação nativa pendente.

## Decisões

Adotar Dio 5.11.1 para HTTP, Riverpod 3.4.3 para estado/injeção, GoRouter 17.5.0 para navegação e flutter_secure_storage 11.2.0 para refresh. Versões e SDKs foram conferidos no registry dos próprios pacotes e restaurados com Flutter 3.44.2/Dart 3.12.2. Riverpod/Dio usam MIT; GoRouter/secure storage, BSD de três cláusulas. Repositórios ativos e licenças conferidos antes do uso.

Riverpod 3.5/GoRouter 18 migram para o pacote separado material_ui. Foram escolhidas as últimas versões sem essa dependência, compatíveis com os widgets Material existentes, preservando o escopo desta entrega. Não houve atualização ampla de dependências antigas. O lockfile versiona todas as resoluções.

Separar domínio/coordenação, adapters e apresentação por feature. O provider de sessão mantém um único coordenador por app; a fila serializa login, restauração, consultas e logout. A versão da operação no controller impede que uma resposta antiga volte a mostrar dados depois da saída. Providers não executam refresh em build com retry automático.

## Credenciais e falhas

Somente refresh/origem/expiração são persistidos, em um único valor de storage nativo. Senhas não são persistidas; acesso e conta ficam em memória. Origem é configurada por TRONO_API_URL e deve ser HTTPS, sem path, query, fragment ou userinfo. Credenciais salvas para outra origem são apagadas sem envio.

Consumir o refresh local antes de enviá-lo evita replay após resposta perdida ou encerramento do processo. Uma falha incerta sacrifica a recuperação automática e exige novo login, coerente com a política estrita de replay do servidor. Não há retry de escritas nem redirects HTTP. Para acesso, antecipar renovação em 30 segundos ou reagir a um 401, no máximo uma vez por operação. O servidor continua sendo a autoridade de validade/autorização.

Após emissão, salvar o novo refresh antes de apresentar a conta. Se persistência falhar, ocultar a conta e tentar revogar o acesso emitido. Falha em remover credenciais não é tratada como logout completo. Logout offline limpa o que puder localmente e informa que a saída no servidor não foi confirmada.

## Plataformas e limites

Android declara INTERNET, bloqueia cleartext e desabilita backup. O minSdk 24 já definido pelo Flutter atende o requisito do plugin. iOS usa Keychain desbloqueado e limitado ao dispositivo, sem sincronização, e entitlements de seu próprio bundle nas três configurações. Não foram adicionadas permissões de biometria, câmera ou localização.

Mocks do plugin verificam serialização/remoção, sem provar proteção nativa. A integração cliente/API usa HTTPS real com certificado temporário confiado somente no teste, PostgreSQL descartável e limpeza dos recursos. Nenhum callback aceita certificados inválidos. Builds/assinatura e Keychain/Keystore em dispositivo permanecem pendentes.

## Fontes

- [Dio](https://pub.dev/packages/dio/versions/5.11.1)
- [Riverpod](https://pub.dev/packages/flutter_riverpod/versions/3.4.3)
- [GoRouter](https://pub.dev/packages/go_router/versions/17.5.0)
- [flutter_secure_storage e configuração nativa](https://pub.dev/packages/flutter_secure_storage/versions/11.2.0)
