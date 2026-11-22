# Validação do próprio perfil

## RED → GREEN

- API. Os testes HTTP de `OwnProfileTransportTests` receberam `404` antes do mapeamento de GET e PATCH. Depois da implementação, ambos receberam `401` sem credencial e `400` por HTTP sem TLS. Os testes rodaram em processo com `WebApplicationFactory`.
- API autenticada. O host em processo recebeu uma implementação de teste para sessão e persistência. Foram verificados o titular autenticado, a leitura em ambas as rotas, a atualização, a remoção, a conta indisponível e os erros de validação. O cenário de campo ausente em Development revelou `500` durante o binding. A configuração de erro de cliente corrigiu a resposta para `400`.
- Flutter. O cenário de definição e remoção falhou inicialmente ao procurar “Editar apelido”. Os testes passaram depois da implementação e cobriram também o limite do campo, a guarda da rota e o contrato de PATCH com remoção explícita.

## Verificações executadas

- .NET 10.0.301. Restore pelo cache local, build Release sem avisos e verificação de whitespace aprovados. O `dotnet format` da solução depende de um pipe local bloqueado no ambiente.
- EF Core. O script incremental de `AddRevocableSessions` para `AddAccountNickname` foi revisado e adiciona uma coluna anulável. `migrations has-pending-model-changes` confirmou a correspondência entre o modelo e a migration. A verificação usou parâmetros fictícios de design.
- Flutter 3.44.2. Formatação, análise estática e 42 testes de unidade e widget passaram na cópia temporária, usando o engine de teste em processo. A cópia usou GoRouter 14.8.1 e crypto 3.0.7 disponíveis no cache, além de uma interface substituta para compilar o acesso ao storage. Os dois testes específicos do plugin ficaram fora da execução. Os manifests do projeto preservam as versões fixadas.

## Validação pendente

O executor padrão de `dotnet test` e o `flutter test` precisam de sockets locais que o ambiente bloqueia. O PostgreSQL descartável depende de Docker, indisponível nesta execução. Assim, ficaram pendentes os testes de persistência e isolamento entre contas, a aplicação e reversão da migration em PostgreSQL e o fluxo do cliente real com a API por TLS. A migration foi gerada, mas nenhuma base foi atualizada.

A restauração offline Flutter com o lock oficial ficou bloqueada por dependências ausentes, incluindo o plugin de storage e GoRouter 17.5.0. O teste com as versões fixadas e a verificação nativa de Android/iOS continuam pendentes.

Para reproduzir as verificações completas em ambiente com Docker e os SDKs instalados, consulte [os comandos do projeto](../../AGENTS_references.md). A cobertura de PostgreSQL está em `OwnProfileTests.cs` e `SessionMigrationTests.cs`. O cenário de cliente e API foi estendido em `apps/mobile/test_live/auth_api_live_test.dart`.
