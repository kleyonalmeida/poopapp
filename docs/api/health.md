# Sondas de saúde da API

As sondas são públicas e respondem em `text/plain` com `Cache-Control: no-store`. Elas aceitam chamadas sem credenciais e ignoram cabeçalhos de autenticação. O corpo contém somente o estado agregado.

| Rota | Estado | HTTP | Corpo |
|---|---|---|---|
| GET `/health/live` | Processo respondendo | `200` | `Healthy` |
| GET `/health/ready` | PostgreSQL acessível e migrations de Identity e Records desta versão aplicadas | `200` | `Healthy` |
| GET `/health/ready` | Conexão ausente, inválida ou indisponível, ou migrations pendentes | `503` | `Unhealthy` |

`/health/live` funciona sem conexão de banco e deve ser usado para detectar falhas do processo. `/health/ready` consulta os históricos de migrations de Identity e Records, cada verificação com prazo de cancelamento de três segundos. Use a prontidão para decidir se a instância pode receber tráfego de negócio. Migrar Identity antes de Records, no mesmo banco configurado em `ConnectionStrings__Identity`.

Aplique as migrations antes de colocar uma nova versão em serviço. No [Compose de desenvolvimento](../operations/local-preview.md), `up -d` executa o serviço `migrate` antes da API e o healthcheck do gateway usa `/health/ready`. Essa automação é somente local, sem migrations no processo da API. `/health/live` continua acessível diretamente para verificar somente o processo, mesmo com banco ainda não migrado fora desse Compose. [Configuração da conexão e das migrations](registration.md)

A cobertura HTTP está em `HealthTests.cs` e `ReadinessTests.cs`. [Relatório de validação](../testing/readiness.md)
