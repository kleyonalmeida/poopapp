# TRONO — Plano Mestre de Produto, Negócio e Engenharia

> **Status:** planejamento inicial v1.0, sujeito a refinamento. **Data:** 13/10/2026.  
> **Produto:** aplicativo mobile-first (Android + iOS), rede social gamificada entre amigos em torno de registros humorísticos de idas ao banheiro.  
> **Fonte da verdade:** este documento define o escopo proposto; `AGENT.md` contém instruções operacionais obrigatórias do agente; `AGENTS.md` documentará a arquitetura *efetivamente implementada*. Nunca descrever propostas como funcionalidades já prontas.

---

# 0. Decisões e princípios

## 0.1 Proposta de valor

**TRONO** transforma um registro pessoal opcional em uma experiência social divertida: registrar → descrever → escolher audiência → compartilhar → receber reações → avançar em quests/conquistas → voltar para ver amigos e a temporada. O aplicativo **não é um dispositivo médico**, não diagnostica doenças e não incentiva evacuações adicionais, esforço, demora excessiva no vaso, desperdício de água/papel ou exposição íntima.

**Pilares:** (1) social: amigos, grupos, feed, comentários, reações; (2) jogo: XP, insígnias, temporadas, títulos e desafios; (3) exploração: mapa pessoal, lugares compartilhados e coleções geográficas.

**Princípios:** privacidade por padrão; registro rápido; tudo opcional além do mínimo; grupos privados primeiro; recompensas por participação legítima, não por frequência corporal; segurança no servidor; acessibilidade; simplicidade operacional e baixo custo de manutenção.

## 0.2 Escopo por fase

- **MVP público/beta:** onboarding, autenticação, perfil, amizades/convites, grupos privados, cronômetro robusto, registro manual, catálogo de descrições, histórico privado, publicação opcional, feed, reações, comentários, foto opcional protegida, mapa pessoal e local público selecionado, quests iniciais, XP/insígnias básicas, notificações essenciais, configurações de privacidade, denúncia/bloqueio/exclusão.
- **V1 pós-validação:** ranking por temporadas com múltiplas categorias, missões coletivas, títulos semanais, histórico detalhado, mais conquistas secretas, coleções de lugares e retrospectiva semanal.
- **V2:** avatares/cosméticos e moeda virtual, figurinhas exclusivas, retrospectiva anual, eventos sazonais, avaliação de banheiros (requer moderação ampliada) e eventualmente minigames/IA (somente após validação e revisão de segurança/privacidade).
- **Fora do escopo inicial:** DMs, feed mundial público, geolocalização contínua/background, pagamento real, NFT/cripto, diagnóstico médico, análise automática de fotos íntimas, publicidade direcionada por dados de saúde.

**Termos:** `registro` é dado privado sobre a atividade; `postagem` é objeto social separado, criado somente após consentimento explícito; `local privado` não implica `local compartilhado`.

## 0.3 Decisões em aberto a validar

Nome e marca final (TRONO é provisório); classificação etária e política de fotos; catálogo final de termos visuais; login Apple/Google versus e-mail; provedor de mapa (Google Maps x Mapbox); estratégia de login/social/FCM no iOS; formato final das temporadas; preço/monetização (não previsto para MVP); volume de armazenamento por conta; retenção de dados; política detalhada de moderação. Registrar cada escolha em ADR em `docs/decisions/`.

---

# 1. Stack oficial proposta

| Camada | Escolha inicial | Responsabilidade/critério |
|---|---|---|
| Mobile | **Flutter + Dart** | Aplicativo único Android/iOS; interfaces animadas e acessíveis |
| Estado | **Riverpod** | Estado previsível, dependências e testes |
| Navegação | **go_router** | Rotas, deep links e redirecionamento autenticado |
| HTTP | **Dio** | Interceptors, tempo limite, autenticação, tratamento de erros |
| Backend | **ASP.NET Core Web API** | REST, lógica de negócios, autorização e orquestração |
| Persistência | **PostgreSQL + PostGIS** | Dados relacionais e consultas geoespaciais |
| ORM | **Entity Framework Core + Npgsql** | Migrations, mapeamento e transações |
| Identidade | **ASP.NET Core Identity + fluxo de tokens bem definido** | Usuários, credenciais, sessões; tokens curtos e refresh rotativo seguro |
| Validação | **FluentValidation** | Validação de entrada; invariantes permanecem no Domain |
| Storage | **S3 compatível (S3/R2)** | Fotos privadas, uploads controlados, miniaturas |
| Push | **Firebase Cloud Messaging + APNs** | Push Android/iOS, tokens por dispositivo |
| Mapa | **Google Maps SDK ou Mapbox** | Seleção de lugar e mapa; avaliar custos e termos antes da decisão |
| Background jobs | **Worker .NET + outbox persistente** | Distribuição de eventos, notificações e recompensas confiáveis |
| Tempo real | **SignalR, somente quando necessário** | Contadores/eventos em tempo real; feed pode iniciar por polling/paginação |
| Cache/filas | **Redis opcional, após demanda comprovada** | Rate limiting/cache; não obrigatórios no MVP |
| Telemetria | **OpenTelemetry + Sentry ou equivalente** | Erros, traces, logs sem payloads íntimos |
| Contêineres | **Docker Compose (local)** | API, PostgreSQL/PostGIS, worker e ferramentas |
| CI/CD | **GitHub Actions** | Build, testes, segurança, artifacts e deploy controlado |
| iOS release | **macOS + Xcode / CI macOS** | Assinar/compilar IPA e distribuir via TestFlight/App Store |
| Android release | **Android SDK/Gradle** | AAB assinado, testes internos e Google Play |

