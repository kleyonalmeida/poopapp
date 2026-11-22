# TRONO mobile

Aplicativo Flutter Android/iOS com cadastro, login, própria conta e sessão rotativa integrada por HTTPS. Configurar a origem com `--dart-define=TRONO_API_URL=https://seu-endpoint`.

Consulte o [README da raiz](../../README.md), as [instruções locais](AGENTS.md) e a [validação mobile/API](../../docs/testing/mobile-identity.md). Builds nativos e Keychain/Keystore em dispositivo ainda precisam de validação.

A primeira fatia de registros privados está em Minha conta → Meus registros: criação manual, histórico, edição e exclusão. Sem cronômetro ou publicação social. [Contrato](../../docs/api/records.md) e [validação atual com dependências oficiais](../../docs/testing/private-records.md).

Para visualizar no navegador em formato de celular, executar `docker compose up -d` na raiz e abrir `https://localhost:8443`, confiando no certificado local. O Compose prepara o banco e aplica migrations locais antes de iniciar a API. A imagem usa `lib/main_preview.dart`, a mesma origem da API e sessão em memória que não sobrevive à recarga. Não é necessário Python ou SDK Flutter/Android no host para essa prévia. [Operação](../../docs/operations/local-preview.md).
