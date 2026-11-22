# Validação das preferências de conta

## RED → GREEN

- No host ASP.NET Core em processo, `OwnSettingsTransportTests` recebeu `404` antes do mapeamento. Depois, GET e PATCH retornaram `401` sem Bearer e `400` por HTTP sem TLS. Com portas substitutas, foram verificados o valor inicial desativado, a seleção da conta pela sessão, a ativação, a leitura em `/me`, a rejeição de valores inválidos e a revogação.
- No Flutter, o teste falhou inicialmente pela ausência da opção “Preferências”. Depois, os testes de widget verificaram ativação, revogação, rota autenticada e preservação do último valor confirmado em caso de falha. O teste de Dio verificou o booleano enviado e a rejeição de uma resposta com tipo inválido.

## Verificações executadas

- Build Release da API e dos testes com .NET 10.0.301 passou sem avisos. A verificação de whitespace passou. O executor padrão de `dotnet test` abortou por falta de permissão para abrir um socket local.
- EF Core confirmou o modelo sincronizado e gerou os scripts de ida e volta. A ida adiciona `boolean NOT NULL DEFAULT FALSE` em `identity.AspNetUsers`; a volta remove a coluna. Os comandos usaram parâmetros fictícios de design. A execução da migration em PostgreSQL permanece pendente.
- Com Flutter 3.44.2, a formatação, a análise estática e 45 testes de unidade e widget passaram em cópia temporária com o engine em processo. O cache disponibilizou GoRouter 14.8.1 e crypto 3.0.7 e foi usada uma interface substituta de storage. Os dois testes específicos do plugin ficaram fora dessa execução. O `flutter test` padrão foi bloqueado pela abertura de socket local, e o restore offline com o lock do projeto não encontrou `flutter_secure_storage` 11.2.0.

## Validação pendente

O ambiente bloqueou o acesso ao socket do Docker. A persistência, o isolamento entre contas e a atualização do banco com dados existentes têm cobertura em `OwnSettingsTests.cs` e `SessionMigrationTests.cs`, com execução pendente em PostgreSQL descartável. Também ficam pendentes o fluxo Flutter/API por TLS em `test_live` e os testes com as dependências fixadas e os SDKs nativos. [Comandos do projeto](../../AGENTS_references.md)
