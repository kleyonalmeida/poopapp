# Login, renovação e própria conta

Todos os endpoints abaixo exigem HTTPS e respondem com `Cache-Control: no-store`, inclusive em falhas. Tokens são opacos; o cliente deve guardá-los com segurança e enviá-los somente pelos campos/cabeçalhos documentados. Não há cookies ou autenticação via query string.

## POST `/api/v1/auth/login`

Público. Corpo JSON:

```json
{"email":"pessoa@example.com","password":"a senha exata da conta"}
```

E-mail usa o mesmo formato do cadastro, com trim e normalização de caixa. Senha não sofre trim; exige valor não vazio com até 128 caracteres. Uma senha curta incorreta resulta em 401, sem revelar regras ou dados da conta.

Sucesso, `200 OK`:

```json
{
  "tokenType": "Bearer",
  "accessToken": "at_<segredo opaco>",
  "expiresIn": 900,
  "refreshToken": "rt_<segredo opaco>",
  "refreshExpiresAt": "2026-11-19T12:00:00Z"
}
```

Os valores de exemplo são placeholders. Tokens reais contêm 256 bits aleatórios além do prefixo de tipo. `expiresIn` é a duração restante do acesso, em segundos, até 900. A sessão vence 7 dias após o login, sem extensão por refresh.

Erros: 400 ValidationProblemDetails para campos inválidos; 401 ProblemDetails genérico para usuário inexistente, senha incorreta ou conta bloqueada; 429 após 10 tentativas/minuto/IP/processo. Cinco falhas de senha na mesma conta bloqueiam novos logins por 5 minutos, com incremento serializado em concorrência. Login bem-sucedido zera o contador de falhas.

E-mail ainda pode estar não confirmado: este fluxo autentica a credencial cadastrada e não declara verificação do endereço. Confirmação e recuperação ainda não foram implementadas.

## POST `/api/v1/auth/refresh`

Público, com a credencial de renovação no corpo:

```json
{"refreshToken":"rt_<segredo opaco>"}
```

Sucesso 200 com o mesmo DTO de login. Ambos os tokens são substituídos; o acesso anterior deixa de funcionar. `refreshExpiresAt` permanece o prazo original. Perto do fim da sessão, `expiresIn` pode ser menor que 900.

400 para campo ausente/vazio ou maior que 256 caracteres; 401 para token desconhecido, incorreto, expirado, de tipo errado, revogado ou já consumido. Limite independente de 30 tentativas/minuto/IP/processo.

**Reutilização:** um refresh consumido é reconhecido pelo histórico de hashes e revoga a sessão inteira, inclusive tokens emitidos depois dele. Duas renovações simultâneas têm um 200 e um 401; a segunda revoga também o resultado vencedor. O cliente deverá serializar renovações e pedir novo login quando não puder usar a credencial atual. Não repetir automaticamente o token antigo após uma resposta perdida.

## POST `/api/v1/auth/logout`

Exige `Authorization: Bearer <accessToken>` válido. Sem corpo. Retorna 204 e revoga acesso e refresh da sessão corrente. Outras sessões da mesma conta permanecem válidas. Uma nova chamada com o acesso já revogado recebe 401. Se o acesso expirou, renovar primeiro ou iniciar um novo login; o refresh não serve como credencial Bearer.

## GET `/api/v1/me`

Exige `Authorization: Bearer <accessToken>`. Retorna 200:

```json
{"id":"<uuid>","email":"pessoa@example.com","emailConfirmed":false,"nickname":null,"pushNotificationsEnabled":false}
```

A conta vem exclusivamente da identidade autenticada. IDs em query string não escolhem outro usuário; não existe rota de leitura de contas alheias nesta entrega. Não retorna hash, SecurityStamp, sessões, credenciais ou registros pessoais.

Me/logout sem credencial válida retornam 401 ProblemDetails com desafio `WWW-Authenticate: Bearer`. Não há papéis ou recursos de terceiros que justifiquem 403 nesta fatia.

## Persistência e operação

`AddRevocableSessions` adiciona `identity.Sessions` e `identity.SessionRefreshTokens`. Índices protegem unicidade do hash de acesso e da credencial de refresh. O banco guarda SHA-256 dos tokens, nunca seus valores utilizáveis. Atualizar o SecurityStamp da conta invalida acesso e refresh já emitidos; exclusão da conta remove sessões por FK em cascata.

Aplicar migrations explicitamente conforme [cadastro](registration.md); não há atualização automática no startup. A migration é aditiva e preserva contas. Reverter somente essa migration elimina as sessões e força novos logins; não executar reversões em ambientes compartilhados sem plano e autorização.

O Compose oferece agora a [prévia local HTTPS](../operations/local-preview.md), mantendo o HTTP direto apenas para sondas e rejeição de identidade sem TLS. O gateway verifica o HTTPS da API; não foram confiados forwarded headers automaticamente. Fora do Compose, login e `/me` requerem TLS no host ou proxy confiável, como descrito no contrato de cadastro. O handler resolve o banco sob demanda, preservando o healthcheck público sem credenciais e sem configuração de conexão.

Os resumos padrão de requisição de `Microsoft.AspNetCore.Hosting.Diagnostics` foram limitados a Warning para evitar registrar queries com credenciais mesmo quando rejeitadas. Outros logs permanecem ativos, sem logging de dados sensíveis do EF ou de corpos de autenticação. A configuração de logs de um proxy externo ainda pertence à implantação.

A [limpeza de famílias com prazo absoluto vencido](../operations/session-retention.md) está disponível por opt-in, desativada por padrão. O histórico de famílias ainda no prazo é preservado. O cliente mobile implementa os fluxos e armazenamento seguro, com testes nativos pendentes. [Excluir a própria conta](account-deletion.md) é uma operação distinta de logout e invalida todas as sessões. O [próprio perfil](own-profile.md) permite ler e editar o apelido opcional, também retornado em `/me`. O mesmo retorno inclui a [preferência de notificações push](own-settings.md). Confirmação/recuperação e preferências adicionais seguem pendentes de F1.
