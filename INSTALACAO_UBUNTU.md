# Guia de Instalação - BeaverDB no Ubuntu Server

Este guia mostra como instalar e configurar o BeaverDB em um servidor Ubuntu com suporte completo à criação de containers Docker gerenciados.

## Pré-requisitos

- Ubuntu Server 20.04 LTS ou superior
- Acesso root ou sudo
- Conexão com internet

## 1. Instalação Inicial

### 1.1. Atualizar o Sistema

```bash
sudo apt update
sudo apt upgrade -y
```

### 1.2. Instalar Docker

```bash
# Remover versões antigas (se existirem)
sudo apt remove docker docker-engine docker.io containerd runc

# Instalar dependências
sudo apt install -y \
    ca-certificates \
    curl \
    gnupg \
    lsb-release

# Adicionar chave GPG oficial do Docker
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg

# Adicionar repositório
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Instalar Docker Engine
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Verificar instalação
sudo docker --version
sudo docker compose version
```

### 1.3. Configurar Usuário Docker (Opcional)

```bash
# Adicionar seu usuário ao grupo docker
sudo usermod -aG docker $USER

# Fazer logout e login novamente para aplicar
# Ou execute: newgrp docker

# Testar sem sudo
docker ps
```

### 1.4. Instalar Git

```bash
sudo apt install -y git
```

## 2. Instalação do BeaverDB

### 2.1. Clonar o Repositório

```bash
cd /opt
sudo git clone <URL_DO_SEU_REPOSITORIO> beaverdb
cd beaverdb
sudo chown -R $USER:$USER /opt/beaverdb
```

### 2.2. Configurar Permissões do Docker Socket

```bash
# Garantir que o socket do Docker tem as permissões corretas
sudo chmod 666 /var/run/docker.sock

# OU adicionar o grupo docker ao socket (mais seguro)
sudo chgrp docker /var/run/docker.sock
```

### 2.3. Configurar Variáveis de Ambiente

```bash
# Criar arquivo de ambiente para produção
nano .env.production
```

Adicione o seguinte conteúdo:

```env
# Database
POSTGRES_DB=beaverdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=SuaSenhaSegura123!

# Backend
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=internal-db;Port=5432;Database=beaverdb;Username=postgres;Password=SuaSenhaSegura123!

# JWT (MUDE ESTAS CHAVES!)
Jwt__Key=SuaChaveJWTMuitoSeguraDeNoMinimo64CaracteresParaProducao123456!
Jwt__Issuer=BeaverDB
Jwt__Audience=BeaverDB

# Encryption (MUDE ESTAS CHAVES!)
Encryption__Key=SuaChaveDe32CharacteresAqui!!
Encryption__IV=Seus16CharsIV!!
```

### 2.4. Atualizar docker-compose.yml (Produção)

Se necessário, crie um arquivo `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  internal-db:
    image: postgres:16
    container_name: beaverdb-internal-db
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    ports:
      - "127.0.0.1:5432:5432"  # Apenas localhost
    volumes:
      - beaverdb-internal-data:/var/lib/postgresql/data
    networks:
      - beaverdb-network
    restart: unless-stopped

  backend:
    build:
      context: ./backend/BeaverDB.API
      dockerfile: Dockerfile
    container_name: beaverdb-backend
    user: root  # Necessário para acesso ao socket Docker
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
      - ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection}
      - ASPNETCORE_URLS=http://+:8080
      - Jwt__Key=${Jwt__Key}
      - Jwt__Issuer=${Jwt__Issuer}
      - Jwt__Audience=${Jwt__Audience}
      - Encryption__Key=${Encryption__Key}
      - Encryption__IV=${Encryption__IV}
    ports:
      - "127.0.0.1:5000:8080"  # Apenas localhost
    depends_on:
      - internal-db
    networks:
      - beaverdb-network
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    restart: unless-stopped

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: beaverdb-frontend
    ports:
      - "80:80"      # HTTP
      - "443:443"    # HTTPS (se configurado)
    depends_on:
      - backend
    networks:
      - beaverdb-network
    restart: unless-stopped

volumes:
  beaverdb-internal-data:

networks:
  beaverdb-network:
    driver: bridge
```

## 3. Deploy

### 3.1. Construir e Iniciar

```bash
# Usando arquivo de produção
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build

# OU usando arquivo padrão
docker compose up -d --build
```

### 3.2. Verificar Status

```bash
# Ver containers rodando
docker ps

# Ver logs do backend
docker logs beaverdb-backend -f

# Ver logs do frontend
docker logs beaverdb-frontend -f

# Ver logs do banco
docker logs beaverdb-internal-db -f
```

### 3.3. Testar Funcionalidade

```bash
# Testar API
curl http://localhost:5000/api/auth/check-init

# Deve retornar: {"initialized":false}
```

## 4. Configuração de Segurança

### 4.1. Firewall (UFW)

```bash
# Instalar UFW
sudo apt install -y ufw

# Configurar regras
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow ssh
sudo ufw allow 80/tcp   # HTTP
sudo ufw allow 443/tcp  # HTTPS

# Ativar firewall
sudo ufw enable
sudo ufw status
```

### 4.2. Configurar Nginx como Reverse Proxy (Recomendado)

