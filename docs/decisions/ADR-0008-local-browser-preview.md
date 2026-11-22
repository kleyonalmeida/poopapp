# ADR-0008 — Prévia mobile em Docker e HTTPS local

Status: adotada para desenvolvimento local. O produto permanece Flutter Android/iOS; esta prévia não substitui testes nativos.

## Decisão

Adicionar `web/index.html` e um ponto de entrada específico `lib/main_preview.dart`, reutilizando as telas existentes. O build Docker usa Flutter 3.44.2 e o lockfile oficial, sem exigir SDKs .NET/Flutter/Android no host. Recursos do engine são empacotados com `--no-web-resources-cdn` e o service worker fica desabilitado para a prévia.

Nginx serve o build e encaminha `/api/` e `/health/` pela mesma origem HTTPS. A URL padrão é `https://localhost:8443`; o cliente de prévia usa `Uri.base.origin`, validada pelo adapter HTTPS existente. Records usa a origem da sessão, evitando uma configuração paralela que impedia o uso no navegador. Não foi necessário introduzir CORS, confiar em forwarded headers ou mudar o middleware de transporte da API.

## TLS e exposição

O serviço `certificates` cria um certificado de desenvolvimento para `localhost`, `127.0.0.1` e o hostname interno `api`. Certificado e chave ficam em volume dedicado, fora da imagem e do Git. A chave possui permissão 0600 e pertence ao UID app 1654 da imagem ASP.NET; API e gateway recebem o volume somente para leitura. Certificado válido já existente é preservado. Configuração parcial/expirada falha, sem rotação silenciosa que invalidaria serviços em execução.

O gateway recebe HTTPS e também verifica o certificado do upstream HTTPS da API com `proxy_ssl_verify on`, CA explícita e hostname `api`. Kestrel lê PEM/KeyPath diretamente. HTTP 5080 continua disponível para sondas, mas identidade/registros permanecem proibidos nesse transporte. Todas as portas publicadas estão vinculadas a 127.0.0.1.

Logs de acesso e de erro por requisição do Nginx ficam desativados: mensagens de falha do upstream incluem o alvo completo, que pode conter uma credencial indevidamente enviada em query. A regressão reproduziu esse vazamento com uma marca fictícia antes da correção. Prontidão, códigos HTTP e logs técnicos da API continuam disponíveis; em troca, o gateway local não mantém detalhes de erro de requisições. A conexão ao upstream tem timeout de três segundos, retornando 502 ou 504 conforme a falha.

Não instalar confiança no sistema operacional automaticamente. O desenvolvedor deve aceitar ou importar o certificado público no navegador local. Não adicionar callbacks que aceitem certificados inválidos, nem opções de cliente que desabilitem TLS no aplicativo.

## Sessão da prévia

`PreviewRefreshVault` guarda a credencial apenas em memória. Uma nova instância/recarga começa sem credencial, sem localStorage/sessionStorage ou uso do plugin de storage. Isso é um bootstrap explícito de prévia; não é fallback para falha do storage nativo. `main.dart` continua usando flutter_secure_storage, Keychain/Keystore e TRONO_API_URL como antes.

O vault em memória evita adotar a implementação experimental WebCrypto do plugin como se tivesse as mesmas garantias nativas. A recarga encerra o acesso local, mas não afirma revogação remota; a sessão do servidor permanece sujeita a logout/expiração. Somente dados e telas reais são usados: não há conta simulada nem autorização de frontend.

## Operação e migrations

`docker compose up -d` é o comando único de inicialização, conforme preferência do usuário. O Compose compila imagens ausentes, aguarda PostgreSQL saudável e certificado pronto e executa `migrate`, sem profile opcional. Esse serviço exige Development, aplica Identity antes de Records e precisa terminar com sucesso para liberar a API; o gateway depende da API. Se a migração falhar, API/web não iniciam. Migrations continuam fora do processo da API e essa automação não se aplica a produção.

Dados e certificado sobrevivem à parada do Compose. O arquivo `.env` com POSTGRES_PASSWORD deve ser preparado uma única vez em instalações novas; o ambiente existente já o possui e não tem sua senha substituída. Portas podem ser configuradas por TRONO_WEB_PORT/TRONO_API_PORT/TRONO_DB_PORT. A prontidão do gateway depende de `/health/ready`, verificando ambos os módulos; o PostgreSQL verifica TCP para aguardar o fim da inicialização temporária de sua imagem. Reconstruções de código usam `docker compose up -d --build`. O helper Python de inicialização foi removido; Python permanece apenas nas ferramentas de teste.

## Dependências e alternativas

Nginx 1.30.7 Alpine é uma imagem oficial da linha estável consultada para esta entrega; seu código é BSD de duas cláusulas. OpenSSL e curl são instalados pelo repositório Alpine para geração/verificação de TLS e sondas; licenças Apache-2.0 e curl, respectivamente. O builder Flutter usa Debian e o repositório oficial do SDK. Não houve alteração de versões do pubspec/lockfile.

Alternativas: API HTTP atrás de TLS com forwarded headers confiáveis exigiria mudar a política de transporte/proxy da aplicação; origens separadas exigiriam CORS e duas configurações de certificados. HTTPS verificado entre gateway e API mantém a regra atual com menos mudanças de backend. Persistir refresh no navegador exigiria uma decisão de segurança própria e não é necessário para revisão visual local.

## Fontes

- [Imagem oficial Nginx](https://hub.docker.com/_/nginx/)
- [Licença e projeto Nginx](https://nginx.org/en/)
- [Verificação SSL de upstream](https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_ssl_verify)
- [Certificados PEM/KeyPath no Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)
- [Build web do Flutter](https://docs.flutter.dev/deployment/web)
- [Storage web experimental](https://pub.dev/packages/flutter_secure_storage)
- [OpenSSL](https://github.com/openssl/openssl)
- [Licença curl](https://curl.se/docs/copyright.html)
