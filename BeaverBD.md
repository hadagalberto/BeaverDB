Você é um desenvolvedor sênior full-stack especializado em DevOps e bancos de dados. Sua tarefa é criar, do zero, um sistema web simples (um “mini painel de banco de dados”) para eu ter controle básico sobre meus bancos de dados e servidores de banco de dados.

## Objetivo geral

Criar uma plataforma parecida com o PHPMyAdmin, porém bem mais simples, com foco em:

1. Criar e gerenciar **servidores de banco de dados**, incluindo:
   - Bancos relacionais: MySQL, PostgreSQL, SQL Server
   - Bancos NoSQL / cache: Redis, MongoDB
   - Com opção de subir esses servidores em containers Docker.

2. Criar e gerenciar **bancos de dados** dentro de cada servidor quando fizer sentido (MySQL, PostgreSQL, SQL Server, MongoDB).

3. Criar e gerenciar **usuários de acesso** para cada banco de dados, com permissões básicas (quando suportado pelo engine).

4. Permitir operações simples de visualização e manipulação das tabelas (para bancos relacionais) e coleções (para MongoDB), além de execução de queries/comandos simples.

O sistema também deve rodar em Docker.

---

## Stack e arquitetura desejadas

Escolha uma stack moderna e bem documentada e descreva no README o que foi escolhido. Sugestão (pode seguir essa, a menos que haja motivo forte para outra):

- **Backend**: 
  - Linguagem: C# (.NET 9 Web API)
  - Padrão de arquitetura em camadas (Controllers → Services → Repositories).
  - Implementar um “driver/provider” de conexão por tipo de servidor:
    - MySQL
    - PostgreSQL
    - SQL Server
    - Redis
    - MongoDB

- **Frontend**:
  - SPA com React + TypeScript.
  - Estilização com TailwindCSS.
  - Autenticação simples (login/senha) apenas para proteger o painel.
  - Telas para controle de usuários do painel.
  - Criar usuário admin caso ainda não exista de forma parecida como é feita no Portainer.

- **Banco interno do sistema**:
  - Um banco único (pode ser Postgres ou MySQL) apenas para armazenar:
    - Usuários do painel (login).
    - Lista de “servidores de banco de dados” cadastrados (incluindo SQL, Redis e MongoDB).
    - Configurações e metadados.

- **Containerização**:
  - Utilizar Docker e docker-compose.
  - Um `docker-compose.yml` principal que sobe:
    - Backend
    - Frontend
    - Banco interno do painel.
  - Serviços adicionais (no mesmo compose ou em outro) para subir instâncias de:
    - MySQL, PostgreSQL, SQL Server
    - Redis
    - MongoDB

Documente claramente no README como subir tudo com Docker.

---

## Funcionalidades detalhadas

### 1. Autenticação no painel

- Cadastro inicial de um usuário admin via seed/migration ou variável de ambiente.
- Login simples (email/usuário + senha).
- Proteção de todas as rotas internas do painel (exceto login).

---

### 2. Gestão de Servidores de Banco de Dados (SQL, Redis, MongoDB)

Na interface, uma tela “Servidores” deve permitir:

- Criar um “Servidor de Banco de Dados” com os campos:
  - Nome amigável
  - Tipo: `MySQL`, `PostgreSQL`, `SQL Server`, `Redis`, `MongoDB`
  - Host
  - Porta
  - Usuário administrador (quando aplicável)
  - Senha
  - Indicar se esse servidor é:
    - “Externo” (já existe e só quero conectar)
    - ou “Gerenciado via Docker” (o próprio sistema sobe o container)

- Backend deve:
  - Validar a conexão ao salvar (testar se consegue conectar no tipo correto).
  - Armazenar as credenciais de forma minimamente segura (ex.: criptografia simétrica, ou ao menos indicar onde implementar isso).
  - Oferecer endpoints REST:
    - `GET /servers`
    - `POST /servers`
    - `PUT /servers/{id}`
    - `DELETE /servers/{id}`
    - `POST /servers/{id}/test-connection`

