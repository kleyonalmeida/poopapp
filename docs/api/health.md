# Sondas de saúde da API

As sondas são públicas e respondem em `text/plain` com `Cache-Control: no-store`. Elas aceitam chamadas sem credenciais e ignoram cabeçalhos de autenticação. O corpo contém somente o estado agregado.

| Rota | Estado | HTTP | Corpo |
|---|---|---|---|
| GET `/health/live` | Processo respondendo | `200` | `Healthy` |
| GET `/health/ready` | PostgreSQL de Identity acessível e migrations desta versão aplicadas | `200` | `Healthy` |
| GET `/health/ready` | Conexão ausente, inválida ou indisponível, ou migrations pendentes | `503` | `Unhealthy` |

`/health/live` funciona sem conexão de banco e deve ser usado para detectar falhas do processo. `/health/ready` consulta o histórico de migrations de Identity e usa um prazo de cancelamento de três segundos. Use a prontidão para decidir se a instância pode receber tráfego de negócio.

Aplique as migrations explicitamente antes de colocar uma nova versão em serviço. O Compose de desenvolvimento usa `/health/live` para seu smoke inicial, pois o banco pode ainda precisar da aplicação das migrations. [Configuração da conexão e das migrations](registration.md)

A cobertura HTTP está em `HealthTests.cs` e `ReadinessTests.cs`. [Relatório de validação](../testing/readiness.md)