**Arquitetura escolhida:** monorepo; backend **monólito modular** (não microserviços), módulos isolados por domínio; Flutter **feature-first**. Não introduzir CQRS/MediatR, repositório genérico ou outras abstrações automaticamente: usar somente quando justificadas. API versionada (`/api/v1`), contratos explícitos e OpenAPI.

**Execução local no Windows/WSL2:** Flutter/Android SDK + Android Studio/Emulator no Windows, API/DB no WSL2 via Docker; configurar acesso à API a partir do emulador (host Android `10.0.2.2` quando apropriado e porta WSL exposta ao Windows). Testar em Android físico; para iOS, macOS/Xcode ou serviço de build com testes em dispositivo real/TestFlight. Nunca presumir que um build iOS pode ser feito nativamente no Windows.

---

# 2. Estrutura obrigatória de pastas

```text
trono/
├── AGENT.md                        # Regras de operação do agente (TDD e segurança)
├── AGENTS.md                       # Arquitetura real e contexto atualizado
├── AGENTS_references.md            # Índice compacto de referências
├── README.md                       # Instalação e comandos
├── .editorconfig
├── .gitignore
├── .env.example                    # Somente chaves de exemplo, nunca segredos
├── docker-compose.yml
├── apps/
│   ├── mobile/
│   │   ├── android/
│   │   ├── ios/
│   │   ├── assets/
│   │   │   ├── illustrations/
│   │   │   ├── badges/
│   │   │   ├── animations/
│   │   │   ├── fonts/
│   │   │   └── icons/
│   │   ├── lib/
│   │   │   ├── main.dart
│   │   │   ├── app/
│   │   │   │   ├── app.dart
│   │   │   │   ├── router/
│   │   │   │   └── bootstrap/
│   │   │   ├── core/
│   │   │   │   ├── network/
│   │   │   │   ├── security/
│   │   │   │   ├── storage/
│   │   │   │   ├── errors/
│   │   │   │   ├── design_system/
│   │   │   │   ├── localization/
│   │   │   │   └── widgets/
│   │   │   └── features/
│   │   │       ├── auth/
│   │   │       ├── onboarding/
│   │   │       ├── home/
│   │   │       ├── profiles/
│   │   │       ├── friendships/
│   │   │       ├── leagues/
│   │   │       ├── timer/
│   │   │       ├── records/
│   │   │       ├── feed/
│   │   │       ├── comments/
│   │   │       ├── map/
│   │   │       ├── quests/
│   │   │       ├── achievements/
│   │   │       ├── leaderboard/
│   │   │       ├── notifications/
│   │   │       ├── moderation/
│   │   │       └── settings/
│   │   ├── test/                   # Testes unitários e de widgets espelhando lib/
│   │   ├── integration_test/       # Fluxos E2E Flutter
│   │   ├── pubspec.yaml
│   │   └── analysis_options.yaml
│   └── api/
│       ├── Trono.slnx
│       └── src/
│           ├── Trono.Api/          # Composition root, middleware, DI e OpenAPI
│           ├── Trono.BuildingBlocks/
│           ├── Trono.Worker/       # Outbox, notificações e tarefas agendadas
│           ├── Trono.Modules.Identity/
│           ├── Trono.Modules.Profiles/
│           ├── Trono.Modules.Friendships/
│           ├── Trono.Modules.Leagues/
│           ├── Trono.Modules.Records/
│           ├── Trono.Modules.Feed/
│           ├── Trono.Modules.Locations/
│           ├── Trono.Modules.Gamification/
│           ├── Trono.Modules.Notifications/
│           ├── Trono.Modules.Media/
│           └── Trono.Modules.Moderation/
├── tests/
│   └── api/
│       ├── Trono.UnitTests/
│       ├── Trono.IntegrationTests/
│       └── Trono.ArchitectureTests/
├── infrastructure/
│   ├── docker/
│   ├── nginx/
│   ├── database/
│   └── observability/
├── scripts/
├── docs/
│   ├── product/
│   ├── architecture/
│   ├── features/
│   ├── api/
│   ├── security/
│   ├── testing/
│   ├── releases/
│   └── decisions/                  # ADRs
└── .github/workflows/
    ├── api-ci.yml
    ├── mobile-ci.yml
    └── release.yml
```

**Padrão módulo backend:** `Domain/{Entities,ValueObjects,Events,Rules}`, `Application/{UseCases,Contracts,Validators}`, `Infrastructure/{Persistence,Integrations}`, `Presentation/Endpoints`. Imports/dependências devem apontar para o núcleo: Domain independe de infraestrutura; Application depende de Domain; Infrastructure implementa portas; Api é a raiz de composição. Evitar referenciar módulos por tabelas/entidades internas: comunicar por contratos ou eventos.

**Padrão feature Flutter:** `domain/{entities,repositories}`, `data/{datasources,models,repositories}`, `presentation/{pages,widgets,providers}`; omitir pastas vazias e camadas desnecessárias. Estado de tela não contém regras críticas de concessão de pontos.

**Documentação do repositório:** `AGENT.md` = *política*; `AGENTS.md` raiz = estado real + decisões; `AGENTS_references.md` = caminhos/índice; `AGENTS.md` local = contexto do módulo. Atualizar os arquivos afetados após cada alteração significativa, sem inventar histórico.

---

# 3. Mapa de navegação e telas

**Bottom navigation (usuário autenticado):** `Feed | Mapa | [Registrar] | Missões | Perfil`. Em cada aba preservar estado quando viável. Acesso a notificações no topo, pesquisa de amigos/grupos e configurações via perfil.

