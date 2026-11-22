# Validação da prontidão da API

## RED → GREEN

O teste HTTP esperava `503 Unhealthy` para prontidão sem conexão configurada e recebeu `404` antes do mapeamento. Depois da implementação, o host real em processo passou pelos três cenários de conexão ausente, inválida e indisponível. Em todos, `/health/live` respondeu `200 Healthy`. As duas sondas também foram verificadas com um cabeçalho Bearer fictício e devolveram somente o estado previsto com `Cache-Control: no-store`.

## PostgreSQL real

O cenário de `ReadinessTests` foi executado em processo dentro de um contêiner PostGIS 17 descartável, com PostgreSQL acessível somente por loopback e dados em `tmpfs`. A mesma rotina usada pelo teste Testcontainers verificou estes estados:

1. Banco recém-criado. Prontidão `503` e histórico de migrations vazio.
2. Banco migrado até `AddAccountNickname`. Prontidão `503` com a migration de preferências pendente.
3. Banco com todas as migrations da versão. Prontidão `200` e liveness `200`.

As migrations foram aplicadas explicitamente pelo teste no banco descartável.

## Regressão

Build Release e verificação de whitespace passaram. O executor em processo também verificou o contrato de liveness e os principais cenários HTTP de perfil e preferências. O executor padrão de `dotnet test` abortou ao abrir seu socket local, deixando a suíte completa pendente. [Comandos do projeto](../../AGENTS_references.md)
