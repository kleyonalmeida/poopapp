# AGENT.md — Regras obrigatórias do agente TRONO

> **Escopo:** repositório `trono/`, aplicativo Flutter (Android/iOS) e API ASP.NET Core. Estas regras valem para agentes de IA e colaboradores. Este arquivo é uma política operacional; `AGENTS.md` é a documentação central do projeto. **Não aplicar automaticamente padrões do SweetCandy ao TRONO.**

---

## 🚨 REGRA ZERO — Ler o contexto ANTES de agir

**Antes de analisar implementação, editar arquivos, executar alterações ou gerar código:**

1. Ler `./AGENT.md` (este arquivo), como política de trabalho.
2. Ler `./AGENTS.md` — arquitetura vigente, domínio, fluxos e decisões; caso não exista, informar e propor sua criação.
3. Ler `./AGENTS_references.md` — índice conciso de módulos, comandos e convenções; caso não exista, informar e propor sua criação.
4. Ler `AGENTS.md` das pastas envolvidas, do caminho mais alto ao mais específico, **se existirem**. Ex.: `apps/api/AGENTS.md`, `apps/api/src/Trono.Modules.Records/AGENTS.md`, `apps/mobile/AGENTS.md`, `apps/mobile/lib/features/poop_records/AGENTS.md`.
5. Verificar instruções adicionais de testes e ADRs relevantes em `docs/`.
6. Somente então inspecionar os **arquivos de implementação diretamente relevantes**. Utilize busca dirigida (`rg`, caminhos, símbolos), evitando leitura indiscriminada do repositório.

**Conflitos:** regras de segurança e instruções de maior prioridade prevalecem; no repositório, este arquivo define o processo global e `AGENTS.md` locais refinam comportamento sem enfraquecer segurança, testes ou privacidade. Se as instruções se contradisserem, explicitar o conflito antes de modificar.

**Não inventar arquivos, padrões existentes, endpoints ou decisões.** Em repositório novo, criar a documentação mínima prevista na fase de fundação.

---

## 1. TDD FIRST — Invariável

Toda **nova funcionalidade, alteração de comportamento ou correção de bug** deve seguir rigorosamente:

1. **Entender:** identificar requisito, critérios de aceitação, falhas, limites, autorização e impactos sobre dados pessoais.
2. **RED:** escrever primeiro teste automatizado específico e **executá-lo**, comprovando falha pela ausência do comportamento esperado. Não aceitar falha por configuração quebrada como evidência de RED.
3. **GREEN:** implementar **o mínimo de código de produção** necessário para passar o teste.
4. **REFACTOR:** melhorar legibilidade/design sem alterar comportamento, com testes continuamente verdes.
5. **REGRESSION:** executar testes unitários e os testes de integração/widget/arquitetura pertinentes, além do build e análises estáticas.
6. **REPORT:** registrar testes executados, resultados, falhas pendentes e limitações.

**Proibições:** implementar regra antes do teste; remover, ignorar ou afrouxar asserts para fazê-lo passar; simular execução de testes; declarar sucesso sem executar verificações; adicionar mocks que apenas reproduzam a implementação e não validem comportamento.

Para mudanças apenas visuais/configurações/infra sem comportamento testável, definir validação apropriada (golden/widget, verificação automatizada ou checklist manual) **antes** da mudança e justificar a exceção. Para bug: primeiro teste de regressão reproduzindo o defeito.

---

## 2. Mudar somente o necessário (Touch Minimally)

- Alterar somente arquivos/linhas pertinentes; preservar contratos e padrões já estabelecidos.
- Não refatorar código fora do escopo, não adicionar dependências ou padrões sem justificativa.
- Não fazer melhorias cosméticas irrelevantes durante correções.
- Buscar soluções existentes antes de criar duplicações.
- Solicitar decisão quando uma mudança exigir quebra de contrato, migração destrutiva ou alteração ampla.

---

## 3. Arquitetura do repositório — fonte de verdade