## 3.1 Fluxo de entrada e autenticação

| Tela | Conteúdo | Ações/regras |
|---|---|---|
| Splash/bootstrap | Logo, verificação de versão/sessão | Roteamento correto sem expor conteúdo privado; loading/erro/offline |
| Boas-vindas | Proposta de valor e tom humorístico | Entrar, criar conta, política/termos |
| Cadastro | E-mail ou provedor social, apelido, consentimentos | E-mail único, validações, idade/classificação conforme política, não publicar por padrão |
| Login | Credenciais, recuperar acesso | Limite de tentativas, erros genéricos, refresh seguro |
| Onboarding | Avatar, apelido, preferências | Pular itens opcionais; explicar privacidade e fotos |
| Convite | Link/código de amigo ou liga | Validade, antifraude, convite pendente e aceite consciente |
| Permissões | Push, fotos e localização contextual | Solicitar **somente** quando o recurso for usado; negou = fluxo alternativo |

## 3.2 Feed (aba inicial)

**Cabeçalho:** nome do grupo/feed, seletor `Amigos` ou `Minha liga`, notificações e acesso a convites.  
**Cards:** avatar, nome, título, texto, data relativa, tags (ex.: ☢️ Nuclear, 💣 Torazona, 🏢 Trabalho), duração **se compartilhada**, mídia com aviso e desfoque opcional, local **se compartilhado**, reações temáticas, contagem de comentários, menu `denunciar/bloquear/ocultar`.  
**Ações:** reagir/desfazer; comentar; abrir perfil; abrir local compartilhado; editar/excluir própria postagem; paginar.  
**Estados:** sem amigos, feed vazio, erro, carregando, offline/retentar, conteúdo removido, autor bloqueado.  
**Eventos automáticos:** insígnia rara, missão coletiva, temporada encerrada — somente se usuário/grupo autorizou eventos no feed.  
**Ordenação MVP:** cronológico inverso; sem algoritmo opaco de recomendação.

## 3.3 Registrar / Timer

**Entrada:** botão central abre escolha `Iniciar cronômetro` ou `Registrar agora`.  
**Cronômetro:** tempo decorrido, pausar/retomar se o produto decidir suportar, finalizar, descartar com confirmação; persistir `startedAt` e estado de sessão para sobreviver background/restart; durações implausíveis pedem revisão; nunca incentivar permanecer no vaso.  
**Registro manual:** selecionar data/hora retroativa dentro de limite definido, informar duração opcional, salvar.  
**Finalização — campos (maioria opcionais):**

| Categoria | Valores iniciais | Observações |
|---|---|---|
| Tipo/apelido | Bode, Serpentão, Torazona, Cachoeira, Pedregulhos, Erupção | Catálogo humorístico com ilustrações próprias |
| Tamanho percebido | Mini, pequeno, médio, grande, colossal | Subjetivo; não utilizado para diagnóstico |
| Cheiro | Inofensivo, suspeito, tóxico, nuclear, extinção em massa | Autodescrição, não sensor/medição real |
| Sensação | Libertador, ardido, sofrido, bomba atômica, lançamento espacial | Multiseleção limitada |
| Consistência | Pedrinhas, firme, macio, pastoso, líquido | Se futuramente usar escala clínica, rotular separadamente e com cuidado |
| Cor | Marrom, clara, amarela, verde, vermelha, preta, outra | Alerta discreto sobre sinais de atenção, sem diagnóstico |
| Higiene | Papel, ducha, bidê, banho, outro | Sem incentivo a gasto/desperdício |
| Foto | 0..N por limite de produto (MVP sugerido: 1) | Opcional, consentimento, revisão/moderação e thumbnail protegida |
| História | Título, texto curto, emojis | Opcional; limitar comprimento e conteúdo inadequado |
| Local | Nenhum, categoria, lugar selecionado, cidade aproximada | **Privado inicialmente**; publicação requer escolha explícita |
| Audiência | Privado, amigos, liga selecionada | Privado é padrão; controle independente de mídia/local |

**Passos UX:** 1) rápido: finalizar/salvar em segundos; 2) `Personalizar relatório` em seções; 3) tela de revisão com preview **exato do que será visível**; 4) salvar registro; 5) publicar opcionalmente; 6) mostrar XP/conquista somente se aplicável. É permitido editar/excluir, com política para reversão de recompensa.

## 3.4 Histórico pessoal

Calendário de registros; filtros por data, características, com/sem foto, local; visualização detalhada, editar, excluir, exportar dados; indicadores privados de frequência e duração; mensagens não médicas. Nada é público por estar no histórico.

## 3.5 Mapa (Passaporte do Trono)

**Mapa pessoal:** somente marcadores do usuário autenticado, filtros por cidade/categoria/data, marcador agrupado, lista acessível alternativa, total de locais únicos e badges.  
**Mapa social:** somente lugares de postagens visíveis ao usuário e com local explicitamente compartilhado; abrir a postagem a partir de um marcador.  
**Adicionar local:** selecionar estabelecimento reconhecido ou região aproximada; nunca salvar GPS exato desnecessariamente; usuário pode editar/remover; deduplicação por `placeId` quando confiável e identidade de lugar.  
**Privacidade:** compartilhar *nome do estabelecimento* ou *região aproximada*; nunca publicar coordenadas precisas ou localização em tempo real por padrão; suprimir endereço residencial e padrões sensíveis; revogação remove acesso social e links.  
**Conquistas:** primeiro shopping, cinco lugares únicos, três cidades, primeiro aeroporto, etc. GPS sozinho **não** comprova presença; não afirmar antifraude forte para localizações declaradas.

