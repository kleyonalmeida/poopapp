# Prévia local do TRONO com Docker

Esta prévia abre as mesmas telas Flutter do app no navegador, para revisão mobile-first. Android/iOS continuam sendo os alvos do produto. Para iniciar, basta Docker com Compose; não é necessário Python nem SDKs Flutter/.NET/Android no host.

## Iniciar

Na raiz do repositório:

```sh
docker compose up -d
```

Abrir **https://localhost:8443**. Na primeira compilação o Docker baixa SDKs/pacotes; as seguintes reutilizam o cache. No DevTools do navegador, ativar a visualização de dispositivo e usar uma largura próxima de 390 px.

O Compose preserva `.env` e os volumes existentes, aguarda o PostgreSQL e executa `migrate` antes de iniciar a API. Em uma cópia nova do projeto, prepare `.env` a partir de `.env.example` e defina POSTGRES_PASSWORD uma única vez; o ambiente local atual já está configurado. Não usar essa configuração de desenvolvimento em produção.

Para reconstruir depois de alterar o código, executar `docker compose up -d --build`. Para aguardar a prontidão no próprio comando, acrescentar `--wait`. Os serviços `certificates` e `migrate` são de inicialização e terminam com código 0 quando concluem; isso é esperado. Se migrations falharem, API/web não iniciam.

## Certificado local

É esperado um aviso do navegador sobre o certificado de desenvolvimento. Autorize somente o certificado deste ambiente local ou importe sua cópia pública no navegador/sistema usado para acessar a prévia. O Compose não modifica a confiança global do computador.

Para exportar somente o certificado público:

```sh
docker compose cp web:/tls/localhost.crt /tmp/trono-localhost.crt
```

Não exportar/publicar a chave privada. No Windows, a confiança precisa ser estabelecida no navegador/sistema Windows, mesmo quando Docker é iniciado pelo WSL. O certificado abrange localhost/127.0.0.1; as portas são apenas de loopback, destinadas a esse computador.

## O que pode ser testado

Cadastro, login, própria conta, apelido, preferências, registros privados e exclusão usam a API e o banco reais do ambiente local. Não há contas prontas de demonstração: crie uma conta de desenvolvimento. O mínimo da senha continua em 12 caracteres.

A sessão da prévia fica somente na memória da página. Recarregar ou fechar exige novo login. Isso não muda o armazenamento seguro nativo Android/iOS e não representa validação de Keychain/Keystore. A recarga não garante logout remoto; use Sair para solicitar revogação da sessão no servidor.

## Comandos diretos

Com `.env` preparado, todo o ambiente sobe com:

```sh
docker compose up -d
```

O serviço `migrate` aplica Identity antes de Records, exclusivamente em Development; o processo da API não migra seu próprio banco. Novas execuções são idempotentes quando o schema já está atualizado. Para verificar o estado, `docker compose ps -a`; para parar os serviços preservando dados e certificado, `docker compose down`. Não adicionar `--volumes` no ambiente persistente.

| Endereço padrão | Uso |
|---|---|
| `https://localhost:8443` | Interface e API pela mesma origem |
| `https://localhost:8443/health/ready` | Banco/migrations prontos |
| `http://localhost:5080/health/live` | Sonda direta da API |
| `127.0.0.1:5432` | PostgreSQL do host |

As variáveis TRONO_WEB_PORT, TRONO_API_PORT e TRONO_DB_PORT permitem mudar as portas no `.env`. Como a prévia usa a própria origem, não precisa recompilar uma URL de API específica ao trocar a porta.

## Teste isolado

```sh
python3 scripts/test_docker_preview.py
```

Este teste requer Python 3. Cria um projeto Compose próprio, senha e portas aleatórias e executa o mesmo `docker compose up -d` do desenvolvedor, sem helper de inicialização. Verifica banco novo migrado, repetição preservando dados/certificado, bloqueio de API/web se a migração falhar, interface, TLS, PostGIS, autenticação, exclusão e falhas do proxy. No finally, remove somente os contêineres/volumes/imagens locais da fixture. Não usa contas do banco persistente.

Para a verificação de navegador, disponibilizar Playwright e Chrome para Node no ambiente de testes e executar `python3 scripts/test_docker_preview.py --browser`. PLAYWRIGHT_MODULE e CHROME_BIN podem indicar instalações existentes; o teste abre janela 390×844 e confia apenas na chave pública do certificado da própria fixture. Não usa `ignoreHTTPSErrors` global. Evidências em [validação](../testing/local-preview.md).