---

### 3. Subir servidores com Docker (incluindo Redis e MongoDB)

Para servidores marcados como “Gerenciado via Docker”:

- Ao criar o servidor, o backend deve:
  - Gerar a configuração necessária para um container daquele tipo de banco:
    - MySQL: imagem oficial (ex.: `mysql:8`)
    - PostgreSQL: `postgres:16`
    - SQL Server: `mcr.microsoft.com/mssql/server`
    - Redis: `redis:latest`
    - MongoDB: `mongo:latest`
  - Definir variáveis de ambiente (usuário/senha/DB inicial quando aplicável).

- Implementar no backend um serviço que:
  - Cria dinamicamente a entrada no `docker-compose` ou gera e executa um comando `docker run` (explique a abordagem no README).
  - Tenha endpoints para:
    - `POST /servers/{id}/start`
    - `POST /servers/{id}/stop`
    - `GET /servers/{id}/status`

Documentar qualquer limitação (por exemplo, necessidade de o backend rodar no mesmo host do Docker).

---

### 4. Gestão de Bancos de Dados (MySQL, PostgreSQL, SQL Server, MongoDB)

Para cada servidor **SQL** (MySQL, PostgreSQL, SQL Server):

- Tela de detalhes do servidor → lista de bancos de dados existentes.
- Funcionalidades:
  - Listar bancos.
  - Criar novo banco (nome + charset/collation quando aplicável).
  - Excluir banco (com confirmação).

Para **MongoDB**:

- Tela de bancos/DBs:
  - Listar databases.
  - Criar database (nome).
  - Excluir database (com confirmação), se aplicável.

Para **Redis**:
- Redis não possui “databases” do mesmo jeito, apenas índices (0, 1, 2, ...).  
- Nesse painel, o foco é **gerenciar o servidor e testar conexão**, não precisa gerenciar databases internos. Só exibir:
  - Host, porta, auth.
  - Status do servidor (on/off).
  - Opcional: um campo para executar comandos simples (`PING`, `INFO`) e mostrar o resultado.

Endpoints REST sugeridos:

- SQL e MongoDB:
  - `GET /servers/{id}/databases`
  - `POST /servers/{id}/databases`
  - `DELETE /servers/{id}/databases/{dbName}`

- Redis:
  - `GET /servers/{id}/info` (status básico, info, ping)

---

### 5. Gestão de Usuários e Permissões

Para cada **banco SQL** (MySQL, PostgreSQL, SQL Server) e para **MongoDB**:

- Tela de “Usuários”:
  - Listar usuários do banco.
  - Criar usuário:
    - Nome do usuário
    - Senha
    - Permissões básicas (por exemplo: somente leitura, leitura/escrita, tudo).
  - Atribuir/remover permissões em um banco específico.

- Backend deve traduzir isso para comandos apropriados:

  - MySQL:
    - `CREATE USER`, `GRANT`, `REVOKE`, etc.
  - PostgreSQL:
    - `CREATE ROLE`, `GRANT`, etc.
  - SQL Server:
    - `CREATE LOGIN`, `CREATE USER`, `ALTER ROLE`, etc.
  - MongoDB:
    - `db.createUser`, `db.updateUser`, roles como `read`, `readWrite`, etc.

Para **Redis**:

- Focar apenas em:
  - Senha padrão de conexão (AUTH).
  - Opcionalmente suportar ACL (Redis 6+), mas pode ser apenas documentado como ponto de extensão.

Endpoints REST:

- `GET /servers/{id}/databases/{dbName}/users`
- `POST /servers/{id}/databases/{dbName}/users`
- `PUT /servers/{id}/databases/{dbName}/users/{userName}`
- `DELETE /servers/{id}/databases/{dbName}/users/{userName}`

