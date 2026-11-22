# Identidade mobile — F1

Leia a política e os AGENTS da raiz e de `apps/mobile`. Contratos HTTP: `docs/api/registration.md` e `docs/api/sessions.md`.

- `domain`: modelos, portas AuthApi/RefreshVault e AuthSession. Sem Dio, Flutter, storage ou UI. Senha preservada sem trim; nenhum token em toString.
- `data`: Dio e armazenamento seguro. Origem HTTPS explícita via TRONO_API_URL, sem credenciais/path/query/fragment; não seguir redirects nem desabilitar validação de certificado. Não adicionar LogInterceptor ou retry automático de escritas.
- `presentation`: estado Riverpod e telas; não decide autorização do servidor. Rotas GoRouter em `app/app_router.dart`.

Persistir somente refresh, origem e prazo absoluto, em uma única entrada segura. Acesso e conta ficam em memória. Não usar SharedPreferences ou fallback em texto puro. Consumir a entrada local antes de enviar refresh, para não reutilizar credencial após crash/resposta incerta. Toda operação de sessão passa pela mesma fila. Falha de refresh exige novo login; não repetir automaticamente o token consumido.

Logout remove dados privados da UI imediatamente, invalida respostas antigas e limpa a entrada segura, mesmo se o servidor estiver indisponível. Informar quando a revogação remota não for confirmada. Falha de remoção do storage não pode ser apresentada como saída concluída.

Testes de unidade/widget usam portas falsas e storage mock do plugin; não provam criptografia nativa. `scripts/test_mobile_api.py` executa integração real do cliente por TLS com PostgreSQL descartável, sem bypass de certificado. Keychain/Keystore e builds nativos exigem Android SDK/macOS/dispositivo.

Exclusão da conta requer senha, consentimento e diálogo final. Nunca repetir DELETE automaticamente. A verificação/renovação anterior usa somente GET; 403 não é gatilho de refresh. Distinguir confirmação remota de limpeza local, sem afirmar saída concluída se o storage falhar. A rota `/excluir-conta` também é privada. Exclusões executadas pelo agente limitam-se a fixtures descartáveis de teste; não operar contas reais para demonstrar a feature.

O perfil privado permite definir ou remover o apelido. O app carrega o campo com a própria conta e envia `null` quando o usuário limpa o formulário. A edição passa pela fila de sessão e apresenta o valor na tela da conta após confirmação da API. O apelido fica apenas em memória, como os demais dados da conta. Uma falha de rede exige nova ação do usuário para reenviar o PATCH.

Preferências carregam com a própria conta. O envio do valor segue pela fila de sessão em `PATCH /api/v1/me/settings`. O app atualiza o controle após confirmação do servidor. O armazenamento local de sessão persiste apenas o refresh, a origem e o prazo.

`AuthSession.authenticated` permite que Records use a mesma fila sem persistir ou expor tokens na UI. Com `readOnly: true`, a leitura pode renovar e repetir uma vez; por padrão, executa leitura segura para renovar antes e envia a escrita uma vez. 401 na escrita limpa a sessão e exige novo login, sem reenviar a operação. Não passar escritas pelo modo de leitura.

O bootstrap exclusivo `main_preview.dart` usa PreviewRefreshVault: refresh somente em memória, sem persistência no navegador. Nova instância/recarga começa vazia; isso não confirma revogação remota. Não tornar esse vault um fallback para erro do storage nativo. Testes em `preview_refresh_vault_test.dart`; fluxo do navegador em `scripts/test_preview_browser.cjs`, apenas com fixtures descartáveis.
