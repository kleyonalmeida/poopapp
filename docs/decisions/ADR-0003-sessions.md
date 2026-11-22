# ADR-0003 — Sessões opacas e revogáveis

Status: adotada e validada nesta fatia de F1.

## Decisão

Usar autenticação Bearer do ASP.NET Core com tokens opacos de 256 bits, gerados por RandomNumberGenerator, persistindo somente SHA-256. O banco mantém a sessão, seu SecurityStamp, expiração e histórico de hashes de refresh consumidos. Não há JWT, cookie, Redis, biblioteca criptográfica adicional ou servidor OAuth/OIDC nesta entrega.

O acesso dura até 15 minutos; a sessão tem prazo absoluto de 7 dias. Refresh substitui ambos os tokens e não estende esse prazo. Logout revoga a sessão corrente. Mudança de SecurityStamp invalida todas as sessões emitidas com o stamp anterior. A consulta autenticada lê o banco, permitindo revogação nas requisições seguintes.

Rotação e logout são operações transacionais. Um lock na linha de sessão serializa refreshes; reutilizar um refresh consumido revoga a família inteira. Duas renovações concorrentes resultam em um sucesso e uma rejeição, e o replay revoga inclusive o resultado vencedor. O cliente deverá serializar renovações e pedir novo login após falha de replay, em vez de repetir o token antigo.

Login usa UserManager para verificar senha, sem trim, e bloqueio de conta por 5 minutos após 5 falhas. O lock da conta evita perder incrementos de falhas concorrentes. Para usuário inexistente/bloqueado, uma verificação com hash fictício do mesmo framework reduz diferenças grosseiras de custo; não se afirma tempo de resposta constante.

Limites por processo/IP: cadastro 5/min, login 10/min e refresh 30/min, independentes. Sem forwarded headers confiados automaticamente. HTTPS e no-store também cobrem `/api/v1/me`. Nenhum endpoint escolhe a conta por ID fornecido pelo cliente.

## Alternativas e consequências

Tokens Bearer autocontidos do Identity não oferecem por si sós rotação de uso único e revogação imediata. JWT exigiria chaves e ainda precisaria de estado de revogação. A consulta indexada ao PostgreSQL mantém essas garantias com a stack existente, ao custo de acesso ao banco por autenticação.

A expiração é decidida no servidor com TimeProvider; testes avançam o relógio sem sleeps. Datas emitidas são truncadas à precisão de microssegundos do PostgreSQL para preservar o prazo ao recarregar a sessão.

Não há fluxo de confirmação/recuperação ou interface mobile nesta fatia. E-mail não confirmado não representa verificação de identidade. A retenção e limpeza operacional das sessões expiradas/hashes consumidos deverão ser implementadas antes do beta; expiração já bloqueia seu uso.

## Referências

- [Autenticação ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0)
- [OWASP: gerenciamento de sessão](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [RFC 9700, seção 4.14.2: rotação e detecção de replay](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14.2) — referência para o princípio de rotação, sem alegar implementação OAuth.
