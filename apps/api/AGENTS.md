# API — fundação, identidade e registros privados

Leia as instruções da raiz. .NET 10, ASP.NET Core Minimal API. `src/Trono.Api/Program.cs` compõe saúde e o módulo Identity, com ProblemDetails, HTTPS nas rotas de identidade e rate limiting.

`/health/live` verifica somente o processo. `/health/ready` verifica o banco e os históricos de migrations de Identity e Records, com cancelamento após três segundos por verificação. Ambas as sondas retornam um estado agregado com `no-store` e ignoram credenciais. Contrato em `docs/api/health.md` e testes em `HealthTests.cs` e `ReadinessTests.cs`.

Não criar módulos vazios, persistência ou autenticação provisória. Novas regras exigem teste RED antes de implementação.

`src/Trono.Modules.Identity/AGENTS.md` descreve cadastro e sessões. A conexão é `ConnectionStrings__Identity`; a sonda de processo funciona sem resolver a conexão. Autenticação Bearer verifica sessão/hash/stamp/expiração no banco; `/api/v1/me` e logout exigem autorização. Não aplicar migrations no processo da API; o Compose Development usa um serviço de inicialização separado. Os testes usam Docker/PostgreSQL descartável; não substituir por EF InMemory.

Resumos de requisição Hosting.Diagnostics são limitados a Warning para não registrar credenciais recebidas em query string. Não habilitar logs de corpos de autenticação ou dados sensíveis do EF.

`DELETE /api/v1/me/account` exige Bearer, HTTPS e confirmação de senha. Usa transação/lock do usuário e FKs em cascata, sem migration nova. As exclusões executadas para validar esta feature usam exclusivamente contas fictícias em PostgreSQL descartável. Não operar contas reais para demonstração.

Retenção usa PruneExpiredSessions e BackgroundService com escopo por ciclo. Está desativada por padrão, exige configuração válida e conexão quando habilitada. Não cria endpoint público, não aplica migrations e não remove contas. Ver `docs/operations/session-retention.md` antes de habilitar em qualquer ambiente.

O próprio perfil está em `GET/PATCH /api/v1/me/profile`, com autorização Bearer, HTTPS e `no-store`. A API retorna erros de binding como erros de cliente também em Development. O campo de apelido exige a migration aditiva `AddAccountNickname` antes da atualização da API. Ver `docs/api/own-profile.md`.

`GET/PATCH /api/v1/me/settings` usa a sessão autenticada, HTTPS e `no-store`. A migration aditiva `AddPushNotificationPreference` introduz a preferência de push com padrão `false`. Ver `docs/api/own-settings.md`.

`Trono.Modules.Records` implementa registros manuais privados em `/api/v1/records`, com HTTPS, Bearer e no-store. Consulte o AGENTS do módulo e `docs/api/records.md`. Migrations de Identity precedem Records; a prontidão verifica ambos os históricos. Exclusão da conta agora também remove registros e recibos por cascata no mesmo PostgreSQL. A única referência compartilhada é o contrato explícito de ID/tabela da conta, sem leitura de credenciais/perfil por Records.

No Compose local, Kestrel também escuta HTTPS 8443 com certificado PEM do volume TLS somente leitura. Nginx verifica esse certificado e serve a prévia na mesma origem pública; não foi habilitada confiança em forwarded headers nem CORS genérico. `migrate` é um serviço de inicialização exclusivo de Development, executado pelo `docker compose up -d`; a API depende de sua conclusão bem-sucedida e continua sem migrations no próprio startup. Essa automação é apenas local. Guia em `docs/operations/local-preview.md`.
