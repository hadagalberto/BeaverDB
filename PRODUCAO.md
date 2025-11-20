# BeaverDB em Produção - Guia de Deploy

## Funcionalidade de Docker Gerenciado em Produção

### ❌ Não Funciona (Configuração Padrão)

Com a configuração atual do `docker-compose.yml`, a funcionalidade de **criar containers Docker gerenciados** **NÃO funcionará** em produção porque:

1. O backend está em um container
2. Containers não podem criar outros containers facilmente (Docker-in-Docker)
3. Requer configuração especial de segurança

### ✅ Soluções para Produção

## Opção 1: Usar Apenas Servidores Externos (Recomendado)

**Melhor para**: Ambientes corporativos, produção estável

Em produção, é **mais seguro e comum** conectar a bancos de dados **já existentes**:

```yaml
# Seus bancos de produção já existem
- RDS (AWS)
- Cloud SQL (Google Cloud)
- Azure Database
- Servidores on-premise
```

**Como usar:**
1. Ao criar servidor no BeaverDB, **NÃO marque** "Managed by Docker"
2. Informe host, porta e credenciais do banco existente
3. BeaverDB apenas se conecta (não gerencia o ciclo de vida)

**Vantagens:**
- ✅ Mais seguro
- ✅ Bancos gerenciados por equipes especializadas
- ✅ Backups e alta disponibilidade já configurados
- ✅ Não precisa de permissões Docker especiais

## Opção 2: Backend Fora do Docker

**Melhor para**: Servidores dedicados, VMs

Rodar o backend **diretamente no servidor** (não em container):

```bash
# No servidor de produção
cd /opt/beaverdb/backend
dotnet BeaverDB.API.dll
```

**Configuração:**
```yaml
# docker-compose.yml (apenas frontend e banco interno)
services:
  internal-db:
    image: postgres:16
    # ... configuração do banco

  frontend:
    image: beaverdb-frontend
    # ... configuração do frontend

# Backend roda como serviço systemd
```

**Vantagens:**
- ✅ Funcionalidade de Docker gerenciado funciona
- ✅ Backend tem acesso direto ao Docker do host
- ✅ Melhor performance (sem overhead de container)

**Desvantagens:**
- ❌ Precisa gerenciar dependências .NET no servidor
- ❌ Menos portável

## Opção 3: Docker-in-Docker (DinD) - Avançado

**Melhor para**: Ambientes de desenvolvimento/staging, DevOps experientes

Configurar Docker-in-Docker com privilégios especiais:

```yaml
# docker-compose.yml
services:
  backend:
    build: ./backend
    privileged: true  # ⚠️ RISCO DE SEGURANÇA
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - DOCKER_HOST=unix:///var/run/docker.sock
```

**⚠️ ATENÇÃO - Riscos de Segurança:**
- Container tem acesso ROOT ao Docker host
- Pode criar/destruir QUALQUER container
- Pode acessar volumes de outros containers
- **NÃO recomendado** para produção exposta

**Quando usar:**
- Ambientes internos/VPN
- Equipe DevOps experiente
- Monitoramento e auditoria robustos

## Opção 4: Kubernetes com Operators

**Melhor para**: Infraestrutura Kubernetes, grande escala

Usar Kubernetes Operators para gerenciar bancos de dados:

```yaml
# Usar operators como:
- Zalando Postgres Operator
- MySQL Operator
- MongoDB Enterprise Operator
```

BeaverDB se conectaria aos bancos criados pelos operators (modo externo).

## Arquitetura Recomendada para Produção

### Cenário 1: Pequena/Média Empresa

```
┌─────────────────────────────────────┐
│  BeaverDB (Docker Compose)          │
│  ├─ Frontend (Nginx)                │
│  ├─ Backend (.NET)                  │
│  └─ Internal DB (PostgreSQL)        │
└─────────────────────────────────────┘
           │
           │ Conecta a (modo externo)
           ▼
┌─────────────────────────────────────┐
│  Bancos de Dados de Produção        │
│  ├─ MySQL (RDS/Cloud SQL)           │
│  ├─ PostgreSQL (Managed)            │
│  ├─ MongoDB Atlas                   │
│  └─ Redis (ElastiCache)             │
└─────────────────────────────────────┘
```