```text
trono/
├── AGENT.md                     # regras operacionais do agente
├── AGENTS.md                    # documentação central
├── AGENTS_references.md         # índice compacto e referências
├── apps/
│   ├── api/
│   │   ├── Trono.slnx
│   │   └── src/
│   │       ├── Trono.Api/
│   │       ├── Trono.BuildingBlocks/
│   │       ├── Trono.Modules.Identity/
│   │       ├── Trono.Modules.Profiles/
│   │       ├── Trono.Modules.Friendships/
│   │       ├── Trono.Modules.Groups/
│   │       ├── Trono.Modules.Records/
│   │       ├── Trono.Modules.Feed/
│   │       ├── Trono.Modules.Locations/
│   │       ├── Trono.Modules.Gamification/
│   │       ├── Trono.Modules.Notifications/
│   │       └── Trono.Modules.Media/
│   └── mobile/
│       ├── lib/app/
│       ├── lib/core/
│       ├── lib/features/
│       ├── test/
│       └── integration_test/
├── tests/api/
│   ├── Trono.UnitTests/
│   ├── Trono.IntegrationTests/
│   └── Trono.ArchitectureTests/
├── infrastructure/
├── docs/{architecture,features,api,testing,decisions}/
└── .github/workflows/
```

Este mapa é **planejado**, não evidência de que todas as pastas já existem. Respeitar a estrutura implementada e justificar qualquer evolução.

### 3.1 Backend: monólito modular e Clean Architecture

Dentro de cada módulo, quando necessário, organizar `Domain`, `Application`, `Infrastructure` e `Presentation`.

**Direção correta das dependências de código:**

```text
Presentation ──> Application ──> Domain
Infrastructure ──> Application e/ou Domain
Trono.Api = composition root (configura DI e hospeda endpoints)
```

- `Domain`: entidades, value objects, invariantes e eventos de domínio; **não referencia** Application, Infrastructure, Presentation ou frameworks web.
- `Application`: casos de uso, interfaces/ports, contratos, validação e orquestração; **não referencia** Infrastructure ou Presentation.
- `Infrastructure`: implementa ports e persistência, integra storage, serviços externos, EF Core.
- `Presentation`: endpoints, binding de requisições, autenticação/autorização declarativa, contratos HTTP; delega regras para casos de uso.
- `Trono.Api`: registra módulos, injeção de dependências e middleware; não concentra lógica de negócio.
- Módulos compartilham apenas contratos explícitos; evitar dependência circular e acesso direto às tabelas internas de outro módulo.
- Começar com monólito modular; **não introduzir microserviços, Kafka, Kubernetes ou Redis por antecipação**.

### 3.2 Flutter: feature-first

- Organizar `lib/features/<feature>/{domain,data,presentation}` conforme complexidade real.
- Riverpod para estado e injeção no app, `go_router` para navegação, Dio para rede.
- `core/` contém apenas infraestrutura UI/técnica realmente compartilhada.
- UI não acessa diretamente banco, storage ou credenciais privadas; não decide autorização nem recompensa de forma definitiva.
- Usar widgets reutilizáveis apenas quando houver reutilização real; separar lógica de apresentação dos widgets.
- Respeitar acessibilidade, tamanhos variados, estados de carregamento, offline/erro e plataformas Android/iOS.

---

## 4. Contratos, IDs, validações e respostas

**Não copiar convenções legadas (`ResponseWrapper`, `IValidateMe`, `string` contendo GUID, mensagens de erro como retorno) do SweetCandy sem decisão técnica explícita.**

- Entidades backend: preferir `Guid` tipado para IDs internos e identificadores opacos em contratos públicos; não expor identificadores previsíveis.
- HTTP: códigos semânticos (`200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`, `422` quando cabível) e erros estruturados por `ProblemDetails`/RFC 9457; não responder `200` para uma falha.
- API: DTOs explícitos; não expor entidades EF diretamente; versionar endpoints quando aplicável e documentar contratos OpenAPI.
- Validações de formato/campos em requests via validadores separados (FluentValidation se adotado); invariantes que protegem o estado do negócio **continuam no domínio** e nunca dependem apenas do validador.
- Handlers/casos de uso não devem conter regras de validação duplicadas, detalhes de HTTP, EF Core ou regras de apresentação.
- Operações assíncronas respeitam `CancellationToken` quando aplicável.
- Datas persistidas em UTC; converter para horário local apenas para exibição e regras de calendário explicitamente documentadas.
- Contratos externos e bibliotecas devem ser adotados por ADR quando introduzirem acoplamento relevante.

---

## 5. Banco de dados e migrations