## 3.6 Missões (central do jogo)

Abas `Hoje | Semana | Temporada | Coleção`; cards mostram nome, ícone, requisitos, progresso, recompensa, período e status; quests bloqueadas explicam pré-requisitos; histórico das conclusões. **Nunca exigir frequência intestinal específica para manter streaks ou progresso essencial.**

**Missões candidatas** (ajustar por segurança e testes com usuários):

| Nome | Condição verificável | Recompensa ilustrativa | Observação |
|---|---|---|---|
| Primeiro relatório | Criar 1 registro válido | 10 XP | Apenas primeira vez |
| Relatório completo | Completar descrição opcional (sem foto) | 5 XP | Limite diário |
| Dupla descarga | Registrar 2 atividades espontâneas em um dia | Insígnia humorística | Não recomendar forçar evacuação; sem streak/pressão |
| Banho de Rei | Marcar banho que já ocorreu | Insígnia ocasional | Nunca exigir banho após cada registro |
| Semana do Papel | Registrar uso de papel ao longo da semana | Insígnia | Não recompensar esgotar rolos/desperdício |
| Turista do Trono | Cinco lugares únicos declarados | 100 XP | Antispam e locais elegíveis |
| Explorador Urbano | Três categorias de estabelecimento | Badge | Local compartilhado não obrigatório |
| Rei da Resenha | Receber reações de 3 amigos distintos | 50 XP | Combate a reações artificiais |
| Primeira Liga | Entrar em uma liga privada | 15 XP | Apenas uma vez |
| Time Unido | Liga completa objetivo coletivo | Badge coletivo | Contagem por membro/ação elegível |

**Motor de missões:** catálogo versionado (`QuestDefinition` + versão de regra), janela temporal/timezone, pré-requisitos, `QuestProgress`, `QuestCompletion`, `RewardLedger`; processamento idempotente por evento. Definir explicitamente se progresso de localização exige post público (recomendação: **não**; considerar privado elegível). Missão cancelada não remove recompensas já legitimamente concedidas sem uma política anunciada.

## 3.7 Ranking, ligas e temporadas

**Liga privada:** proprietário/moderador/membro, convite por código expiráveis, permissões de admissão, expulsão/saída, limites de membros, bloqueios.  
**Ranking:** pontos de temporada separados do XP vitalício; placares `Participação`, `Exploração`, `Social`; nunca ranking baseado puramente em quantidade ou duração de evacuações.  
**Temporada:** sugestão de 4 semanas, início/fim versionado por liga, congelamento de placar e publicação opcional dos vencedores; novos participantes podem entrar durante a temporada sob regra transparente.  
**Missões coletivas:** progressão por ações válidas de membros, limite antispam, recompensas concedidas a elegíveis uma vez.  
**Títulos:** `Cagão da Semana`, `Explorador de Tronos`, `Lenda da Porcelana`; títulos sazonais podem expirar, insígnias adquiridas permanecem no perfil, salvo correção por fraude.

## 3.8 Perfil e personalização

Avatar, apelido, bio curta, nível, barra de XP, até 3 insígnias em destaque, estatísticas visíveis apenas conforme controles, grid de publicações para audiência autorizada, coleção de conquistas, histórico e mapa **privados** em áreas separadas. Dono vê editar perfil, privacidade, exportar/apagar dados, gerenciar liga e dispositivos. Usuário bloqueado não visualiza feed/perfil privado/mapa.

## 3.9 Amigos, convites e liga

Busca por apelido/código com limite para evitar enumeração de usuários; solicitar/aceitar/recusar amizade, desfazer, bloquear; link de convite com token de uso/expiração definidos; notificações controláveis. Só amigos/grupo autorizado veem posts no MVP.

## 3.10 Comentários, reações, fotos e denúncias

Reações customizadas (😂 KKK, ☢️ Nuclear, 🫡 Respeito, 💀 RIP Banheiro, 🏆 Obra-prima, 🔥 Incendiário). Uma reação ativa por usuário por tipo conforme regra escolhida; toggles idempotentes; contagens derivadas. Comentários editáveis/excluíveis, sem anexos no MVP. Fotos sujeitas a limites MIME real, tamanho, transformação, remoção de EXIF/metadados e, se necessário, moderação; pré-visualização borrada/aviso de conteúdo. Denúncia, bloqueio e remoção acessíveis; políticas de conteúdo e sanções documentadas; não tolerar exposição não consensual de terceiros.

## 3.11 Notificações e configurações

Push: novo comentário/reação (agrupados), convite/aceite, missão coletiva, conquista de amigo (opt-in), fim da temporada. Preferências granulares, horários silenciosos e opção de desligar; notificações push jamais contêm detalhes íntimos no lock screen. Configurações: conta, sessão/dispositivos, privacidade/audiência padrão, mídia, mapa, notificações, bloqueados, dados/exportação, exclusão, termos e suporte.

## 3.12 Estados transversais de UX

Para **toda tela** definir: vazio, loading, erro, offline, tentativa novamente, sessão expirada, acesso negado, conteúdo removido, acessibilidade (sem depender apenas de cor/emoji, leitor de tela, alvo de toque, contraste, texto escalável), permissões negadas e operações em progresso para evitar clique duplicado. Deep links nunca contornam autorização.

---

# 4. Modelo de domínio e fronteiras

