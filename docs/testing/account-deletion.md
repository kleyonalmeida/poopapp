# Validação de exclusão da própria conta — F1

Data: 2026-11-12. Código/VM/backend validados; **PARCIAL** quanto à execução nativa Android/iOS e CI remoto.

## Entrega e arquivos

`DELETE /api/v1/me/account` exige Bearer, HTTPS e senha atual; a conta é sempre a do titular autenticado. UserManager executa a remoção em transação PostgreSQL com lock de usuário. As cascatas existentes removem sessões, histórico de refresh e dependentes Identity. Sem nova dependência ou migration.

Backend: `Application/DeleteOwnAccount.cs`, `Infrastructure/AccountDeletionStore.cs`, `Presentation/AccountDeletionEndpoints.cs`, DI/composição e reutilização da identidade em SessionEndpoints. Mobile: porta/adapter, coordenador e estado de autenticação, `AccountDeletionPage`, botão na conta e rota privada. Testes de API, domínio, adapters, widgets e integração real foram estendidos; contratos e instruções atualizados.

No app, senha, consentimento e diálogo final precedem o único envio de DELETE. Cancelar não chama a API. 403 não provoca renovação ou repetição da exclusão. Confirmação remota e limpeza local são estados separados; respostas perdidas não são apresentadas como sucesso.

## RED → GREEN

- Nove cenários backend falharam antes da rota, recebendo 404 no lugar dos status esperados. A falha não foi de configuração ou compilação.
- Novos cenários mobile falharam pela ausência de exclusão/métodos e pelo mapeamento de 403 como renovação de acesso. Os fluxos de identidade existentes continuaram na regressão.
- Após implementação, 53 testes backend e 38 mobile passaram.
- A revisão reproduziu duas bordas: falha de rede combinada com falha de limpeza local ainda afirmava saída concluída; diálogo em viewport 320 × 568 com texto em escala 2 transbordava 136 pixels. A mensagem passou a refletir ambas as falhas, e o diálogo tornou-se rolável. Asserts foram preservados.
- Resultado final: **53 testes backend e 41 testes unidade/widgets mobile aprovados**, sem ignorar testes.

## Cobertura backend

- 204 remove usuário, claims/logins/tokens Identity, todas as sessões e os hashes de refresh correspondentes.
- Body/query indicando outro usuário não mudam a conta selecionada; outra conta e sua sessão permanecem acessíveis.
- Nenhum token antigo autentica ou renova depois do commit; novo cadastro com o mesmo e-mail recebe outro UUID, sem reativar credenciais antigas.
- Sem autenticação: 401. Sem senha: 400. Senha incorreta ou conta bloqueada: 403, preservando a conta e a sessão corrente.
- Duas exclusões simultâneas têm um 204 e um 401, sem erro de servidor. Renovação concorrente de outra sessão da mesma conta não sobrevive à exclusão.
- HTTP é recusado; limite independente de 5 tentativas/minuto/IP não é contornado por X-Forwarded-For e não bloqueia healthcheck.
- Logger capturado no fluxo não contém senha nem acesso/refresh utilizáveis.

Todas as exclusões do agente ocorreram com contas fictícias em PostgreSQL/PostGIS 17-3.5 descartável via Testcontainers ou fixture local. Nenhuma conta real, volume de desenvolvimento ou banco persistente foi excluído/migrado. Não há afirmação de remoção de backups ou logs externos.

## Cobertura mobile

Senha preservada exatamente; solicitação única; recusa sem refresh/retry nem encerramento local; resposta incerta sem falsa confirmação; limpeza falha separada de exclusão confirmada; consentimento/diálogo/cancelamento; envio em andamento bloqueado; texto ampliado e tela pequena. Os testes mantêm todos os casos anteriores de login, restauração, logout e storage.

O cliente primeiro verifica a conta com leitura segura, renovando acesso se necessário. O DELETE não entra no mecanismo de retry de leitura. Se uma resposta puder ter sido perdida após o commit, o cliente informa incerteza e limpa a sessão quando possível. Se a remoção do storage também falhar, não afirma nem exclusão nem saída concluída.

## Execuções reais

```sh
dotnet test apps/api/Trono.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet format apps/api/Trono.slnx --no-restore
dotnet build apps/api/Trono.slnx --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
dotnet test apps/api/Trono.slnx --no-build --configuration Release -m:1
dotnet format apps/api/Trono.slnx --verify-no-changes --no-restore
# Em apps/mobile:
dart format lib test test_live
flutter analyze
flutter test
dart format --output=none --set-exit-if-changed lib test test_live
flutter build apk --debug --no-pub
```

Resultados: build Release sem warnings/erros; 53 testes backend; análise Flutter sem problemas; 41 testes mobile; formatação sem mudanças. Build APK bloqueado: `No Android SDK found`.

A imagem `trono-account-deletion:check` também compilou com `docker build --file infrastructure/api.Dockerfile --tag trono-account-deletion:check .`. A verificação EF `has-pending-model-changes` retornou que não há diferenças desde a última migration; nenhuma migration nova foi necessária.

O ambiente usou `DOTNET_CLI_HOME=/tmp/trono-dotnet`, Flutter em `/tmp/trono-flutter-sdk`, `HOME=/tmp/trono-flutter-home` e `PUB_CACHE=/tmp/trono-pub-cache`. Testes/análise usaram execução fora do sandbox por sockets locais e Docker, sem alterar permissões do socket.

Integração real:

```sh
DOTNET_CLI_HOME=/tmp/trono-dotnet \
NUGET_PACKAGES=/home/kley/.nuget/packages \
NUGET_HTTP_CACHE_PATH=/tmp/trono-nuget-http \
HOME=/tmp/trono-flutter-home PUB_CACHE=/tmp/trono-pub-cache \
python3 scripts/test_mobile_api.py --flutter /tmp/trono-flutter-sdk/bin/flutter
```

**1 teste HTTPS real aprovado**, com certificado verificado e PostgreSQL descartável. Além do ciclo anterior de identidade, cria outra conta fictícia, verifica recusa com senha alterada sem trocar o refresh salvo, confirma exclusão com senha exata, limpa o vault e comprova que o login antigo recebe 401. O script encerrou API, contêiner/volume de fixture e arquivos temporários no finally. O vault desta prova é em memória, não um teste de criptografia nativa.

## Limites e continuidade

Android SDK/dispositivo, macOS/Xcode/Keychain e CI remoto continuam pendentes. Não existe E2E de dispositivo nesta entrega. Perfil/preferências e confirmação/recuperação ainda não foram implementados; limpeza operacional de sessões expiradas permanece pendente. Qualquer módulo novo com dados de usuário deve integrar a exclusão antes de ser disponibilizado.

Sem deploy, commit ou push. A implementação pode ser revisada pelo contrato, testes e código; não foi executada sobre contas reais para demonstração.