```bash
# Instalar Nginx
sudo apt install -y nginx

# Criar configuração
sudo nano /etc/nginx/sites-available/beaverdb
```

Adicione:

```nginx
server {
    listen 80;
    server_name seu-dominio.com;

    # Redirecionar para HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name seu-dominio.com;

    # Certificado SSL (gerar com Let's Encrypt)
    ssl_certificate /etc/letsencrypt/live/seu-dominio.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/seu-dominio.com/privkey.pem;

    # Configurações SSL
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Proxy para frontend
    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Restringir acesso (opcional)
    # allow 10.0.0.0/8;
    # deny all;
}
```

```bash
# Ativar site
sudo ln -s /etc/nginx/sites-available/beaverdb /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### 4.3. Certificado SSL com Let's Encrypt

```bash
# Instalar Certbot
sudo apt install -y certbot python3-certbot-nginx

# Obter certificado
sudo certbot --nginx -d seu-dominio.com

# Auto-renovação
sudo certbot renew --dry-run
```

## 5. Manutenção

### 5.1. Backup do Banco Interno

```bash
# Criar script de backup
nano /opt/beaverdb/backup.sh
```

Adicione:

```bash
#!/bin/bash
BACKUP_DIR="/opt/beaverdb/backups"
DATE=$(date +%Y%m%d_%H%M%S)
mkdir -p $BACKUP_DIR

docker exec beaverdb-internal-db pg_dump -U postgres beaverdb | gzip > $BACKUP_DIR/beaverdb_$DATE.sql.gz

# Manter apenas últimos 7 dias
find $BACKUP_DIR -name "*.sql.gz" -mtime +7 -delete

echo "Backup concluído: beaverdb_$DATE.sql.gz"
```

```bash
# Tornar executável
chmod +x /opt/beaverdb/backup.sh

# Adicionar ao cron (diário às 2h)
crontab -e
```

Adicione:
```
0 2 * * * /opt/beaverdb/backup.sh >> /var/log/beaverdb-backup.log 2>&1
```

### 5.2. Atualizar BeaverDB

```bash
cd /opt/beaverdb
git pull
docker compose down
docker compose up -d --build
```

### 5.3. Logs

```bash
# Ver todos os logs
docker compose logs -f

# Logs de um serviço específico
docker compose logs -f backend

# Logs com timestamp
docker compose logs -f --timestamps

# Últimas 100 linhas
docker compose logs --tail=100
```

### 5.4. Reiniciar Serviços

```bash
# Reiniciar tudo
docker compose restart

# Reiniciar apenas backend
docker compose restart backend

# Parar tudo
docker compose down

# Iniciar novamente
docker compose up -d
```

## 6. Monitoramento

### 6.1. Verificar Recursos

```bash
# Ver uso de recursos
docker stats

# Ver containers
docker ps -a

# Ver volumes
docker volume ls

# Ver redes
docker network ls
```

### 6.2. Healthcheck (Adicionar ao docker-compose)

```yaml
backend:
  # ... outras configurações
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:8080/api/auth/check-init"]
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 40s
```

## 7. Troubleshooting

### Erro: Cannot connect to Docker daemon

```bash
# Verificar se Docker está rodando
sudo systemctl status docker

# Iniciar Docker
sudo systemctl start docker

# Verificar permissões do socket
ls -la /var/run/docker.sock
sudo chmod 666 /var/run/docker.sock
```

### Erro: Permission denied ao criar container

```bash
# Backend precisa rodar como root
# Verificar no docker-compose.yml:
user: root
```

### Containers não iniciam

```bash
# Ver logs detalhados
docker compose logs

# Remover e recriar
docker compose down -v
docker compose up -d --build
```

### Porta já em uso

```bash
# Ver o que está usando a porta
sudo lsof -i :5000
sudo lsof -i :3000

# Matar processo
sudo kill -9 <PID>
```

## 8. Testando Criação de Containers

Após tudo configurado:

1. Acesse: `http://seu-dominio.com` ou `http://seu-ip`
2. Faça login ou crie admin
3. Clique em "+ Add Server"
4. Preencha:
   - **Name**: MySQL Test
   - **Type**: MySQL
   - **Port**: 3307 (não use 3306 se já tiver outro MySQL)
   - **Password**: senha123
   - **✅ Managed by Docker**: Marcado
5. Clique em "Create"

Verifique:
```bash
docker ps
# Deve aparecer o container: beaverdb-mysql-1
```

## Resumo dos Comandos Principais

```bash
# Build e start
docker compose up -d --build

# Parar
docker compose down

# Ver logs
docker compose logs -f

# Reiniciar
docker compose restart

# Backup
docker exec beaverdb-internal-db pg_dump -U postgres beaverdb > backup.sql

# Atualizar
git pull && docker compose up -d --build
```

## Segurança em Produção

✅ **Configurado neste guia:**
- Firewall UFW
- Nginx como reverse proxy
- SSL/HTTPS com Let's Encrypt
- Portas expostas apenas no localhost
- Backups automáticos

⚠️ **Ainda precisa:**
- Senhas fortes (trocar as padrão)
- Restrição de IP (se possível)
- Monitoramento (Prometheus/Grafana)
- Logs centralizados
- VPN para acesso remoto
