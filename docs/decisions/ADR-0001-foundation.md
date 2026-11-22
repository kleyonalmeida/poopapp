# ADR-0001 — Fundação mínima e verificável

Status: adotada para a implementação inicial de F0.

## Contexto

O diretório inicial continha somente política operacional e planejamento. Não havia código de aplicação nem metadados Git utilizáveis. O plano propõe Flutter, ASP.NET Core e PostgreSQL/PostGIS.

## Decisão

- Fixar .NET SDK 10.0.301 e Flutter 3.44.2, disponíveis no ambiente, evitando atualização ampla de ferramentas durante a fundação.
- Criar somente o host API, teste HTTP e tela inicial Flutter; módulos de negócio serão introduzidos por história. Não criar worker sem processamento real.
- Usar healthcheck nativo do ASP.NET Core para liveness. Readiness do banco será separada quando houver persistência real. A implementação posterior segue essa separação em `/health/ready`. [Contrato das sondas](../api/health.md)
- Usar WebApplicationFactory, xUnit 2.9.3, runner 3.1.4 e Test SDK 17.14.1, com versões disponíveis no cache local. Mvc.Testing e Test SDK usam MIT; xUnit e seu runner usam Apache-2.0, conforme os manifests NuGet consultados. Nenhuma biblioteca de assertions ou mocks adicional foi necessária. xUnit v2 recebe correções de segurança, enquanto novas funcionalidades seguem em v3; reavaliar a migração antes de ampliar a suíte.
- Fixar Mvc.Testing 10.0.10, compatível com o alvo .NET 10. Pacotes usados apenas em testes não integram a imagem de produção.
- Usar Flutter Material e flutter_test do SDK, com flutter_lints 6.0.0 para análise. Não introduzir estado, rotas ou HTTP sem caso de uso.
- Preparar Compose com PostGIS 17-3.5 e volume persistente. A API não recebe connection string nesta fase.
- Validar configurações por build, formatação, testes e smoke de infraestrutura no CI; não fabricar teste unitário de arquivos de configuração.

## Alternativas e consequências

Criar todos os módulos, adotar workers e conectar um banco sem história aumentaria a superfície sem comportamento verificável. A fundação mínima deixa identidade, migrations, outbox, integração HTTP mobile e testes de arquitetura para as fases que implementarem essas responsabilidades.

A fixação de versões permite reproduzir esta entrega; não substitui revisão periódica de atualizações. A restauração local usou cache offline e não comprovou ausência de vulnerabilidades. O CI mantém restauração normal com acesso ao feed e auditoria padrão do NuGet.

## Referências consultadas

- [Integração ASP.NET Core e WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [Mvc.Testing 10.0.10](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing/10.0.10)
- [Manutenção do xUnit 2.9.3](https://www.nuget.org/packages/xunit/2.9.3)
- [Flutter 3.44.2](https://github.com/flutter/flutter/releases/tag/3.44.2)
- [Imagem PostGIS](https://github.com/postgis/docker-postgis)
- [flutter_lints](https://github.com/flutter/packages/tree/main/packages/flutter_lints)
