# Docker de desenvolvimento e prévia mobile

Leia AGENT/AGENTS da raiz. Esta configuração é somente local; não publicar como configuração de produção. Guias em `docs/operations/local-preview.md` e ADR-0008.

`api.Dockerfile` separa runtime e target de migrations. O runtime continua como usuário app e não migra no startup. `migrate-local.sh` exige Development e executa Identity antes de Records. O Compose local chama esse serviço de inicialização no `up -d`; a API depende da sua conclusão com sucesso. Essa automação foi solicitada pelo usuário para desenvolvimento e não autoriza migração automática em produção. Não ativar limpeza de sessões ou remover volumes como efeito colateral de subir a prévia.

`web.Dockerfile` usa o SDK Flutter fixado e o lockfile oficial, compilando apenas `main_preview.dart`. Não incluir `.env`, certificados, chaves, build local ou caches do host no contexto copiado para imagens. Os certificados são gerados em volume próprio, montado somente leitura no gateway/API; chave 0600 para UID 1654. Todas as portas publicadas são de loopback.

Nginx usa TLS também até a API, com verificação de certificado e hostname. Não habilitar bypass TLS, confiar em forwarded headers ou introduzir CORS genérico para resolver a prévia. Logs de requisição/erro do gateway ficam desativados para não revelar queries; usar prontidão/códigos HTTP para diagnóstico local. Preservar o teste de falha do upstream sem vazamento.

Antes de alterar infraestrutura, definir validação apropriada. `scripts/test_docker_preview.py` testa em projeto/volumes próprios; sua opção `--browser` valida o fluxo mobile com Playwright/Chrome de teste. Remover somente fixtures identificadas, nunca volumes do ambiente persistente para demonstrar mudanças.
