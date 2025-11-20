#!/bin/bash

# BeaverDB - Script de Instalação Automática para Ubuntu
# Este script instala (se necessário) Docker, Docker Compose e configura o BeaverDB
# com backend, frontend, nginx + ssl, tudo em Docker.
# IMPORTANTE: execute este script DENTRO da pasta do projeto já baixado.

set -e

echo "========================================="
echo "  BeaverDB - Instalação Automática"
echo "========================================="
echo ""

# Verificar se é root
if [ "$EUID" -ne 0 ]; then 
    echo "❌ Por favor, execute como root ou com sudo"
    exit 1
fi

echo "✓ Rodando como root"

# Diretório do projeto (pasta atual)
PROJECT_DIR="$(pwd)"
echo "📁 Instalando no diretório: $PROJECT_DIR"

# Atualizar sistema
echo ""
echo "📦 Atualizando sistema..."
apt update -qq
apt upgrade -y -qq

# Instalar dependências
echo ""
echo "📦 Instalando dependências..."
apt install -y -qq \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    git \
    openssl

# Verificar / instalar Docker
echo ""
if command -v docker &>/dev/null; then
    echo "🐳 Docker já instalado: $(docker --version)"
else
    echo "🐳 Instalando Docker..."

    # Remover versões antigas
    apt remove -y docker docker-engine docker.io containerd runc 2>/dev/null || true

    # Adicionar chave GPG
    mkdir -p /etc/apt/keyrings
    if [ ! -f /etc/apt/keyrings/docker.gpg ]; then
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    fi

    # Adicionar repositório
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
      $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null

    # Instalar Docker Engine + plugin compose
    apt update -qq
    apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

    echo "✓ Docker instalado: $(docker --version)"
fi

# Verificar Docker Compose (plugin)
if docker compose version &>/dev/null; then
    echo "✓ Docker Compose disponível: $(docker compose version)"
else
    echo "⚠️ Docker Compose (plugin) não encontrado. Tentando instalar plugin..."
    apt install -y docker-compose-plugin
    if ! docker compose version &>/dev/null; then
        echo "❌ Não foi possível habilitar 'docker compose'."
        echo "   Verifique a instalação do Docker/Compose antes de continuar."
        exit 1
    fi
    echo "✓ Docker Compose disponível: $(docker compose version)"
fi

# Configurar permissões do socket
echo ""
echo "🔧 Configurando permissões do Docker..."
chmod 666 /var/run/docker.sock || true
systemctl enable docker >/dev/null 2>&1 || true
systemctl start docker >/dev/null 2>&1 || true

# Criar estrutura mínima de pastas (sem mexer fora do diretório atual)
mkdir -p backend frontend nginx/conf.d certbot/conf certbot/www

# Criar Dockerfile placeholder do backend (somente se não existir)
if [ ! -f backend/Dockerfile ]; then
cat > backend/Dockerfile <<'EOF'
# Exemplo para backend ASP.NET 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Ajuste este bloco para o seu backend real:
# COPY ./src/Backend/Backend.csproj ./
# RUN dotnet restore Backend.csproj
# COPY ./src/Backend/. .
# RUN dotnet publish Backend.csproj -c Release -o /app/publish

# Placeholder: cria um webapi simples
RUN dotnet new webapi -n BeaverDbBackend -o .
RUN dotnet publish BeaverDbBackend.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BeaverDbBackend.dll"]
EOF
    echo "✓ Dockerfile de backend criado em backend/Dockerfile (placeholder)"
else
    echo "✓ Dockerfile de backend já existe, mantendo arquivo atual."
fi

# Criar Dockerfile placeholder do frontend (somente se não existir)
if [ ! -f frontend/Dockerfile ]; then
cat > frontend/Dockerfile <<'EOF'
# Exemplo para frontend React
FROM node:20-alpine AS build
WORKDIR /app
# Ajuste este bloco para o seu frontend real:
# COPY package*.json ./
# RUN npm install
# COPY . .
# RUN npm run build

# Placeholder: cria um app React básico
RUN npx create-react-app . --template cra-template-pwa-typescript
RUN npm run build

FROM nginx:alpine
WORKDIR /usr/share/nginx/html
COPY --from=build /app/build .
EOF
    echo "✓ Dockerfile de frontend criado em frontend/Dockerfile (placeholder)"
else
    echo "✓ Dockerfile de frontend já existe, mantendo arquivo atual."
fi

