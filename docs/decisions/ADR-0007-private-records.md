# ADR-0007 — Primeira fatia de registros privados

Status: adotada na implementação de registros manuais de F2. Cronômetro e publicação continuam pendentes.

## Produto e limites

Construir o primeiro ciclo útil do TRONO: registrar uma cagada, opcionalmente escolher uma descrição humorística e escrever uma história, consultar o próprio histórico, editar e excluir. Não criar feed, missões, recompensa, mídia ou localização junto com este registro.

Data/hora é o único valor obrigatório do conteúdo. Aceitar os últimos 30 dias em janela móvel e tolerar até cinco minutos de diferença de relógio no futuro. Persistir UTC com precisão de microssegundos. Um registro que envelheceu além da janela continua editável se sua data original for preservada; mudar a data exige respeitar a janela atual. Duração opcional de 1 a 86400 segundos é informação, sem pontuação. História opcional de até 280 unidades UTF-16, aparada nas extremidades. Catálogo inicial: Bode, Serpentão, Torazona, Cachoeira, Pedregulhos e Erupção.

Todos os registros são privados. Não há campo de audiência nem operação de publicação. A escolha de um catálogo pequeno permite testar o tom do produto sem antecipar todas as características do plano mestre.

## Fronteiras e exclusão de conta

Adicionar `Trono.Modules.Records` com Domain, Application, Infrastructure e Presentation, seguindo a organização já adotada em Identity. EF usa schema e histórico de migrations próprios (`records`), no mesmo PostgreSQL configurado em `ConnectionStrings:Identity`. Identity precisa ser migrado primeiro. A prontidão exige as migrations dos dois módulos, sem aplicação automática no startup.

Identity passa a exportar `Contracts/AccountPersistenceContract`: schema, tabela e identidade opaca da conta. Records referencia exclusivamente o ID para propriedade, FK e lock, sem carregar credenciais nem campos de perfil. O modelo EF dessa referência é excluído das migrations de Records. Trata-se de um contrato físico explícito entre módulos do monólito, não de autorização para consultas arbitrárias às tabelas internas.

As FKs de registros e recibos de criação usam `ON DELETE CASCADE` para a conta. Assim a transação de exclusão já existente remove os novos dados atomicamente. A criação obtém primeiro o lock da conta, na mesma ordem da exclusão; conta removida durante a requisição resulta em 401. Atualizações e remoções filtram sempre pelo titular e pelo ID do registro.

A consequência é acoplamento ao contrato físico de identidade e à ordem de migrations. Renomear/mover contas exige evolução coordenada desse contrato. Alternativas: coordenador entre contextos com transação compartilhada ou exclusão assíncrona por eventos. Nenhuma é necessária para dois módulos no mesmo PostgreSQL; a exclusão assíncrona introduziria um intervalo de retenção que esta fatia não precisa.

## Duplicidade, concorrência e histórico

POST exige `Idempotency-Key` UUID, exclusivo por conta. Um recibo armazena chave, ID do registro e SHA-256 do conteúdo normalizado; não duplica a história em texto puro. Registro e recibo são gravados na mesma transação. O lock da conta serializa criações concorrentes. Mesma chave e conteúdo retornam o registro existente; conteúdo diferente retorna 409. Se o registro foi excluído, o recibo impede recriação e retorna 410. Recibos permanecem até a exclusão da conta; sua política operacional poderá ser refinada com os volumes reais.

PATCH recebe o conteúdo completo e permite limpar valores opcionais com null. A última gravação válida prevalece. DELETE repetido recebe 404 depois da remoção, sem outros efeitos. Não existem recompensas a reverter.

Histórico usa offset/limit, ordem por data decrescente e UUID como desempate, com até 50 registros por página. Escritas concorrentes podem deslocar páginas; atualizar o histórico reinicia a paginação. Cursor e controle otimista de edição podem ser introduzidos quando o uso justificar contratos adicionais.

## Mobile e segurança

Feature `records` com porta de repositório, adapter Dio e páginas próprias. Leituras passam pela fila de sessão e podem renovar acesso. Escritas usam uma leitura segura antes do envio para renovar se necessário, mas não são repetidas após 401 nem falha de rede. O formulário mantém a mesma chave de criação enquanto aberto. Resposta incerta mantém o rascunho em memória e pede consulta ao histórico; só uma confirmação HTTP válida permite voltar como salvo.

Não há persistência offline de registros/rascunhos, bypass de TLS, logs de payload ou tokens em URL. Estado e respostas de uma conta não reaparecem depois do logout. [Contrato](../api/records.md) e [validação](../testing/private-records.md).