| Módulo | Entidades/agregados centrais | Responsabilidade |
|---|---|---|
| Identity | User, Credential/ExternalLogin, Session, Device | Autenticação e ciclo de sessão |
| Profiles | Profile, ProfileBadgeDisplay, Preferences | Identidade visível e preferências |
| Friendships | FriendshipRequest, Friendship, Block | Grafo social e bloqueios |
| Leagues | League, LeagueMember, Invite, Season, SeasonMembership | Grupos privados e períodos |
| Records | PoopRecord, TimerSession, RecordAttributes | Histórico privado e integridade temporal |
| Feed | Post, AudienceGrant, Comment, Reaction | Publicação, visibilidade e interação |
| Locations | Place, UserPlaceVisit, SharedPlaceProjection | Lugar pessoal e projeção publicada |
| Gamification | QuestDefinition, QuestProgress, AchievementDefinition, UserAchievement, XpLedger, SeasonScoreLedger | Regras e recompensas idempotentes |
| Media | MediaAsset, UploadIntent, ModerationState | Upload/thumbnail/autorização |
| Notifications | Notification, DevicePushToken, DeliveryAttempt | Inbox e push |
| Moderation | Report, BlockCase, EnforcementAction, AuditEvent | Denúncia, sanções e auditoria |

**IDs:** `Guid`/UUID tipado no backend, serializado como string em JSON; PostgreSQL `uuid`. Timestamps UTC, converter para timezone do usuário onde importar para janela diária. Registros e posts são objetos distintos: excluir/privatizar postagem não apaga necessariamente o registro pessoal; excluir registro deve seguir política explícita de referências, mídias e recompensas. Banco mínimo com migrações EF Core versionadas; índices obrigatórios em `user_id`, relações, `created_at`, audiências e chaves idempotentes; índices geográficos somente onde necessário.

## 4.1 Eventos de domínio e fluxo de integração

```text
Usuário finaliza e salva registro
  → Records valida e persiste PoopRecord em transação
  → publica RecordCreated no outbox (apenas metadados mínimos)
  → Worker entrega evento com idempotência
  → Gamification avalia quests elegíveis e ledger de recompensas
  → Notifications cria alerta apenas se apropriado

Usuário escolhe compartilhar
  → Feed verifica propriedade e audiência
  → Post criado com projeção permitida dos campos
  → Media/Locations fornecem SOMENTE conteúdo autorizado
  → Amigos autorizados veem post
  → Interações geram eventos separados para missões sociais
```

**Entrega pelo menos uma vez:** consumidores idempotentes, chave `eventId` + `consumer`, unique constraints para XP/recompensa; transação local/Outbox para evitar recompensa perdida ou duplicada. Não transmitir dados de saúde/foto/GPS bruto em eventos genéricos.

## 4.2 Regras de negócio invariantes (RB)

- **RB-01:** Usuário só modifica seu registro; administradores não têm leitura indiscriminada de conteúdo privado.
- **RB-02:** Registro é privado mesmo quando foi usado para completar missões.
- **RB-03:** Publicar exige seleção explícita de audiência; alterações em audiência revogam acesso imediatamente.
- **RB-04:** Publicação social não pressupõe autorização para foto, duração ou coordenadas; visibilidade de campos é independente.
- **RB-05:** Quem não possui acesso ao post não pode consultar seus comentários, mídia, local ou contagens protegidas por rota alternativa.
- **RB-06:** Bloqueio prevalece sobre amizade, convite e participação em liga para interações entre as partes.
- **RB-07:** Apenas local explicitamente escolhido/compartilhado pode aparecer para terceiros; por padrão nem GPS preciso nem localização em tempo real.
- **RB-08:** Nova visita é deduplicada por identidade estável de local/normalização; lugares declarados não são prova de presença.
- **RB-09:** XP, moedas, missões e insígnias são concedidos **somente no servidor**, uma única vez por evento elegível.
- **RB-10:** Contagens de registros e cronômetro não geram vantagem ilimitada nem estimulam padrões intestinais prejudiciais.
- **RB-11:** Temporadas mantêm histórico imutável ou auditável de encerramento e cálculo de pontos.
- **RB-12:** Fotos em bucket privado; URL temporária apenas após autorização; EXIF removido antes da disponibilidade.
- **RB-13:** Edição/exclusão têm efeitos documentados sobre post, feed, localização, score e reward ledger (proposta inicial: não contar conteúdo invalidado para progresso futuro e usar reversão auditada apenas em abuso comprovado).
- **RB-14:** Exclusão de conta revoga sessões, remove identidade pública e processa dados associados conforme política de retenção legal/operacional divulgada.
- **RB-15:** Erros e telemetria não revelam descrições íntimas, fotos ou geolocalização exata.
- **RB-16:** Toda escrita mutável relevante suporta proteção contra duplicidade (idempotency key ou constraints transacionais).
- **RB-17:** Moderadores de liga gerenciam o grupo, mas não ganham acesso a registros privados de membros.
- **RB-18:** Mensagens automáticas no feed não revelam conquistas baseadas em registros privados sem consentimento.

---

# 5. API REST inicial (contratos candidatos, não implementados)

**Padrão:** `/api/v1`, JSON, paginação por cursor no feed, HTTP status correto, `ProblemDetails` para erros, `traceId`, validação via FluentValidation em comandos que exigem, validação de domínio nas entidades, autorizador por recurso. Não obrigar `ResponseWrapper` específico de outro produto. OpenAPI deve ser gerado e versionado.

