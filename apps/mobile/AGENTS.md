# Mobile — fundação e identidade

Leia as instruções da raiz. SDK fixado em `.flutter-version`. Android/iOS possuem integração de identidade por HTTPS; validação nativa ainda depende de SDK/dispositivo/macOS.

`lib/main.dart` inicializa bindings; `lib/app/app.dart` compõe ProviderScope/tema; `lib/app/app_router.dart` guarda a rota `/conta`. Home abre `/entrar` e `/cadastro`. `lib/features/auth/AGENTS.md` contém as regras do módulo e da persistência.

Cadastro/login enviam credenciais somente por HTTPS; senha não é persistida. Apenas refresh/origem/prazo ficam no storage seguro; acesso e conta ficam em memória. Não coletar localização, mídia ou registros nesta fase. Não apresentar botões de recursos ainda indisponíveis.

Riverpod 3.4.3, GoRouter 17.5.0, Dio 5.11.1 e flutter_secure_storage 11.2.0 fixados no pubspec/lock. Não misturar a versão separada material_ui com widgets Material do SDK sem decisão própria. Nunca adicionar retry automático de refresh, logs HTTP de credenciais, fallback em texto puro ou bypass de certificado.

`flutter test` executa unidade/widgets; `scripts/test_mobile_api.py` executa `test_live` contra API HTTPS/PostgreSQL descartáveis. Esses testes não substituem a execução Android/iOS ou a verificação real do storage nativo.

Exclusão: rota privada `/excluir-conta`, senha, consentimento e diálogo final. Não enviar DELETE mais de uma vez automaticamente. 403 não renova token. Distinguir confirmação do servidor e limpeza local; não declarar a etapa que falhou como concluída.

A rota privada `/perfil` edita o apelido da própria conta. A tela de conta apresenta o valor recebido de `GET /api/v1/me` ou confirmado pelo `PATCH /api/v1/me/profile`.

A rota autenticada `/preferencias` permite autorizar ou revogar a preferência de push. O app mantém a última escolha confirmada pela API enquanto a alteração está em andamento ou em caso de falha.
