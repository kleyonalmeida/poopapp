# Records — registros manuais privados

Leia AGENT/AGENTS da raiz e `apps/api/AGENTS.md`. Contrato em `docs/api/records.md`; decisão em `docs/decisions/ADR-0007-private-records.md`.

Domain valida conteúdo e propriedade; Application coordena casos de uso e expõe `IOwnRecordStore`; Infrastructure implementa EF/PostgreSQL; Presentation escolhe a conta por ClaimTypes.NameIdentifier já autenticado. Não criar visibilidade social, mídia, mapa ou recompensa neste módulo sem histórias e autorização correspondentes.

Todos os acessos filtram por conta e ID; terceiro recebe o mesmo 404 de inexistência. HTTPS e no-store obrigatórios. Não registrar história, tokens ou payloads. Tipos do catálogo, limite de história, janela de 30 dias e duração opcional ficam no domínio. Datas UTC/microssegundos; data original de um registro antigo pode ser preservada durante edição.

Schema/histórico `records`, conexão compartilhada por `ConnectionStrings:Identity`. Aplicar migrations depois de Identity. No Compose Development, o serviço `migrate` coordena essa execução antes da API; fora dele, a aplicação é explícita. Não migra no processo da API nem automaticamente em produção. A referência de conta possui somente ID e usa o contrato exportado por Identity, excluída das migrations locais. Não ler outras colunas ou tabelas internas de Identity. FKs de registros e recibos excluem por cascata na remoção da conta. Criação usa lock da conta antes de gravar para coordenar com exclusão.

Recibo por conta/chave UUID guarda somente ID e hash do conteúdo inicial. POST concorrente com mesma chave cria uma vez; conteúdo diferente é 409 e reenvio de registro apagado é 410. Não limpar recibos sem redefinir/testar a garantia de deduplicação. PATCH é conteúdo completo, com última gravação válida; DELETE apaga conteúdo e preserva recibo. Histórico paginado por offset com limite de 50.

`RecordTests`, `RecordTransportTests` e `ReadinessTests` verificam HTTP, PostgreSQL real, autorização, validação, concorrência, cascata e prontidão. Testes e migrations do agente usam apenas dados fictícios descartáveis. Nenhum banco persistente deve ser migrado/limpo para demonstração.