| Método | Rota proposta | Objetivo |
|---|---|---|
| POST | `/auth/register` | Criar conta |
| POST | `/auth/login` | Autenticar |
| POST | `/auth/refresh` | Rotacionar refresh token |
| POST | `/auth/logout` | Revogar sessão |
| GET/PATCH | `/me/profile` | Ler/editar perfil |
| DELETE | `/me/account` | Solicitar exclusão de conta |
| GET/POST | `/friends` e `/friends/requests` | Listar/solicitar |
| POST | `/friends/requests/{id}/accept` | Aceitar solicitação |
| POST | `/users/{id}/block` | Bloquear |
| GET/POST | `/leagues` | Listar/criar ligas |
| POST | `/leagues/{id}/invites` | Criar convite |
| POST | `/league-invites/{token}/accept` | Entrar em liga |
| GET/POST | `/records` | Listar/criar registros privados |
| GET/PATCH/DELETE | `/records/{id}` | Ler/editar/excluir registro |
| POST | `/timer-sessions` | Iniciar sessão de tempo |
| POST | `/timer-sessions/{id}/finish` | Finalizar sessão idempotente |
| GET/POST | `/posts` | Consultar/criar feed |
| PATCH/DELETE | `/posts/{id}` | Editar/excluir própria postagem |
| GET/POST | `/posts/{id}/comments` | Ler/comentar |
| PUT/DELETE | `/posts/{id}/reactions/{type}` | Reagir/remover |
| GET | `/map/me/places` | Mapa privado |
| GET | `/map/shared-places` | Lugares autorizados ao observador |
| GET | `/quests` e `/quests/progress` | Catálogo/progresso |
| GET | `/achievements/me` | Insígnias |
| GET | `/leagues/{id}/leaderboard` | Ranking autorizado |
| POST | `/media/upload-intents` | Solicitar upload controlado |
| POST | `/reports` | Denunciar |
| GET/PATCH | `/me/settings` | Preferências |
| GET | `/notifications` | Caixa de notificações |

**Contratos de exemplo** (esquema ilustrativo; sensíveis opt-in):

```json
{
  "occurredAt": "2026-10-13T15:00:00Z",
  "durationSeconds": 245,
  "attributes": {
    "shape": "torazona",
    "size": "large",
    "smell": "nuclear",
    "color": "brown",
    "sensations": ["relief"],
    "hygiene": ["paper"]
  },
  "privatePlaceId": null,
  "notes": ""
}
```

```json
{
  "recordId": "2c5f05f0-4d5a-4d22-a2ea-42419181c111",
  "title": "Acidente no escritório",
  "body": "História fictícia",
  "audience": {"type": "league", "leagueId": "1d00a981-ff6f-43bd-b8f8-4d21441cad89"},
  "sharedFields": ["shape", "size", "smell"],
  "sharePlace": false,
  "mediaIds": []
}
```

**Padrões não funcionais de API:** autenticação/autorização sistemática, paginação e limites, rate limiting em login/comentários/convites/uploads, idempotency keys para registros/finished/quests; concorrência otimista para edição quando relevante, transações para efeitos críticos, testes de autorização contra IDOR e vazamento por mídia/rotas de mapa.

---

# 6. TDD-first e garantia de qualidade — obrigatório

**RED → GREEN → REFACTOR em TODA alteração comportamental:**

1. Ler `AGENT.md`, `AGENTS.md`, `AGENTS_references.md` e instruções locais aplicáveis.
2. Especificar comportamento, regras RB afetadas e critérios de aceitação.
3. Criar teste automatizado que **falhe pelo motivo correto** antes do código de produção.
4. Executar teste e registrar evidência RED.
5. Implementar a menor mudança possível; executar até GREEN.
6. Refatorar preservando testes verdes; executar suites relevantes.
7. Atualizar documentos/contratos e registrar decisões; reportar comandos e resultados **reais**, jamais inventados.

**Testes backend:** xUnit, mocks apenas em fronteiras, integração com PostgreSQL/PostGIS real via Testcontainers quando pertinente, WebApplicationFactory para HTTP/autorização; testes de arquitetura para dependências. Preferir asserts nativos ou biblioteca compatível com a licença escolhida.  
**Testes Flutter:** unitários de mappers/use cases/notifiers, widget tests de telas e estados, integration_test para jornadas, golden tests em telas estáveis selecionadas; testes físicos de câmera/GPS/background e acessibilidade.  
**Casos críticos de segurança:** usuário B não acessa registro de A; link direto de post não ignora permissão; foto não é pública por URL permanente; bloqueio e revogação de audiência efetivos; duplicidade não multiplica XP; falha/retry de worker não gera inconsistências; GPS não vaza nos payloads sociais.

**Comandos-alvo após criação dos projetos:**

```bash
# Backend (a partir da raiz)
dotnet restore apps/api/Trono.slnx
dotnet build apps/api/Trono.slnx --no-restore
dotnet test apps/api/Trono.slnx --no-build

# Flutter (a partir de apps/mobile)
flutter pub get
flutter analyze
flutter test
# smoke E2E com emulator conectado:
flutter test integration_test
```

**Não declarar conclusão** se build/testes exigidos falharem; se não for possível executar (SDK/emulador ausente), documentar impedimento, alcance do trabalho e estado real, sem afirmar testes aprovados.

**Definition of Done:** critérios de aceitação atendidos; testes RED→GREEN registrados; testes relevantes e lint passam; privacidade/autorização verificadas; migrations e rollback considerados; observabilidade sem dados sensíveis; UX de loading/erro/vazio coberta; docs atualizadas; revisão de código; sem segredos no repositório.

---

# 7. Segurança, privacidade, conteúdo e conformidade

