# Cadastro por e-mail — F1

`POST /api/v1/auth/register`, público, exclusivamente por HTTPS, JSON.

```json
{
  "email": "pessoa@example.com",
  "password": "uma frase longa e privada"
}
```

## Contrato

| Resultado | HTTP | Corpo |
|---|---|---|
| Conta criada | 201 | `{"id":"<uuid>"}` |
| Campos inválidos | 400 | ValidationProblemDetails com `errors` |
| Transporte HTTP | 400 | ProblemDetails, HTTPS obrigatório |
| E-mail já cadastrado, inclusive em concorrência | 409 | ProblemDetails genérico, sem e-mail |
| Excesso de tentativas | 429 | ProblemDetails |

Respostas de cadastro usam `Cache-Control: no-store`. Não há cookie, token, hash ou senha no retorno. O UUID é opaco; nenhuma rota de leitura de contas é exposta nesta fatia.

E-mail sofre trim, validação de um único endereço sem nome de exibição e limite de 254 caracteres. Identity normaliza para pesquisa; índices únicos em `NormalizedEmail` e `NormalizedUserName` protegem concorrência. Senha deve ter 12–128 caracteres, não pode conter apenas espaços e é preservada sem trim; não há exigência de símbolos, números ou caixa. ASP.NET Core Identity valida as invariantes e armazena o hash em seu formato versionado, sem criptografia própria.

Cadastro não confirma o e-mail, não autentica nem publica perfil. Login e sessões são operações separadas, descritas em [sessions.md](sessions.md). Confirmação/recuperação seguem pendentes; o [apelido privado](own-profile.md) já pode ser editado após login. Não há teste de 401/403 por recurso nesta rota de criação pública, sem ID de entrada ou recurso de terceiro.

## Proteção contra abuso

Limite inicial de 5 tentativas por minuto, por IP remoto e processo, sem fila. Tentativas inválidas também contam. O healthcheck permanece independente. Cabeçalhos `X-Forwarded-For` enviados pelo cliente não alteram a partição; nenhum proxy confiável foi configurado nesta entrega. Uma implantação com proxy/TLS deverá configurar explicitamente a cadeia confiável antes de expor o cadastro.

## Persistência e execução

Configurar `ConnectionStrings__Identity` em variável de ambiente ou secret do ambiente. Não há valor real no código. A migration `InitialIdentity` cria schema `identity`, tabelas de usuário/claims/logins/tokens e índices. Não é aplicada no startup da API.

Preparação das ferramentas e revisão da migration:

```sh
dotnet tool restore
# Defina ConnectionStrings__Identity para o ambiente local.
dotnet ef migrations script --idempotent \
  --project apps/api/src/Trono.Modules.Identity \
  --startup-project apps/api/src/Trono.Api \
  --context Trono.Modules.Identity.Infrastructure.IdentityDbContext
```

Após revisar o SQL, aplicar **somente no banco local de desenvolvimento**:

```sh
dotnet ef database update \
  --project apps/api/src/Trono.Modules.Identity \
  --startup-project apps/api/src/Trono.Api \
  --context Trono.Modules.Identity.Infrastructure.IdentityDbContext
```

O Compose fornece a conexão interna e agora oferece a [prévia local por HTTPS](../operations/local-preview.md) em `https://localhost:8443`, com gateway e upstream TLS verificado. O HTTP direto de desenvolvimento permanece exclusivo de sondas e rejeita cadastro. Fora do Compose, configurar um endpoint HTTPS no host (`dotnet run --project apps/api/src/Trono.Api --urls https://localhost:7080`, com certificado configurado) ou proxy TLS confiável. Não desabilitar a exigência de HTTPS para contornar o ambiente.

`Down` remove as tabelas do módulo e perderia contas existentes; não é estratégia de rollback de produção. Em ambiente compartilhado, exigir backup e migração corretiva revisada. Nesta entrega, a migration foi aplicada apenas ao PostgreSQL descartável dos testes.