- Persistência principal: PostgreSQL, com PostGIS para consultas espaciais. EF Core com migrations versionadas.
- **Gerar e revisar** migrations com EF Core CLI; não executar alterações manuais em produção. Nunca editar migrations já aplicadas em ambientes compartilhados; criar nova migration corretiva.
- Não aplicar automaticamente `database update` em produção pelo agente; exigir autorização e plano de rollback/backup.
- Preferir migrações progressivas compatíveis com versões em execução, índices adequados e restrições de integridade.
- Testar consultas e migrations críticas em PostgreSQL real via Testcontainers; não confiar em InMemory para comportamento SQL/PostGIS.
- Usar transações quando houver necessidade de atomicidade. Recompensas e eventos devem tolerar reentrega/idempotência.

**Exemplo parametrizado** (confirmar nomes/caminhos reais do DbContext antes de executar):

```bash
dotnet ef migrations add NomeDaMigration \
  --project apps/api/src/Trono.Modules.Records \
  --startup-project apps/api/src/Trono.Api \
  --context RecordsDbContext
```

O comando é **ilustrativo**, não uma configuração existente ou autorização para alterar banco.

---

## 6. Privacidade e segurança: regras de produto inegociáveis

- **Registro pessoal é privado por padrão**; postagem social é operação distinta e depende de escolha expressa de audiência.
- Autorizar **cada acesso no backend**, incluindo feed, comentários, mídia, perfis, grupos, mapas e endpoints de leitura por ID. Testar acesso horizontal indevido (IDOR/BOLA).
- Fotos opcionais: storage privado, URLs com expiração, limites de upload, validação de conteúdo/tamanho, remoção de metadados sensíveis (como EXIF/GPS), possibilidade de denúncia, exclusão e moderação.
- Localizações: coletar somente quando necessário e autorizado; não publicar coordenadas precisas por padrão; permitir compartilhar apenas local/área aproximada; proteger casa e trabalho; revogação deve bloquear leituras futuras.
- Usar credenciais no gerenciador de segredos/variáveis seguras, **nunca** no repositório, app mobile ou logs. HTTPS obrigatório.
- Autenticação segura; refresh tokens com rotação/revogação se utilizados; rate limiting e medidas antiabuso em autenticação, convites, comentários e uploads.
- Dados corporais podem ser dados pessoais sensíveis; aplicar minimização, retenção, exclusão de conta, controle de acesso, auditoria proporcional e exigências LGPD/lojas.
- Não criar missões que premiem forçar evacuações, aumentar tempo no vaso, desperdício de papel/água ou risco físico.
- Nunca inferir liberação de segurança a partir de uma validação só no cliente.

---

## 7. Gamificação e integração social

- Toda concessão de XP/moedas/insígnias é **decidida pelo servidor**, nunca pelo Flutter.
- Eventos de conclusão de registro e recompensas precisam de chave de idempotência e proteção contra duplicidades/reprocessamento.
- Missões devem possuir regra verificável, janela temporal, elegibilidade, limitações antifraude e teste de borda.
- Separar `registro pessoal`, `publicação`, `local compartilhado` e `evento de recompensa`.
- Remover postagem não implica necessariamente excluir o registro pessoal; definir e testar comportamento de reversão de recompensas quando registros forem invalidados.
- Rankings são por temporada, com critério explícito e estratégia contra spam; preservar conquistas permanentes onde a regra permitir.
- Notificações obedecem preferências do usuário e não vazam conteúdo ou localização sensível.

---

## 8. Testes obrigatórios por tipo

| Tipo | Ferramentas previstas | Exigências |
|---|---|---|
| Unitários .NET | xUnit + biblioteca de assertions/mocks aprovada | Invariantes, regras, erros, autorização de casos de uso |
| Integração .NET | WebApplicationFactory + Testcontainers PostgreSQL | Endpoints, transações, índices/queries, idempotência, autorização |
| Arquitetura | Testes de dependência | Direção entre camadas e fronteiras dos módulos |
| Unitários Dart | `flutter_test` | Regras e estado dos providers |
| Widgets Flutter | `flutter_test` | Estados, validações, navegação e acessibilidade relevante |
| Fluxos mobile | `integration_test` | Login, registro, feed, missão, permissão de localização |
| Visual | Golden tests + revisão real | Layouts e estados relevantes, sem substituir testes de comportamento |

