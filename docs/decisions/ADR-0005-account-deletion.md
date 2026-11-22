# ADR-0005 — Exclusão da própria conta

Status: adotada; API e mobile validados em PostgreSQL/VM/widgets, validação nativa pendente.

## Decisão

Adicionar `DELETE /api/v1/me/account`, autorizado por Bearer e confirmado com a senha atual. A conta vem exclusivamente da identidade verificada, sem ID de destino no contrato. No app, exigir senha, consentimento e diálogo final antes de enviar. Não há exclusão iniciada automaticamente no login/logout ou falhas de sessão.

Usar UserManager para senha e remoção, com transação PostgreSQL e lock na linha de usuário. Falhas de senha contam para o bloqueio existente; conta bloqueada não pode ser removida. Limite independente de 5 tentativas/minuto/IP. Senha incorreta é 403, para não provocar refresh como se o acesso estivesse expirado.

As relações atuais já têm cascata no banco: usuário → sessões → histórico de refresh, além de claims/logins/tokens Identity. A exclusão é imediata e atômica no banco operacional atual, sem nova migration. Testes verificam concorrência, renovação de outra sessão, isolamento e reutilização de e-mail sem reativar IDs/tokens antigos.

Esta entrega não representa remoção de backups, logs externos ou conformidade legal completa. Ainda não existem registros, perfis sociais, mídia ou recompensas. Qualquer módulo futuro com dados associados a usuário deverá ampliar a coordenação e os testes de exclusão antes de ser disponibilizado; não basta excluir Identity e deixar dados órfãos.

## Cliente e falhas

Renovar acesso somente na leitura segura de verificação anterior. O DELETE é enviado uma única vez, sem retry automático, mesmo que a resposta seja perdida. 400/403/409/429 são recusas conhecidas e mantêm a sessão para correção; resposta incerta limpa a sessão local quando possível, sem declarar a conta excluída.

Confirmação do servidor e limpeza local são resultados separados. Se a API confirma 204 mas o storage falha, informar que a conta foi excluída e que o acesso salvo não pôde ser limpo. Se ambos são incertos/falhos, não afirmar nem exclusão nem saída concluída. Senha nunca é persistida ou registrada em logs.

Operação repetida tem o mesmo efeito de remoção: depois da primeira exclusão, as credenciais já não autorizam outra chamada, que recebe 401. A tela bloqueia envios duplicados e o servidor serializa solicitações simultâneas. Não há restauração/undo nesta fatia.

## Referências

- [UserManager.DeleteAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.deleteasync?view=aspnetcore-10.0)
- [Cascata no EF Core e banco](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete)
- [Binding explícito de corpo para DELETE](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0)
