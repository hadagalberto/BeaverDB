# Executando BeaverDB Localmente (Desenvolvimento)

## Problema com Docker-in-Docker

Quando o backend roda dentro de um container Docker (via `docker-compose`), ele **não consegue** criar containers Docker gerenciados porque:

1. O backend está rodando em Linux (dentro do container)
2. Mas o Docker host está no Windows
3. A comunicação Docker-in-Docker requer configuração especial

## Solução: Rodar Backend Localmente

Para usar a funcionalidade de **servidores gerenciados por Docker**, você precisa rodar o backend **localmente** (fora do Docker):

### 1. Parar o Backend no Docker

```bash
docker-compose stop backend
```

### 2. Manter Apenas o Banco Interno Rodando

```bash
docker-compose up -d internal-db
```

### 3. Rodar o Backend Localmente

```bash
cd backend/BeaverDB.API
dotnet run
```

O backend irá rodar em: **http://localhost:5000**

### 4. Rodar o Frontend Localmente (Opcional)

```bash
cd frontend
npm run dev
```

O frontend irá rodar em: **http://localhost:5173**

## Configuração

Quando rodar localmente, o backend usará:
- **Windows**: `npipe://./pipe/docker_engine` (Docker Desktop)
- **Linux/Mac**: `unix:///var/run/docker.sock`

## Alternativa: Usar Servidores Externos

Se preferir manter tudo no Docker, você pode:

1. **Não marcar** a opção "Managed by Docker" ao criar servidores
2. Conectar a servidores de banco de dados **já existentes** (externos)
3. Usar os containers de banco que você criar manualmente

### Exemplo: Criar MySQL Manualmente

```bash
docker run -d \
  --name my-mysql \
  -e MYSQL_ROOT_PASSWORD=senha123 \
  -p 3307:3306 \
  mysql:8
```

Depois no BeaverDB:
- **Name**: My MySQL
- **Type**: MySQL
- **Host**: localhost (ou host.docker.internal se backend em Docker)
- **Port**: 3307
- **Username**: root
- **Password**: senha123
- **Managed by Docker**: ❌ Desmarcado

## Resumo

| Cenário | Backend | Frontend | Docker Gerenciado? |
|---------|---------|----------|-------------------|
| **Desenvolvimento Completo** | Local | Local | ✅ Sim |
| **Produção (Docker)** | Container | Container | ❌ Não (usar externos) |
| **Híbrido** | Local | Container | ✅ Sim |

## Próximos Passos

Para suporte completo a Docker-in-Docker em produção, seria necessário:
1. Configurar Docker socket com permissões corretas
2. Usar Docker API via TCP (não recomendado por segurança)
3. Ou usar uma solução como Portainer Agent
