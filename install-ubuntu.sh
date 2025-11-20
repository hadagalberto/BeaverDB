#!/bin/bash

# BeaverDB - Script de Instalação Automática para Ubuntu
# Este script instala Docker, Docker Compose e configura o BeaverDB

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
    git

# Instalar Docker
echo ""
echo "🐳 Instalando Docker..."

# Remover versões antigas
apt remove -y docker docker-engine docker.io containerd runc 2>/dev/null || true

# Adicionar chave GPG
mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg

# Adicionar repositório
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null

# Instalar Docker Engine
apt update -qq
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

echo "✓ Docker instalado: $(docker --version)"
echo "✓ Docker Compose instalado: $(docker compose version)"

# Configurar permissões do socket
echo ""
echo "🔧 Configurando permissões do Docker..."
chmod 666 /var/run/docker.sock
systemctl enable docker
systemctl start docker

# Clonar repositório
echo ""
echo "📥 Clonando repositório BeaverDB..."
cd /opt

# Se já existe, fazer backup
if [ -d "beaverdb" ]; then
    echo "⚠️  Diretório /opt/beaverdb já existe. Fazendo backup..."
    mv beaverdb beaverdb.backup.$(date +%Y%m%d_%H%M%S)
fi

# Aqui você deve colocar a URL do seu repositório
# git clone https://github.com/seu-usuario/beaverdb.git
# Por enquanto, assumindo que você já copiou os arquivos

if [ ! -d "beaverdb" ]; then
    echo "❌ Diretório beaverdb não encontrado em /opt/"
    echo "Por favor, copie os arquivos do BeaverDB para /opt/beaverdb"
    exit 1
fi

cd beaverdb

# Criar arquivo .env
echo ""
echo "📝 Configurando variáveis de ambiente..."

if [ ! -f ".env.production" ]; then
    cat > .env.production <<EOF
# Database
POSTGRES_DB=beaverdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=$(openssl rand -base64 32)

# Backend
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=internal-db;Port=5432;Database=beaverdb;Username=postgres;Password=$(openssl rand -base64 32)

# JWT
Jwt__Key=$(openssl rand -base64 64)
Jwt__Issuer=BeaverDB
Jwt__Audience=BeaverDB

# Encryption
Encryption__Key=$(openssl rand -base64 32 | cut -c1-32)
Encryption__IV=$(openssl rand -base64 16 | cut -c1-16)
EOF
    
    echo "✓ Arquivo .env.production criado com senhas aleatórias"
    echo ""
    echo "⚠️  IMPORTANTE: Salve estas credenciais em local seguro!"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    cat .env.production
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    read -p "Pressione ENTER para continuar..."
fi

# Construir e iniciar containers
echo ""
echo "🏗️  Construindo e iniciando containers..."
docker compose up -d --build

echo ""
echo "⏳ Aguardando containers iniciarem..."
sleep 10

# Verificar status
echo ""
echo "📊 Status dos containers:"
docker ps --filter "name=beaverdb" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Configurar Nginx (opcional)
echo ""
read -p "Deseja instalar e configurar Nginx como reverse proxy? (s/N): " INSTALL_NGINX

if [ "$INSTALL_NGINX" = "s" ] || [ "$INSTALL_NGINX" = "S" ]; then
    echo "📦 Instalando Nginx..."
    apt install -y nginx
    
    read -p "Digite seu domínio (ex: beaverdb.example.com): " DOMAIN
    
    cat > /etc/nginx/sites-available/beaverdb <<EOF
server {
    listen 80;
    server_name $DOMAIN;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF
    
    ln -sf /etc/nginx/sites-available/beaverdb /etc/nginx/sites-enabled/
    nginx -t && systemctl restart nginx
    
    echo "✓ Nginx configurado para $DOMAIN"
    
    # Certificado SSL
    read -p "Deseja instalar certificado SSL com Let's Encrypt? (s/N): " INSTALL_SSL
    
    if [ "$INSTALL_SSL" = "s" ] || [ "$INSTALL_SSL" = "S" ]; then
        apt install -y certbot python3-certbot-nginx
        certbot --nginx -d $DOMAIN --non-interactive --agree-tos --email admin@$DOMAIN
        echo "✓ Certificado SSL instalado"
    fi
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

# Criar script de backup
echo ""
echo "💾 Criando script de backup..."
mkdir -p /opt/beaverdb/backups

cat > /opt/beaverdb/backup.sh <<'EOF'
#!/bin/bash
BACKUP_DIR="/opt/beaverdb/backups"
DATE=$(date +%Y%m%d_%H%M%S)
mkdir -p $BACKUP_DIR

docker exec beaverdb-internal-db pg_dump -U postgres beaverdb | gzip > $BACKUP_DIR/beaverdb_$DATE.sql.gz
find $BACKUP_DIR -name "*.sql.gz" -mtime +7 -delete

echo "Backup concluído: beaverdb_$DATE.sql.gz"
EOF

chmod +x /opt/beaverdb/backup.sh
echo "✓ Script de backup criado em /opt/beaverdb/backup.sh"

# Adicionar ao cron
read -p "Deseja agendar backup diário? (s/N): " SCHEDULE_BACKUP

if [ "$SCHEDULE_BACKUP" = "s" ] || [ "$SCHEDULE_BACKUP" = "S" ]; then
    (crontab -l 2>/dev/null; echo "0 2 * * * /opt/beaverdb/backup.sh >> /var/log/beaverdb-backup.log 2>&1") | crontab -
    echo "✓ Backup agendado para 2h da manhã"
fi

# Finalização
echo ""
echo "========================================="
echo "  ✅ Instalação Concluída!"
echo "========================================="
echo ""
echo "🎯 Próximos passos:"
echo ""
echo "1. Acesse: http://$(hostname -I | awk '{print $1}'):3000"
if [ ! -z "$DOMAIN" ]; then
    echo "   ou http://$DOMAIN"
fi
echo ""
echo "2. Na primeira vez, você verá a tela de inicialização"
echo "   Crie sua conta de administrador"
echo ""
echo "3. Após login, clique em '+ Add Server' para adicionar"
echo "   seus servidores de banco de dados"
echo ""
echo "4. Marque 'Managed by Docker' para que o BeaverDB"
echo "   crie containers automaticamente"
echo ""
echo "📚 Documentação completa em:"
echo "   /opt/beaverdb/INSTALACAO_UBUNTU.md"
echo ""
echo "🔧 Comandos úteis:"
echo "   docker ps                    # Ver containers"
echo "   docker compose logs -f       # Ver logs"
echo "   docker compose restart       # Reiniciar"
echo "   /opt/beaverdb/backup.sh      # Fazer backup"
echo ""
echo "⚠️  Lembre-se de:"
echo "   - Anotar as senhas geradas em .env.production"
echo "   - Configurar DNS apontando para este servidor"
echo "   - Fazer backups regulares"
echo ""