Explique no código e no README que o foco é **simplificar** (não é preciso cobrir 100% das opções de permissão, apenas perfis básicos).

---

### 6. Visualização e manipulação de dados

Para bancos **SQL** (MySQL, PostgreSQL, SQL Server):

- Tela “Tabelas”:
  - Listar as tabelas.
  - Ao clicar numa tabela, mostrar:
    - Estrutura básica (colunas, tipos, chave primária).
    - Primeiras N linhas (ex.: 100) para visualização.

- Funções mínimas:
  - Criar tabela simples (nome da tabela, colunas, tipo, PK).
  - Excluir tabela (com confirmação).
  - Editar estrutura básica (adicionar/remover coluna).
  - Executar uma query SQL simples:
    - Campo de texto para query.
    - Botão “Executar”.
    - Mostrar resultado em tabela.
  - Se possível, exigir confirmação para comandos perigosos (DROP/DELETE/UPDATE sem WHERE, etc.).

Para **MongoDB**:

- Tela “Coleções” para cada database:
  - Listar coleções.
  - Criar coleção.
  - Excluir coleção.
  - Mostrar primeiros N documentos como JSON.
  - Campo de texto para uma query simples em JSON (por exemplo, filtro para `find`).

Para **Redis** (opcional, mas desejável):

- Tela simples:
  - Input para rodar comandos básicos.
  - Exibir resultado em texto.
  - Focar em comandos de leitura (`GET`, `KEYS`, `TTL`, etc.).  
  - Indicar no README que é apenas uma interface mínima, não um painel completo de chaves.

Endpoints REST sugeridos (SQL e MongoDB):

- `GET /servers/{id}/databases/{dbName}/tables` ou `/collections`
- `GET /servers/{id}/databases/{dbName}/tables/{tableName}/schema`
- `GET /servers/{id}/databases/{dbName}/tables/{tableName}/rows`
- `POST /servers/{id}/databases/{dbName}/tables` (ou `/collections`)
- `DELETE /servers/{id}/databases/{dbName}/tables/{tableName}`
- `POST /servers/{id}/databases/{dbName}/query`

---

## UX / UI

- Layout bem simples:
  - Sidebar com:
    - Servidores
    - Configurações
    - Usuário logado / logout
  - Área principal:
    - Lista de servidores com tipo (MySQL, Postgres, SQL Server, Redis, MongoDB) e status.
    - Ao clicar em um servidor → abas, por exemplo:
      - Visão geral
      - Bancos de Dados (para SQL/Mongo)
      - Usuários
      - Status Docker (se gerenciado)
      - Comandos (para Redis)

- Ao clicar em um banco:
  - Abas:
    - Tabelas / Coleções
    - Usuários
    - Query/Comandos

Use componentes básicos, priorizando clareza e facilidade de uso.

---

## Segurança e boas práticas

- Nunca logar senha de banco de dados em logs.
- Sempre usar variáveis de ambiente para senhas e secrets.
- Explicar no README:
  - Riscos de expor esse painel na internet.
  - Recomendação de uso somente em ambiente interno / VPN ou com autenticação robusta na frente (Nginx/Proxy).

---

## Entregáveis

1. Código completo do backend + frontend.
2. Arquivo `docker-compose.yml` que:
   - Sobe backend, frontend e banco interno.
   - Inclui ou documenta os serviços de bancos gerenciados (MySQL, PostgreSQL, SQL Server, Redis, MongoDB).
3. Scripts de inicialização/migrations necessários.
4. README detalhado explicando:
   - Como rodar com Docker.
   - Como fazer login pela primeira vez.
   - Como cadastrar o primeiro servidor (SQL, Redis, MongoDB).
   - Exemplos de uso para cada tipo de servidor.

Siga tudo isso passo a passo e escreva código limpo, comentado quando necessário, para que eu consiga entender, adaptar e evoluir esse painel no futuro.
