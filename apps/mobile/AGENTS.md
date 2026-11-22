# Mobile — fundação e identidade

Leia as instruções da raiz. SDK fixado em `.flutter-version`. Android/iOS possuem integração de identidade por HTTPS; validação nativa ainda depende de SDK/dispositivo/macOS.

`lib/main.dart` inicializa bindings; `lib/app/app.dart` compõe ProviderScope/tema; `lib/app/app_router.dart` guarda a rota `/conta`. Home abre `/entrar` e `/cadastro`. `lib/features/auth/AGENTS.md` contém as regras do módulo e da persistência.

Cadastro/login enviam credenciais somente por HTTPS; senha não é persistida. Apenas refresh/origem/prazo ficam no storage seguro; acesso e conta ficam em memória. Registros manuais privados possuem feature própria `records`, com histórico, editor e exclusão. Não coletar localização ou mídia nesta fase. Não apresentar botões de recursos ainda indisponíveis.

Riverpod 3.4.3, GoRouter 17.5.0, Dio 5.11.1 e flutter_secure_storage 11.2.0 fixados no pubspec/lock. Não misturar a versão separada material_ui com widgets Material do SDK sem decisão própria. Nunca adicionar retry automático de refresh, logs HTTP de credenciais, fallback em texto puro ou bypass de certificado.

`flutter test` executa unidade/widgets; `scripts/test_mobile_api.py` executa `test_live` contra API HTTPS/PostgreSQL descartáveis. Esses testes não substituem a execução Android/iOS ou a verificação real do storage nativo.

Exclusão: rota privada `/excluir-conta`, senha, consentimento e diálogo final. Não enviar DELETE mais de uma vez automaticamente. 403 não renova token. Distinguir confirmação do servidor e limpeza local; não declarar a etapa que falhou como concluída.

A rota privada `/perfil` edita o apelido da própria conta. A tela de conta apresenta o valor recebido de `GET /api/v1/me` ou confirmado pelo `PATCH /api/v1/me/profile`.

A rota autenticada `/preferencias` permite autorizar ou revogar a preferência de push. O app mantém a última escolha confirmada pela API enquanto a alteração está em andamento ou em caso de falha.

`/registros` e suas rotas de editor são privadas. Dados/rascunhos ficam somente em memória. Escritas usam a fila de sessão com renovação na leitura prévia e sem repetição automática da escrita. Não confirmar salvamento/exclusão com resposta incerta. Instruções em `lib/features/records/AGENTS.md`; regressão com lockfile oficial em `docs/testing/private-records.md`.

Prévia em navegador: `lib/main_preview.dart` reutiliza as telas, usa a origem HTTPS da página e injeta PreviewRefreshVault em memória. Não usar esse bootstrap como substituição do storage nativo nem persistir tokens em localStorage/sessionStorage. `web/index.html` é empacotado no Docker; o app nativo continua configurado por TRONO_API_URL em `main.dart`. Records toma a origem da sessão autenticada, sem uma configuração paralela. Operação em `docs/operations/local-preview.md`; não alegar validação nativa a partir da prévia.