# Criar docker-compose.yml se não existir
if [ ! -f docker-compose.yml ]; then
cat > docker-compose.yml <<'EOF'
version: "3.9"

services:
  internal-db:
    image: postgres:16
    container_name: beaverdb-internal-db
    restart: unless-stopped
    env_file:
      - .env.production
    volumes:
      - db_data:/var/lib/postgresql/data
    networks:
      - beaverdb-net

  backend:
    build: ./backend
    container_name: beaverdb-backend
    restart: unless-stopped
    env_file:
      - .env.production
    depends_on:
      - internal-db
    networks:
      - beaverdb-net

  frontend:
    build: ./frontend
    container_name: beaverdb-frontend
    restart: unless-stopped
    env_file:
      - .env.production
    depends_on:
      - backend
    networks:
      - beaverdb-net

  nginx:
    image: nginx:alpine
    container_name: beaverdb-nginx
    restart: unless-stopped
    volumes:
      - ./nginx/conf.d:/etc/nginx/conf.d
      - ./certbot/www:/var/www/certbot
      - ./certbot/conf:/etc/letsencrypt
    depends_on:
      - frontend
      - backend
    ports:
      - "80:80"
      - "443:443"
    networks:
      - beaverdb-net

  certbot:
    image: certbot/certbot
    container_name: beaverdb-certbot
    volumes:
      - ./certbot/www:/var/www/certbot
      - ./certbot/conf:/etc/letsencrypt
    networks:
      - beaverdb-net

volumes:
  db_data:

networks:
  beaverdb-net:
EOF
    echo "✓ docker-compose.yml criado."
else
    echo "✓ docker-compose.yml já existe, mantendo arquivo atual."
fi

# Criar arquivo .env.production
echo ""
echo "📝 Configurando variáveis de ambiente..."

if [ ! -f ".env.production" ]; then
    DB_PASSWORD=$(openssl rand -base64 32)
    JWT_KEY=$(openssl rand -base64 64)
    ENC_KEY=$(openssl rand -base64 32 | cut -c1-32)
    ENC_IV=$(openssl rand -base64 16 | cut -c1-16)

cat > .env.production <<EOF
# Database
POSTGRES_DB=beaverdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=$DB_PASSWORD

# Backend
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=internal-db;Port=5432;Database=beaverdb;Username=postgres;Password=$DB_PASSWORD

# JWT
Jwt__Key=$JWT_KEY
Jwt__Issuer=BeaverDB
Jwt__Audience=BeaverDB

# Encryption
Encryption__Key=$ENC_KEY
Encryption__IV=$ENC_IV
EOF
    
    echo "✓ Arquivo .env.production criado com senhas aleatórias"
    echo ""
    echo "⚠️  IMPORTANTE: Salve estas credenciais em local seguro!"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    cat .env.production
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    read -p "Pressione ENTER para continuar..." _
else
    echo "✓ .env.production já existe, mantendo valores atuais."
fi

# Construir e iniciar containers (db, backend, frontend)
echo ""
echo "🏗️  Construindo e iniciando containers (db, backend, frontend)..."
docker compose up -d --build internal-db backend frontend

echo ""
echo "⏳ Aguardando containers iniciarem..."
sleep 15

# Verificar status
echo ""
echo "📊 Status dos containers BeaverDB:"
docker ps --filter "name=beaverdb" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Configurar Nginx e SSL
echo ""
read -p "Deseja configurar Nginx e SSL (HTTPS)? (s/N): " INSTALL_NGINX

if [ "$INSTALL_NGINX" = "s" ] || [ "$INSTALL_NGINX" = "S" ]; then
    echo "🌐 Configurando Nginx e SSL..."
    
    read -p "Digite seu domínio (ex: db.seudominio.com): " DOMAIN
    read -p "Digite seu email para o Let's Encrypt: " EMAIL
    
    if [ -z "$DOMAIN" ] || [ -z "$EMAIL" ]; then
        echo "❌ Domínio e email são obrigatórios para configuração SSL."
        exit 1
    fi

    # 1. Configuração inicial HTTP para validação do Certbot
    echo "📝 Gerando configuração HTTP inicial do Nginx..."