1. **LGPD e minimização:** inventário de dados, base legal apropriada, consentimentos quando necessários, política de retenção, exportação/eliminação, canal de suporte; examinar se atributos intestinais caracterizam dados pessoais sensíveis no contexto e proteger como tal por padrão.
2. **Fotos:** upload por URL assinada curta e com autorização, MIME por conteúdo, tamanho/resolução limitados, remover EXIF, scans e moderação proporcionais, bucket privado, CDN sem cache público inadequado, revogação de links.
3. **Localização:** permissões contextuais, sem background location, guarda granular, nomes/áreas aproximadas, proteção de casa/trabalho, nada de GPS preciso no feed por padrão; localização compartilhada depende de consentimento específico.
4. **Segurança de API:** HTTPS, secrets manager/variáveis seguras, hashing de senha padrão robusto via Identity, refresh revogável, controle de acesso por objeto e grupo, mitigação OWASP API Top 10, rate limiting e auditoria mínima.
5. **Moderação UGC:** termos, canais de denúncia, bloquear usuários, remover posts, revisão e recursos; definir regras específicas para fotos de fezes, nudez, pessoas identificáveis e assédio; política de conteúdo compatível com lojas.
6. **Proteção a menores:** definir classificação etária, exigências de controle e elegibilidade de uso com aconselhamento jurídico e políticas das lojas; não pressupor aceitação de qualquer conteúdo explícito.
7. **Analytics responsáveis:** métricas agregadas, eventos sem conteúdo íntimo/GPS, opções de privacidade adequadas, nenhuma venda de dados de saúde.
8. **Abuso/gamificação:** throttling, rate limits, antifraude proporcional, detecção de duplicidade e ledger auditável; não criar incentivos para comportamento fisiológico nocivo.
9. **Backups e incidentes:** backups criptografados, restauração testada, segregação ambiente/dev/prod, plano de incidente e proteção de contas administrativas.

---

# 8. Infraestrutura, observabilidade e DevOps

**Local:** compose com `api`, `worker`, `postgres-postgis`, migrações executadas de modo controlado; uploads em bucket de desenvolvimento ou emulador S3 para testes. **Staging:** domínio, TLS, dados fictícios, credenciais isoladas, push configurado para testes. **Produção:** API HTTPS, banco com backup e acesso privado, storage privado, reverse proxy, monitoramento, deploy idempotente e rollback. CDN somente de recursos públicos; fotos protegidas por autorização.

**Pipeline PR:** format/lint → unit → integration/architecture → Flutter analyze/test → build Android debug e artefatos quando pertinente → security scan de dependências e secrets. **Release:** versionamento semântico, changelog, migration review, Android AAB assinado via segredos de CI; iOS build/assinatura em runner macOS, distribuição TestFlight; aprovação nas lojas sob contas oficiais. Nenhum deploy automático em produção sem aprovação na fase inicial.

**Telemetria:** crash-free sessions, latência feed/timer/mapa, taxa de falha em upload, processamento outbox, tempo de entrega de push, fila, banco, uso de storage; redigir/redact PII. Guardar logs com retenção limitada.

**Objetivos preliminares (hipóteses):** registro rápido em ~5–10 s sem personalização; feed responsivo sob redes móveis comuns; sem perda de timer no background; latência e SLOs reais definidos após testes de carga e dispositivos.

---

# 9. Roadmap orientado a fatias verticais

Cada fase entrega API + banco/migration + UI + testes + documentação. Fases não são sinônimo de construir telas sem lógica.

| Fase | Entregáveis | Critério de aceite |
|---|---|---|
| **F0 Fundação** | Monorepo, SDKs, emulador Android, .NET, compose PostGIS, CI, `AGENT.md`, docs | `flutter test`, `dotnet test`, builds e API healthcheck executáveis |
| **F1 Identidade** | Cadastro/login/sessão, onboarding, perfil, preferências, exclusão | Sessões seguras, isolamento entre usuários, fluxos E2E |
| **F2 Core privado** | Timer resiliente, registro manual, atributos, edição/exclusão, calendário/histórico | Registro completo sem rede social e sem perda de dados; testes de tempo/timezone |
| **F3 Social** | Amizades, grupos, convites, postagens, feed, reações, comentários | Acesso de terceiros limitado por audiência e bloqueio; estados de UI |
| **F4 Mídia e mapa** | Fotos privadas/moderadas, local pessoal, compartilhamento específico, pins e listas | Nada de vazamento de foto/local por URL ou mudança de audiência |
| **F5 Gamificação** | Catálogo quests, XP/ledger, conquistas, progresso e missões sociais/exploração | Idempotência e antispam comprovados em testes de integração |
| **F6 Retenção** | Notificações, ligas, temporada/ranking básico, eventos coletivos | Notificações opt-in, cálculo auditável, temporadas corretas |
| **F7 Beta** | Polimento, acessibilidade, segurança, carga, observabilidade, moderação, políticas | Teste com pequenos grupos; corrigir defeitos prioritários |
| **F8 Lojas** | Build AAB/IPA, ficha, capturas, política/termos, avaliação etária, testes Apple/Google | Submissão, revisão e disponibilização gradual |
| **F9 Evolução** | Avatares, moedas cosméticas, badges extras, retrospectivas, eventos | Guiado por métricas e feedback reais |

## 9.1 Backlog inicial priorizado (histórias)

- **US-01:** como usuário, criar conta e ver somente meus dados (P0).
- **US-02:** iniciar e finalizar timer mesmo após alternar aplicativos (P0).
- **US-03:** registrar manualmente com campos básicos ou descrição completa (P0).
- **US-04:** manter meus registros privados por padrão e editá-los/excluí-los (P0).
- **US-05:** convidar amigo e formar liga privada (P0).
- **US-06:** publicar registro com audiência específica, sem foto/local se não autorizar (P0).
- **US-07:** reagir, comentar, denunciar e bloquear (P0).
- **US-08:** compartilhar lugar selecionado e abrir mapa a partir do post (P0).
- **US-09:** concluir missões iniciais e receber XP uma vez (P0).
- **US-10:** visualizar perfil, insígnias e histórico autorizado (P0).
- **US-11:** receber push configurável de interações (P1).
- **US-12:** disputar temporada com amigos por critérios saudáveis (P1).
- **US-13:** desbloquear cosméticos/retrospectivas (P2).