Todo novo endpoint deve testar sucesso, validação, não autenticado, sem permissão, não encontrado e conflitos relevantes. Qualquer operação idempotente deve ter teste de reenvio concorrente ou duplicado quando apropriado.

---

## 9. Comandos mínimos para conclusão

Executar a partir da raiz, **apenas se os projetos/caminhos já existirem**:

```bash
# Backend
dotnet restore apps/api/Trono.slnx
dotnet build apps/api/Trono.slnx --no-restore
dotnet test apps/api/Trono.slnx --no-build

# Flutter
cd apps/mobile
flutter pub get
dart format --output=none --set-exit-if-changed lib test integration_test
flutter analyze
flutter test
```

Se `integration_test` exigir dispositivo/emulador ou as pastas ainda não existirem, relatar o bloqueio; **não declarar esses testes aprovados**. Rodar Testcontainers com Docker disponível. Na CI, executar validações afetadas e um conjunto de regressão adequado antes do merge.

**Não declarar concluído** com build quebrado, testes de comportamento falhando, análise estática com erro ou migração não revisada. Quando impossibilitado de verificar, relatar `BLOQUEADO`/`NÃO VERIFICADO`, nunca presumir sucesso.

---

## 10. Documentação obrigatória ao final de cada tarefa

Ao final de cada execução **que altere código, contratos, dependências ou comportamento**, revisar e atualizar quando impactados:

1. `AGENTS.md` na raiz: resumo de módulos, fluxos, decisões novas e status.
2. `AGENTS.md` do(s) módulo(s) alterado(s): comportamentos, contratos, regras e testes pertinentes. Criá-los quando um módulo novo relevante for introduzido.
3. `AGENTS_references.md`: índice conciso, caminhos, comandos e links internos atualizados.
4. `docs/decisions/ADR-XXXX-*.md`: quando houver decisão arquitetural importante com alternativas/trade-offs.
5. `docs/api/` ou OpenAPI: para qualquer contrato HTTP adicionado ou alterado.

Documentar apenas **mudanças reais**, não reescrever toda a documentação nem produzir entradas fictícias. Para tarefas sem mudança relevante, registrar no relatório final que a documentação não precisou de atualização.

O registro deve conter, conforme o caso: funcionalidades, comandos/queries, DTOs, validações, endpoints (método/rota/contratos), mudanças em regras, migrações, decisões e testes criados.

---

## 11. Critérios de aceite e Definition of Done

Uma tarefa só está **CONCLUÍDA** se:

- [ ] Contexto e AGENTS relevantes lidos antes da alteração.
- [ ] Critérios de aceitação definidos, inclusive privacidade/autorizações.
- [ ] Evidência de teste inicialmente falhando (RED) para mudança de comportamento.
- [ ] Implementação mínima feita (GREEN), refatoração controlada (REFACTOR).
- [ ] Testes afetados, verificações de integração pertinentes, build e análise estática aprovados.
- [ ] Sem alteração fora do escopo, segredos, logs sensíveis ou autorização apenas no cliente.
- [ ] APIs, migrations e documentação revisadas conforme impacto.
- [ ] Resumo final inclui arquivos modificados, comportamento entregue, testes executados/resultados e pendências.

### Modelo de relatório final

```text
Objetivo:
Arquivos alterados:
RED — teste(s) e falha(s) esperadas observadas:
GREEN/REFACTOR — solução implementada:
Validação — comandos executados e resultados:
Segurança/privacidade — impactos e verificações:
Documentação atualizada:
Pendências/limitações:
Status: CONCLUÍDO | PARCIAL | BLOQUEADO
```

---

## 12. Regras adicionais de execução

- Nunca apagar dados, recriar banco, aplicar migração em produção, rodar comandos destrutivos ou publicar builds em lojas sem aprovação explícita.
- Não alterar dependências/versões em massa como efeito colateral.
- Antes de adicionar biblioteca, verificar manutenção, compatibilidade, licença e necessidade real.
- Evitar over-engineering: primeiro solução correta, observável e testada; otimizar mediante evidência.
- Comunicação objetiva: diferenciar fato verificado, hipótese e sugestão; expor riscos e trade-offs sem alegar ações que não foram executadas.

**Regra final:** contexto primeiro; teste vermelho antes da implementação; menor mudança correta; verificação real; documentação fiel; só então concluir.
