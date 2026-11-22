# Registros privados — primeira fatia de F2

Leia instruções da raiz e do mobile. `domain` contém conteúdo e porta RecordsRepository; `data` usa Dio e a fila de AuthSession; `presentation` contém histórico e editor. Contrato em `docs/api/records.md`.

Rotas `/registros`, `/registros/novo` e `/registros/editar` são privadas. Não persistir registros ou rascunhos nesta fatia. Não há timer, publicação, local, mídia ou recompensa. Só apresentar salvamento após confirmação da API. Manter rascunho e chave de criação em memória no formulário; falha incerta exige consulta ao histórico. Nunca gerar outro envio automaticamente.

Transporte exige origem HTTPS e validação TLS normal, sem redirects ou logs de payload. Todas as operações passam por AuthSession.authenticated: leituras podem renovar/repetir uma vez; escritas renovam apenas na leitura segura anterior e são enviadas uma vez. IDs/chaves não substituem autenticação. Respostas de uma conta anterior são descartadas ao sair; não manter dados na UI após logout/expiração.

Excluir registro exige diálogo. Erro de DELETE não confirma exclusão: oferecer atualizar o histórico. Paginação usa offset/20 e reinicia ao atualizar. Testes cobrem CRUD, envio pendente, resposta incerta, rota privada, logout com resposta antiga e texto ampliado. Integração real por TLS é `test_live`, não teste de dispositivo ou prova de Keychain/Keystore.

O provider usa `AuthSession.api.origin`, validada pelo transporte de identidade, para manter a mesma origem na prévia local e no app nativo. Não voltar a construir uma URL paralela por dart-define nesta feature: a origem nativa é definida no bootstrap de identidade; a prévia usa a própria página HTTPS.