## 9.2 Exemplo de critérios de aceitação testáveis

**US-06 Publicar:** Dado registro privado de A, quando A cria postagem visível apenas à liga X com `sharePlace=false` e `mediaIds=[]`, então membro autorizado de X vê apenas atributos selecionados; usuário fora de X recebe 403/404 sem inferir existência; link direto não contorna permissão; A consegue revogar acesso e antigos membros deixam de ter acesso imediatamente.

**US-09 XP:** Dado evento `RecordCreated` com id `E1`, quando processado duas vezes pelo worker, então o ledger contém uma única concessão para ação elegível, o progresso não duplica e o relatório de conclusão mantém a mesma pontuação.

**US-02 Timer:** Dado início persistido no instante T1, quando o app passa ao background, reinicia e reabre em T2, então mostra duração calculada de T2−T1 sem depender de contador rodando em segundo plano e permite correção da duração registrada.

**US-08 Local:** Dado registro com lugar privado P, quando usuário opta por publicar somente região aproximada, então endpoint social não retorna coordenadas precisas nem nome do lugar P; retirar compartilhamento remove pins públicos existentes.

---

# 10. Design system e experiência

**Tom:** humor brasileiro, irreverente, sem infantilizar nem transformar o uso em humilhação. Design lúdico e legível. Ilustrações próprias para Bode, Torazona, Bomba Atômica, Nuclear etc.; não copiar assets e UI dos concorrentes. **Componentes:** bottom navigation, card de postagem, tag de característica, seletor de descrições, modal de audiência, card de missão/progresso, badge de conquista, marcador de mapa, empty states, alertas de segurança, preview de mídia. **Design tokens:** cores, tipografia, espaçamento, raios, elevações, ícones e estados; temas claro/escuro e suporte a texto ampliado. **Performance:** paginar feed, lazy loading de imagens, compressão/thumbnail, preservar rolagem e evitar efeitos pesados no mapa. Validação com aparelhos de entrada e redes lentas.

**Não construir tudo em uma única tela:** o formulário de registro é progressivo e editável, com seleção rápida para o uso casual; mapa e lista de lugares representam o mesmo estado; navegação respeita deep links autenticados.

---

# 11. Métricas de produto e validação

**North star candidata:** número semanal de interações significativas entre amigos ativos (não número de evacuações). Medir aquisição por convites, ativação (primeiro registro ou primeira interação), D1/D7/D30, retenção de usuários com e sem amigos, percentual de posts com interação, quests iniciadas/concluídas, grupos ativos, tempo para registrar, denúncias por 1.000 posts, taxa de falha em uploads e crash-free sessions. Evitar registrar tags íntimas e coordenadas brutas em analytics.

**Piloto:** 3–5 ligas com grupos pequenos, entrevistas, observar frequência de uso e desconfortos; não presumir que um recurso é divertido apenas porque foi pedido. Ajustar pontos/telas antes de escalar.

---

# 12. Próximas decisões de produto para lapidação

1. Nome, estilo visual, faixa etária e política de mídia explícita.
2. Especificação precisa do sistema de audiência: amigo vs liga, múltiplas ligas, herança/revogação.
3. Catálogo final de atributos humorísticos, ilustrações, duração, tipos, cores e limites de mídia.
4. Algoritmos de missões e XP, antispam, temporalidade e impacto de exclusões.
5. Política de lugar: nome de estabelecimento, cidade aproximada e endereços sensíveis.
6. Privacidade por campo, exportação de histórico e retenção após exclusão.
7. Contratos OpenAPI, modelo físico PostgreSQL, índices, migrations e eventos outbox.
8. Wireframes por tela e protótipo Flutter navegável antes de polimento visual.
9. Seleção de mapas/storage e estudo de custos conforme volume de uso.
10. Segurança, moderação, requisitos de publicação e beta testers.

---

# 13. Instruções iniciais para implementação por agente

> **Obrigatório:** antes de executar qualquer tarefa, ler `AGENT.md`, `AGENTS.md`, `AGENTS_references.md`, o presente plano e `AGENTS.md` das pastas afetadas. Onde o repositório ainda não existir, criar estrutura e documentação de estado real sem alegar código pronto. Respeitar TDD RED → GREEN → REFACTOR, modificação mínima, testes verificáveis, confidencialidade e requisitos de privacidade. Não gerar módulos inteiros antecipadamente sem uma história e critérios de aceitação. Não assumir que exemplos JSON ou nomes de endpoint são decisões irrevogáveis.

**Primeiro sprint recomendado (F0):** inicializar repositório; fixar versões SDK; projetos Flutter e ASP.NET; configurar PostGIS local; health endpoint com teste de integração; um widget de tela inicial com widget test; rodar testes e build em CI; documentar passos Windows/WSL/Android Emulator; demonstrar app em aparelho virtual e API healthcheck.

**Segundo sprint (início F1):** escrever testes de registro/login/autorização, implementar o mínimo seguro, criar UI Flutter guiada por testes, validar integração em emulador; somente então avançar para registros privados.

**Critério final do plano:** produto e engenharia permanecem ligados: cada tela refere-se a casos de uso e cada caso de uso tem regras, estados de erro, autorização, testes e telemetria apropriados. Este plano é a base inicial que deverá ser refinada em épicos e ADRs, não uma implementação concluída.
