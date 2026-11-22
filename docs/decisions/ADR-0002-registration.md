# ADR-0002 — Cadastro inicial com ASP.NET Core Identity

Status: adotada para a primeira fatia backend de F1.

## Decisão e escopo

Começar pelo cadastro por e-mail/senha, apoiado na stack Identity prevista no plano. Login social continua em aberto. Cadastro e sessão são operações distintas: esta entrega não emite tokens nem cookies. O fluxo de sessão revogável será especificado com seus próprios testes antes de implementar login/refresh/logout.

`Trono.Modules.Identity` agrupa Application, Infrastructure e Presentation em um único projeto. Application depende apenas de seus contratos e BCL. Infrastructure usa Identity/EF/Npgsql para implementar o port. Presentation traduz o resultado para HTTP. Não foi criado um Domain vazio nem usado IdentityUser como contrato público.

## Dependências

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` e `Microsoft.EntityFrameworkCore.Design` 10.0.10: MIT, compatíveis com .NET 10.
- `Npgsql.EntityFrameworkCore.PostgreSQL` e `Npgsql` 10.0.3: licença PostgreSQL, provider para EF Core 10. Npgsql é referência explícita de teste para inspecionar o estado real do banco.
- `Testcontainers.PostgreSql` 4.15.0: MIT, gerencia PostgreSQL descartável nos testes HTTP.
- CLI `dotnet-ef` 10.0.12 fixada em `.config/dotnet-tools.json`; migration gerada com essa versão e runtime EF 10.0.10.

As licenças e restrições de versões foram conferidas nos manifests NuGet. A restauração normal com auditoria de dependências diretas/transitivas foi executada sem avisos nesta entrega. Não houve atualização ampla das dependências existentes.

## Regras e alternativas

IDs `Guid`, e-mail normalizado pelo Identity, unicidade no PostgreSQL e senha preservada com 12–128 caracteres. O framework rejeita senha composta só por espaços e armazena seu hash. Isso evita algoritmo próprio de credenciais ou uma store em memória que aparentasse persistência.

A validação de formato/tamanho é separada do caso de uso. As invariantes de credenciais continuam no Identity. `UserManager.CreateAsync` não oferece CancellationToken; o caso de uso respeita o cancelamento antes de iniciar a criação, sem alegar interrupção de uma gravação já iniciada.

O limite de 5 tentativas/minuto/IP é local ao processo, suficiente como proteção inicial do monólito, sujeito a calibração. Não foi adotado Redis. Respostas não incluem dados da conta além do UUID criado, e falhas de duplicidade não repetem o endereço. Status 409 ainda permite distinguir duplicidade; recuperação/confirmação e mitigação adicional de enumeração devem ser avaliadas ao implementar o restante de identidade.

HTTPS é obrigatório na rota; forwarded headers não são confiados automaticamente. O Compose atual serve o healthcheck em HTTP e não representa a terminação TLS de uma implantação pública.

Migrations são executadas explicitamente; testes as aplicam em um banco novo. Não aplicar migrations no startup ou em produção por conveniência.

## Fontes

- [Configuração de ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
- [Npgsql EF Core](https://www.npgsql.org/efcore/)
- [PostgreSQL em Testcontainers .NET](https://dotnet.testcontainers.org/modules/postgres/)
- [Rate limiting ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
