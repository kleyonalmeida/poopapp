# Validação do cadastro — F1

Data: 2026-11-12. Escopo: API de cadastro; não inclui login, sessão ou interface mobile de identidade.

## Critérios e RED

Os testes foram escritos antes da rota. Com PostgreSQL real iniciado por Testcontainers, o primeiro teste esperava `201 Created` e recebeu `404 NotFound`. A execução dos 12 cenários iniciais falhou pela ausência da rota; não foram aceitas falhas de compilação/configuração como RED.

Na revisão, testes adicionais demonstraram que a validação inicial aceitava `Pessoa <pessoa@example.com>` e `pessoa nome@example.com`: esperavam 400 e receberam 201. O validador foi corrigido para exigir um único mailbox cujo endereço interpretado corresponda ao valor informado, além dos limites e validação básica. Nenhum assert foi removido ou afrouxado.

## Cobertura

- Cadastro público cria UUID e retorna somente `id`, com resposta não armazenável.
- Banco armazena hash verificável por PasswordHasher do Identity, nunca senha pura; e-mail permanece não confirmado.
- Nulos, campos vazios, e-mail inválido/ambíguo, CRLF, lista de endereços, senha curta, senha só com espaços e limites excedidos retornam ProblemDetails sem criar contas.
- E-mail com espaços externos é normalizado; duplicidade ignora caixa e não repete o endereço no erro.
- Dois cadastros concorrentes retornam um 201 e um 409, com exatamente uma nova linha persistida.
- HTTP é rejeitado sem criar conta.
- Após cinco tentativas por IP, uma nova tentativa recebe 429 e não cria conta; falsificar `X-Forwarded-For` não contorna o limite. Healthcheck continua funcionando.

O fixture aplica `InitialIdentity` no PostgreSQL/PostGIS 17-3.5 descartável antes dos testes, sem usar o banco de desenvolvimento. As factories de HTTP são novas por cenário para isolar o contador do limiter. O banco é compartilhado apenas entre cenários sequenciais da mesma classe e é destruído por Testcontainers ao final.

## Ferramentas e verificações executadas

SDK .NET 10.0.301, com `DOTNET_CLI_HOME=/tmp/trono-dotnet`. Execuções fora do sandbox foram necessárias para sockets de teste/análise e acesso ao Docker. Não houve alteração de permissões do socket.

```sh
dotnet restore apps/api/Trono.slnx --disable-parallel -m:1 -p:NuGetAuditMode=all
dotnet tool restore
dotnet format apps/api/Trono.slnx --no-restore
dotnet build apps/api/Trono.slnx --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet test apps/api/Trono.slnx --no-build --configuration Release -m:1
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore
```

Restauração normal com auditoria direta/transitiva e restauração de dotnet-ef 10.0.12 aprovadas. Testes intermediários: 13 aprovados na primeira implementação; 14 após cobertura de senha só com espaços, já rejeitada pelo Identity sem mudança de produção.

Resultado final após a correção de e-mail: **18 testes aprovados em Release**, build sem warnings/erros e formatação verificada. Status desta fatia backend: **CONCLUÍDA**. F1 permanece parcial pelos fluxos ainda não implementados listados abaixo.

Migration gerada com EF CLI (não escrita manualmente), revisada quanto a tipos, schema, FKs, índices únicos e `Down`. Scripts de aplicação idempotente e reversão foram gerados em `/tmp` e revisados; somente a aplicação foi executada, dentro dos testes. O `Down` removeria contas e não foi executado em banco persistente.

```sh
# Usando conexão fictícia apenas para instanciar o modelo; não consulta o banco.
dotnet ef migrations has-pending-model-changes \
  --project apps/api/src/Trono.Modules.Identity \
  --startup-project apps/api/src/Trono.Api --configuration Release --no-build
```

Resultado: `No changes have been made to the model since the last migration.`

## Infraestrutura

Docker Desktop foi iniciado com seu CLI local. O projeto isolado `trono-f0-check` passou em `docker compose config --quiet` e `docker compose up --build --detach --wait`. A API foi reconstruída com o módulo Identity; o healthcheck retornou `Healthy`; PostGIS respondeu `3.5 USE_GEOS=1 USE_PROJ=1 USE_STATS=1`. Requisição HTTP ao cadastro no contêiner retornou 400, `HTTPS obrigatório`, sem exigir migration no banco persistente.

A senha de desenvolvimento usada no smoke está somente em arquivo temporário `/tmp/trono-f0-validation.env`. Nenhuma senha real foi incluída no código, documentação ou saída dos comandos. A aplicação não habilita logs de dados sensíveis do EF.

A imagem final, incluindo a correção de e-mail, foi reconstruída e passou novamente no healthcheck e consulta PostGIS. Ao terminar, `docker compose down` encerrou apenas o projeto temporário `trono-f0-check`, preservando seu volume. Docker Desktop permaneceu disponível para as próximas execuções de teste.

## Limites e próximos passos

- Cadastro backend é a primeira fatia de F1, não a conclusão de US-01: faltam login, sessão revogável, confirmação/recuperação, autorização, perfil e UI integrada.
- Não há recurso privado de usuário para testar IDOR nesta fatia; nenhuma rota de consulta de contas foi exposta.
- Compose não configura TLS. A rota de cadastro só poderá ser usada fora dos testes com HTTPS configurado, conforme o contrato.
- F0 ainda depende de Android SDK/dispositivo e execução remota do CI; Flutter não mudou nesta entrega, então os 2 testes anteriores não foram reexecutados.
- `.git` continua sem metadados utilizáveis e somente leitura; não houve commit ou push.