cat > nginx/conf.d/app.conf <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN www.$DOMAIN;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://\$host\$request_uri;
    }
}
EOF

    # 2. Iniciar Nginx (somente HTTP)
    echo "🚀 Iniciando Nginx (HTTP)..."
    docker compose up -d nginx

    # 3. Obter certificado SSL
    echo "🔒 Obtendo certificado SSL com Let's Encrypt..."
    docker compose run --rm certbot certonly \
        --webroot --webroot-path /var/www/certbot \
        -d "$DOMAIN" -d "www.$DOMAIN" \
        --email "$EMAIL" --agree-tos --no-eff-email

    # 4. Configuração HTTPS + proxy
    echo "📝 Atualizando configuração do Nginx para HTTPS..."
cat > nginx/conf.d/app.conf <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN www.$DOMAIN;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://\$host\$request_uri;
    }
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name $DOMAIN www.$DOMAIN;

    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # Frontend (SPA)
    location / {
        proxy_pass http://frontend:80;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }

    # Backend API
    location /api/ {
        proxy_pass http://backend:8080/;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF

    # 5. Parâmetros SSL recomendados
    mkdir -p certbot/conf
    if [ ! -f "certbot/conf/options-ssl-nginx.conf" ]; then
        echo "📥 Baixando parâmetros SSL recomendados..."
        curl -sSLo certbot/conf/options-ssl-nginx.conf \
            https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf
    fi
    if [ ! -f "certbot/conf/ssl-dhparams.pem" ]; then
        echo "🔑 Gerando dhparams (pode demorar um pouco)..."
        openssl dhparam -out certbot/conf/ssl-dhparams.pem 2048
    fi

    # 6. Recarregar Nginx
    echo "🔄 Recarregando Nginx com HTTPS..."
    docker compose restart nginx
    
    echo "✓ Nginx e SSL configurados com sucesso!"
fi

# Configurar Firewall
echo ""
read -p "Deseja configurar firewall (UFW)? (s/N): " INSTALL_FW

if [ "$INSTALL_FW" = "s" ] || [ "$INSTALL_FW" = "S" ]; then
    echo "🔒 Configurando firewall..."
    apt install -y ufw
    ufw default deny incoming
    ufw default allow outgoing
    ufw allow ssh
    ufw allow 80/tcp
    ufw allow 443/tcp
    ufw --force enable
    echo "✓ Firewall configurado"
fi

# Criar script de backup na pasta atual
echo ""
echo "💾 Criando script de backup..."
mkdir -p "$PROJECT_DIR/backups"

cat > "$PROJECT_DIR/backup.sh" <<EOF
#!/bin/bash
BACKUP_DIR="$PROJECT_DIR/backups"
DATE=\$(date +%Y%m%d_%H%M%S)
mkdir -p "\$BACKUP_DIR"

docker exec beaverdb-internal-db pg_dump -U postgres beaverdb | gzip > "\$BACKUP_DIR/beaverdb_\$DATE.sql.gz"
find "\$BACKUP_DIR" -name "*.sql.gz" -mtime +7 -delete

echo "Backup concluído: beaverdb_\$DATE.sql.gz"
EOF

chmod +x "$PROJECT_DIR/backup.sh"
echo "✓ Script de backup criado em $PROJECT_DIR/backup.sh"

# Agendar backup diário
read -p "Deseja agendar backup diário? (s/N): " SCHEDULE_BACKUP

if [ "$SCHEDULE_BACKUP" = "s" ] || [ "$SCHEDULE_BACKUP" = "S" ]; then
    (crontab -l 2>/dev/null; echo "0 2 * * * $PROJECT_DIR/backup.sh >> /var/log/beaverdb-backup.log 2>&1") | crontab -
    echo "✓ Backup agendado para 2h da manhã"
fi

# Finalização
IP_LOCAL=$(hostname -I | awk '{print $1}')

echo ""
echo "========================================="
echo "  ✅ Instalação Concluída!"
echo "========================================="
echo ""
echo "🎯 Próximos passos:"
echo ""
echo "1. Dentro do servidor, use:"
echo "   cd $PROJECT_DIR"
echo "   docker compose ps"
echo ""
echo "2. Acesso:"
echo "   - Via IP (sem domínio, se Nginx estiver rodando):"
echo "       http://$IP_LOCAL"
echo ""
echo "   - Se configurou domínio + SSL:"
echo "       https://$DOMAIN"
echo ""
echo "3. Ajuste os Dockerfiles de backend/frontend para apontar pro seu código real."
echo "4. Use:"
echo "   ./backup.sh   # para backup manual"
echo ""
echo "⚠️  Lembre-se de:"
echo "   - Anotar as senhas geradas em .env.production"
echo "   - Configurar DNS do domínio apontando para este servidor"
echo "   - Fazer backups regulares"
echo ""
