# Validação da prévia Docker

Data: 2026-11-22. Escopo: servir a interface Flutter mobile no localhost, com API e PostgreSQL reais, preservando HTTPS e o isolamento dos testes. O produto Android/iOS continua com validação nativa pendente.

## RED → GREEN e critérios

- Antes da configuração, `python3 scripts/test_preview_http.py --url http://127.0.0.1:5080` encontrou 404 na raiz, em vez da interface. Depois do build web e gateway, o smoke confirmou index/bootstrap e o fluxo real por HTTPS.
- `preview_refresh_vault_test.dart` falhou com o vault ainda sem comportamento. A implementação em memória passou: uma instância pode ler/limpar seu refresh; uma nova instância começa vazia.
- A configuração de Records falhou quando a sessão da prévia fornecia a origem HTTPS sem dart-define. O provider passou a usar a origem validada da sessão. O teste específico passou, preservando a configuração nativa no bootstrap de identidade.
- A fixture interrompeu somente sua própria API e enviou uma marca fictícia na query. O teste reproduziu vazamento da query nos logs de erro do Nginx. Depois de desativar logs por requisição, a falha do upstream retornou 502/504 com timeout limitado e sem a marca nos logs.

A validação de infraestrutura foi definida antes dos ajustes: build real, interface na raiz, certificado confiado explicitamente, upstream TLS verificado, migrations no banco descartável, autenticação/autorização, manutenção da recusa de HTTP e descarte de credenciais da prévia ao recarregar. Não foram usados testes que apenas verificam texto do YAML como prova do comportamento.

## Resultados confirmados

| Verificação | Resultado |
|---|---|
| `docker compose config --quiet` | Aprovado |
| Imagens API, migrations, certificados e Flutter/Nginx | Compiladas |
| Build Flutter web Release, lockfile oficial, SDK 3.44.2 | Aprovado |
| Dart format e Flutter analyze | Sem alterações/problemas |
| Regressão Flutter completa | 55 testes aprovados |
| Smoke Compose isolado | Interface, TLS, PostGIS, migrations, cadastro/login, autorização e exclusão aprovados |
| Falha de upstream sem query nos logs | Aprovado |
| Navegador 390×844 | Login, criação, conteúdo exato ao reabrir editor e recarga sem sessão/credenciais persistidas aprovados |
| Instância local persistente | Interface/API/banco iniciados, gateway saudável e `/health/ready` 200 com certificado verificado |

O teste completo de navegador passou junto com o smoke de infraestrutura em uma execução isolada final. A interface foi inspecionada em screenshot 390×844 com registro salvo. Textos desenhados em canvas e campos semânticos do Flutter exigiram ajustes na automação: foco e entrada reais, aguardando as transições de rota, em vez de tratar proxies semânticos como inputs comuns. O teste confirmou o conteúdo exato ao reabrir o editor, retorno ao histórico e recarga sem recuperar a sessão ou manter chaves de credenciais em localStorage/sessionStorage. Nenhum erro JavaScript foi registrado no fluxo final.

## Comandos

```sh
docker compose up -d
python3 scripts/test_docker_preview.py
# Com Playwright e Chrome de testes disponíveis:
python3 scripts/test_docker_preview.py --browser
```

No ambiente desta execução, o navegador de teste usa PLAYWRIGHT_MODULE apontando para uma instalação existente e CHROME_BIN para Chrome headless. Bibliotecas públicas libnss3/libnspr4 foram extraídas em `/tmp/trono-browser-libs`, sem instalação no sistema. O processo de navegador confia somente na chave pública do certificado da fixture; as requisições HTTP Python usam `ssl.create_default_context(cafile=...)`. A aplicação não recebeu bypass de certificado.

As verificações Flutter usaram `/tmp/trono-flutter-sdk`, com sockets liberados para os runners. Docker utilizou acesso ao daemon e à rede para baixar as imagens/pacotes. Não houve mudança de versões de dependências Flutter nem necessidade de SDKs nativos no host para o build da prévia.

## Dados e operação

Cada smoke criou seu próprio projeto `trono-preview-check-*`, senha, portas e volumes. Contas fictícias e operações de exclusão ocorreram somente nessas fixtures; o finally removeu apenas seus contêineres, volumes e imagens locais. O banco persistente `poopapp_trono-postgres` foi preservado.

Na implementação inicial, a subida local usava um helper Python para chamar o profile de ferramentas. A pedido do usuário, ele foi substituído pela dependência `api → migrate` no próprio Compose. O comando atual `docker compose up -d` executa migrations de Development antes da API, preservando `.env`, credencial e volumes. Não há migrations no processo da API nem habilitação da limpeza de sessões. Certificado/chave ficam em volume TLS separado; a chave não foi incluída na imagem ou nos arquivos versionados.

Execuções descartáveis encontraram o processo do certificado já concluído, mas o Docker ainda o marcava como running; somente os contêineres TLS dessas fixtures foram parados para liberar a dependência. Os serviços de inicialização passaram a usar `init: true`. A espera por prontidão no teste é limitada; o comando de operação pode usar `docker compose up -d --wait --wait-timeout 120` quando for necessário aguardar a saúde dos serviços.

## Limites

Prévia local e testes de navegador não substituem build Android/iOS, aparelho, teclado nativo ou Keychain/Keystore. A sessão do navegador é somente em memória; recarregar exige novo login e não confirma revogação remota. O certificado local precisa ser confiado pelo navegador do desenvolvedor; não há configuração de produção ou exposição pela LAN.

O build web emitiu aviso sobre referência opcional à fonte CupertinoIcons, sem impedir a compilação; os ícones Material usados nas telas foram inspecionados. A validação nativa permanece necessária. O workflow foi atualizado para o smoke isolado, sem execução remota nesta tarefa. Não houve commit, push ou publicação externa.

## Continuação — comando único de Compose

O teste passou a executar exatamente `docker compose up -d`, sem helper Python. Antes de alterar o YAML, falhou ao verificar os históricos de migrations no PostgreSQL novo: o serviço `migrate` estava fora do fluxo padrão. Depois de remover o profile opcional e adicionar a dependência de sucesso da API, o teste completo passou.

- Banco novo recebeu os históricos de Identity e Records no `up -d`.
- Uma segunda execução preservou uma linha sentinela na fixture e o mesmo certificado público.
- Em outro projeto descartável, o migrador foi executado com ambiente não permitido: encerrou com falha e API/web não iniciaram.
- Smoke HTTPS de interface, autenticação, PostGIS e privacidade dos logs permaneceu aprovado.
- `docker compose up -d` também foi executado no ambiente local persistente, preservando os dados e a configuração existentes.

O script `dev_up.py` foi removido; Python é usado somente nos testes. Esta continuação alterou orquestração e documentação, sem mudança de código Flutter/API; os resultados anteriores de widgets e navegador acima não foram reexecutados para essa alteração de inicialização.