### Cenário 2: Infraestrutura Própria

```
┌─────────────────────────────────────┐
│  Servidor BeaverDB                  │
│  ├─ Backend (.NET) - Systemd        │ ← Acessa Docker
│  └─ Frontend (Nginx)                │
└─────────────────────────────────────┘
           │
           │ Pode criar containers
           ▼
┌─────────────────────────────────────┐
│  Docker Host                        │
│  ├─ MySQL Container                 │
│  ├─ PostgreSQL Container            │
│  └─ MongoDB Container               │
└─────────────────────────────────────┘
```

## Configuração de Segurança

### Variáveis de Ambiente (Produção)

```bash
# .env.production
ConnectionStrings__DefaultConnection=Host=postgres-prod;Database=beaverdb;...
Jwt__Key=<CHAVE-FORTE-ALEATORIA-64-CARACTERES>
Encryption__Key=<CHAVE-CRIPTOGRAFIA-32-CHARS>
Encryption__IV=<IV-16-CHARS>
ASPNETCORE_ENVIRONMENT=Production
```

### Reverse Proxy (Nginx/Traefik)

```nginx
# nginx.conf
server {
    listen 443 ssl http2;
    server_name beaverdb.suaempresa.com;

    ssl_certificate /etc/ssl/certs/beaverdb.crt;
    ssl_certificate_key /etc/ssl/private/beaverdb.key;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # Restringir acesso por IP (opcional)
    allow 10.0.0.0/8;    # Rede interna
    allow 192.168.0.0/16; # VPN
    deny all;
}
```

### Firewall

```bash
# Permitir apenas IPs confiáveis
ufw allow from 10.0.0.0/8 to any port 3000
ufw allow from 192.168.0.0/16 to any port 3000
ufw deny 3000
```

## Checklist de Deploy em Produção

- [ ] Usar HTTPS (certificado SSL/TLS)
- [ ] Configurar firewall (restringir IPs)
- [ ] Trocar todas as senhas padrão
- [ ] Usar variáveis de ambiente (não hardcoded)
- [ ] Configurar backup do banco interno
- [ ] Implementar logs centralizados
- [ ] Configurar monitoramento (Prometheus/Grafana)
- [ ] Restringir acesso via VPN ou IP whitelist
- [ ] Revisar permissões de usuários
- [ ] Documentar credenciais em cofre seguro (Vault)
- [ ] Testar recuperação de desastres
- [ ] Configurar alertas de falha

## Resumo - Qual Opção Escolher?

| Cenário | Opção Recomendada | Docker Gerenciado? |
|---------|-------------------|-------------------|
| **Produção Corporativa** | Opção 1 (Externos) | ❌ Não |
| **Servidor Dedicado** | Opção 2 (Backend fora) | ✅ Sim |
| **Staging/Dev** | Opção 3 (DinD) | ✅ Sim (com cuidado) |
| **Kubernetes** | Opção 4 (Operators) | ❌ Não (usa operators) |
| **Desenvolvimento Local** | Backend local | ✅ Sim |

## Conclusão

**Para a maioria dos casos de produção**, recomendo:

1. ✅ **Usar BeaverDB para GERENCIAR bancos existentes** (modo externo)
2. ✅ **NÃO usar** a funcionalidade de criar containers em produção
3. ✅ **Deixar a criação de bancos** para ferramentas especializadas (Terraform, Ansible, Kubernetes Operators)
4. ✅ **Usar BeaverDB** como painel de visualização, queries e administração

A funcionalidade de Docker gerenciado é **excelente para desenvolvimento e testes**, mas em produção é mais seguro e profissional usar bancos gerenciados ou provisionados por outras ferramentas.
