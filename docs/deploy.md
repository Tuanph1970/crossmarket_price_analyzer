# CrossMarket Analyzer — Docker Deployment Guide

> **Scope**: Full-stack deployment on **Windows 10/11** and **Ubuntu 22.04+** using Docker Compose
> **Services**: AuthService, NotificationService, ProductService, MatchingService, ScoringService, CMA.Gateway, CMA.WebApp
> **Version**: 2.0.0

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Clone & Configure](#2-clone--configure)
3. [Docker Compose — Ubuntu](#3-docker-compose--ubuntu)
4. [Docker Compose — Windows](#4-docker-compose--windows)
5. [Initial Setup](#5-initial-setup)
6. [Health Verification](#6-health-verification)
7. [Troubleshooting](#7-troubleshooting)
8. [Kubernetes (Optional)](#8-kubernetes-optional)

---

## 1. Prerequisites

### Ubuntu 22.04+

```bash
# Install Docker
sudo apt update
sudo apt install -y ca-certificates curl gnupg lsb-release

sudo install -m 0755 -o root -g root /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin docker-buildx-plugin

# Enable and start Docker
sudo systemctl enable docker
sudo systemctl start docker

# Add current user to docker group (avoids needing sudo)
sudo usermod -aG docker $USER
newgrp docker

# Verify
docker --version          # → Docker version 27.x.x or newer
docker compose version   # → Docker Compose version v2.29.x or newer
```

### Windows 10/11

1. **Enable WSL 2** (Windows Subsystem for Linux):
   ```powershell
   # Run PowerShell as Administrator
   wsl --install --no-distribution
   wsl --set-default-version 2
   ```

2. **Install Ubuntu from Microsoft Store**:
   - Open Microsoft Store → search "Ubuntu 22.04 LTS" → Install
   - Launch Ubuntu and set your username/password

3. **Install Docker Desktop**:
   - Download from https://www.docker.com/products/docker-desktop/
   - Run installer (enable WSL 2 backend during install)
   - Restart Windows
   - Verify Docker Desktop is running (tray icon → green)

4. **Open Ubuntu terminal** (or PowerShell with WSL):
   ```bash
   docker --version
   docker compose version
   ```

> **Note for Windows users**: All bash commands below run inside **Ubuntu WSL2** terminal, NOT PowerShell directly. Alternatively, use Docker Desktop's built-in terminal.

---

## 2. Clone & Configure

### Ubuntu

```bash
# Clone (or pull latest)
git clone https://github.com/<your-org>/crossmarket-price-analyzer.git
cd crossmarket-price-analyzer

# Create environment file
cp .env.example .env   # if .env.example exists
# Otherwise create it:
cat > .env << 'EOF'
# ── MySQL ─────────────────────────────────────────────────────────────
MYSQL_ROOT_PASSWORD=ChangeMeRoot123!
MYSQL_APP_PASSWORD=ChangeMeApp456!

# ── RabbitMQ ──────────────────────────────────────────────────────────
RABBITMQ_PASSWORD=crossmarket123

# ── AuthService ────────────────────────────────────────────────────────
JWT_SECRETKEY=ChangeMeToAMinimum32CharacterSecret!

# ── SendGrid (optional — real email in v2.1) ────────────────────────────
SENDGRID_APIKEY=

# ── Telegram (optional — real bot in v2.1) ────────────────────────────────
TELEGRAM_BOTTOKEN=

# ── App ──────────────────────────────────────────────────────────────────
APP_BASE_URL=http://localhost
EOF
```

### Windows (WSL2 Ubuntu terminal)

```bash
# Clone into WSL filesystem (faster than /mnt/c)
git clone https://github.com/<your-org>/crossmarket-price-analyzer.git /home/$USER/crossmarket-price-analyzer
cd /home/$USER/crossmarket-price-analyzer

cat > .env << 'EOF'
MYSQL_ROOT_PASSWORD=ChangeMeRoot123!
MYSQL_APP_PASSWORD=ChangeMeApp456!
RABBITMQ_PASSWORD=crossmarket123
JWT_SECRETKEY=ChangeMeToAMinimum32CharacterSecret!
SENDGRID_APIKEY=
TELEGRAM_BOTTOKEN=
APP_BASE_URL=http://localhost
EOF
```

> **Windows path note**: Never store files under `/mnt/c/` (Windows filesystem) — clone into the WSL Ubuntu home directory for best performance.

---

## 3. Docker Compose — Ubuntu

### 3.1 Create the compose file

```bash
cat > docker-compose.yml << 'EOF'
name: crossmarket-analyzer

services:
  # ── Infrastructure ────────────────────────────────────────────────────
  mysql:
    image: mysql:8.0
    container_name: cma-mysql
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: cma_auth
      MYSQL_USER: cma_app
      MYSQL_PASSWORD: ${MYSQL_APP_PASSWORD}
    volumes:
      - mysql_data:/var/lib/mysql
    ports:
      - "3306:3306"
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p${MYSQL_ROOT_PASSWORD}"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    networks:
      - cma-net

  redis:
    image: redis:7-alpine
    container_name: cma-redis
    restart: unless-stopped
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - cma-net

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    container_name: cma-rabbitmq
    restart: unless-stopped
    environment:
      RABBITMQ_DEFAULT_USER: cma
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    ports:
      - "5672:5672"    # AMQP
      - "15672:15672"  # Management UI
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - cma-net

  # ── Backend Services ──────────────────────────────────────────────────
  authservice:
    build:
      context: ./src/Services/AuthService/AuthService.Api
      dockerfile: Dockerfile
    container_name: cma-authservice
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:8080"
      Jwt__Issuer: CrossMarketAuth
      Jwt__Audience: CrossMarketApp
      Jwt__SecretKey: ${JWT_SECRETKEY}
      Jwt__AccessTokenExpirationMinutes: 60
      Jwt__RefreshTokenExpirationDays: 30
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=cma_auth;User=cma_app;Password=${MYSQL_APP_PASSWORD};SslMode=Preferred"
    ports:
      - "5005:8080"
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - cma-net

  notificationservice:
    build:
      context: ./src/Services/NotificationService/NotificationService.Api
      dockerfile: Dockerfile
    container_name: cma-notificationservice
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:8080"
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=cma_notifications;User=cma_app;Password=${MYSQL_APP_PASSWORD};SslMode=Preferred"
      Redis__ConnectionString: "redis:6379"
      RabbitMq__Host: rabbitmq
      RabbitMq__Username: cma
      RabbitMq__Password: ${RABBITMQ_PASSWORD}
      SendGrid__ApiKey: ${SENDGRID_APIKEY}
      Telegram__BotToken: ${TELEGRAM_BOTTOKEN}
      App__BaseUrl: ${APP_BASE_URL}
    ports:
      - "5004:8080"
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    networks:
      - cma-net

  productservice:
    build:
      context: ./src/Services/ProductService/ProductService.Api
      dockerfile: Dockerfile
    container_name: cma-productservice
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:8080"
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=cma_products;User=cma_app;Password=${MYSQL_APP_PASSWORD};SslMode=Preferred"
      Redis__ConnectionString: "redis:6379"
      RabbitMq__Host: rabbitmq
      RabbitMq__Username: cma
      RabbitMq__Password: ${RABBITMQ_PASSWORD}
    ports:
      - "5001:8080"
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    networks:
      - cma-net

  matchingservice:
    build:
      context: ./src/Services/MatchingService/MatchingService.Api
      dockerfile: Dockerfile
    container_name: cma-matchingservice
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:8080"
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=cma_matching;User=cma_app;Password=${MYSQL_APP_PASSWORD};SslMode=Preferred"
      Redis__ConnectionString: "redis:6379"
      RabbitMq__Host: rabbitmq
      RabbitMq__Username: cma
      RabbitMq__Password: ${RABBITMQ_PASSWORD}
    ports:
      - "5002:8080"
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    networks:
      - cma-net

  scoringservice:
    build:
      context: ./src/Services/ScoringService/ScoringService.Api
      dockerfile: Dockerfile
    container_name: cma-scoringservice
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:8080"
      ConnectionStrings__DefaultConnection: "Server=mysql;Port=3306;Database=cma_scoring;User=cma_app;Password=${MYSQL_APP_PASSWORD};SslMode=Preferred"
      Redis__ConnectionString: "redis:6379"
      RabbitMq__Host: rabbitmq
      RabbitMq__Username: cma
      RabbitMq__Password: ${RABBITMQ_PASSWORD}
    ports:
      - "5003:8080"
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    networks:
      - cma-net

  # ── API Gateway ─────────────────────────────────────────────────────
  gateway:
    build:
      context: ./src/Services/CMA.Gateway
      dockerfile: Dockerfile
    container_name: cma-gateway
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: "http://+:8080"
    ports:
      - "8080:8080"
    depends_on:
      - authservice
      - productservice
      - matchingservice
      - scoringservice
      - notificationservice
    networks:
      - cma-net

  # ── Frontend (static) ────────────────────────────────────────────────
  webapp:
    image: nginx:alpine
    container_name: cma-webapp
    restart: unless-stopped
    ports:
      - "3000:80"
    volumes:
      - ./src/Apps/CMA.WebApp/dist:/usr/share/nginx/html:ro
      - ./nginx.conf:/etc/nginx/conf.d/default.conf:ro
    depends_on:
      - gateway
    networks:
      - cma-net

# ── Volumes ──────────────────────────────────────────────────────────────
volumes:
  mysql_data:
  redis_data:
  rabbitmq_data:

# ── Network ──────────────────────────────────────────────────────────────
networks:
  cma-net:
    driver: bridge
EOF
```

### 3.2 Create backend Dockerfiles

Each backend service needs a `Dockerfile` in its Api folder. Create them all:

```bash
# AuthService
cat > src/Services/AuthService/AuthService.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CrossMarket.SharedKernel/CrossMarket.SharedKernel.csproj", "CrossMarket.SharedKernel/"]
COPY ["AuthService.Domain/AuthService.Domain.csproj", "AuthService.Domain/"]
COPY ["AuthService.Application/AuthService.Application.csproj", "AuthService.Application/"]
COPY ["AuthService.Infrastructure/AuthService.Infrastructure.csproj", "AuthService.Infrastructure/"]
COPY ["AuthService.Api/AuthService.Api.csproj", "AuthService.Api/"]
COPY ["../../Common/Common.Domain/Common.Domain.csproj", "../../Common/Common.Domain/"]
COPY ["../../Common/Common.Application/Common.Application.csproj", "../../Common/Common.Application/"]
COPY ["../../Common/Common.Infrastructure/Common.Infrastructure.csproj", "../../Common/Common.Infrastructure/"]
RUN dotnet restore "AuthService.Api/AuthService.Api.csproj"

COPY . .
RUN dotnet build "AuthService.Api/AuthService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuthService.Api/AuthService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuthService.Api.dll"]
EOF

# NotificationService
cat > src/Services/NotificationService/NotificationService.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["NotificationService.Domain/NotificationService.Domain.csproj", "NotificationService.Domain/"]
COPY ["NotificationService.Application/NotificationService.Application.csproj", "NotificationService.Application/"]
COPY ["NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "NotificationService.Infrastructure/"]
COPY ["NotificationService.Api/NotificationService.Api.csproj", "NotificationService.Api/"]
COPY ["../../Common/Common.Domain/Common.Domain.csproj", "../../Common/Common.Domain/"]
COPY ["../../Common/Common.Application/Common.Application.csproj", "../../Common/Common.Application/"]
COPY ["../../Common/Common.Infrastructure/Common.Infrastructure.csproj", "../../Common/Common.Infrastructure/"]
RUN dotnet restore "NotificationService.Api/NotificationService.Api.csproj"

COPY . .
RUN dotnet build "NotificationService.Api/NotificationService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NotificationService.Api/NotificationService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
EOF

# ProductService
cat > src/Services/ProductService/ProductService.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ProductService.Domain/ProductService.Domain.csproj", "ProductService.Domain/"]
COPY ["ProductService.Application/ProductService.Application.csproj", "ProductService.Application/"]
COPY ["ProductService.Contracts/ProductService.Contracts.csproj", "ProductService.Contracts/"]
COPY ["ProductService.Infrastructure/ProductService.Infrastructure.csproj", "ProductService.Infrastructure/"]
COPY ["ProductService.Api/ProductService.Api.csproj", "ProductService.Api/"]
COPY ["../../Common/Common.Domain/Common.Domain.csproj", "../../Common/Common.Domain/"]
COPY ["../../Common/Common.Application/Common.Application.csproj", "../../Common/Common.Application/"]
COPY ["../../Common/Common.Infrastructure/Common.Infrastructure.csproj", "../../Common/Common.Infrastructure/"]
RUN dotnet restore "ProductService.Api/ProductService.Api.csproj"

COPY . .
RUN dotnet build "ProductService.Api/ProductService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProductService.Api/ProductService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductService.Api.dll"]
EOF

# MatchingService
cat > src/Services/MatchingService/MatchingService.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["MatchingService.Domain/MatchingService.Domain.csproj", "MatchingService.Domain/"]
COPY ["MatchingService.Application/MatchingService.Application.csproj", "MatchingService.Application/"]
COPY ["MatchingService.Infrastructure/MatchingService.Infrastructure.csproj", "MatchingService.Infrastructure/"]
COPY ["MatchingService.Api/MatchingService.Api.csproj", "MatchingService.Api/"]
COPY ["../../Common/Common.Domain/Common.Domain.csproj", "../../Common/Common.Domain/"]
COPY ["../../Common/Common.Application/Common.Application.csproj", "../../Common/Common.Application/"]
COPY ["../../Common/Common.Infrastructure/Common.Infrastructure.csproj", "../../Common/Common.Infrastructure/"]
RUN dotnet restore "MatchingService.Api/MatchingService.Api.csproj"

COPY . .
RUN dotnet build "MatchingService.Api/MatchingService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MatchingService.Api/MatchingService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MatchingService.Api.dll"]
EOF

# ScoringService
cat > src/Services/ScoringService/ScoringService.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ScoringService.Domain/ScoringService.Domain.csproj", "ScoringService.Domain/"]
COPY ["ScoringService.Application/ScoringService.Application.csproj", "ScoringService.Application/"]
COPY ["ScoringService.Infrastructure/ScoringService.Infrastructure.csproj", "ScoringService.Infrastructure/"]
COPY ["ScoringService.Api/ScoringService.Api.csproj", "ScoringService.Api/"]
COPY ["../../Common/Common.Domain/Common.Domain.csproj", "../../Common/Common.Domain/"]
COPY ["../../Common/Common.Application/Common.Application.csproj", "../../Common/Common.Application/"]
COPY ["../../Common/Common.Infrastructure/Common.Infrastructure.csproj", "../../Common/Common.Infrastructure/"]
RUN dotnet restore "ScoringService.Api/ScoringService.Api.csproj"

COPY . .
RUN dotnet build "ScoringService.Api/ScoringService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ScoringService.Api/ScoringService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ScoringService.Api.dll"]
EOF

# CMA.Gateway
cat > src/Services/CMA.Gateway/CMA.Gateway.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CMA.Gateway/CMA.Gateway.Api.csproj", "CMA.Gateway/"]
RUN dotnet restore "CMA.Gateway/CMA.Gateway.Api.csproj"
COPY . .
RUN dotnet build "CMA.Gateway/CMA.Gateway.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CMA.Gateway/CMA.Gateway.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CMA.Gateway.Api.dll"]
EOF
```

### 3.3 Create nginx configuration

```bash
cat > nginx.conf << 'EOF'
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # Gzip compression
    gzip on;
    gzip_types text/plain application/json application/javascript text/css;
    gzip_min_length 1000;

    # SPA fallback — all routes to index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy API calls to gateway
    location /api/ {
        proxy_pass         http://gateway:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For  $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
    }

    # WebSocket for real-time scores
    location /ws/ {
        proxy_pass         http://gateway:8080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade  $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host    $host;
        proxy_read_timeout 86400s;
    }
}
EOF
```

### 3.4 Build frontend

```bash
cd src/Apps/CMA.WebApp
npm install
npm run build
cd ../..
# The dist/ folder is mounted by the webapp container
```

### 3.5 Start services

```bash
# Start infrastructure first
docker compose up -d mysql redis rabbitmq

# Wait for MySQL to be ready (about 30 seconds)
docker compose up -d

# Or bring everything up at once
docker compose up -d --build
```

---

## 4. Docker Compose — Windows

> **Assumption**: Ubuntu WSL2 is installed and Docker Desktop is running.

### Step 1: Open Ubuntu Terminal

```powershell
# Option A: from PowerShell
wsl -d Ubuntu

# Option B: from Start menu → Ubuntu 22.04
```

### Step 2: Clone & Configure

Same as Section 2 above — run inside the Ubuntu WSL2 terminal.

### Step 3: Build Frontend

```bash
cd /home/$USER/crossmarket-price-analyzer/src/Apps/CMA.WebApp
npm install
npm run build
cd ../..
```

### Step 4: Start Docker Compose

```bash
docker compose up -d --build
```

### Step 5: (Alternative) Use Docker Desktop GUI

1. Open Docker Desktop → **Containers** → **+ Add**
2. Or: right-click `docker-compose.yml` → **Compose Up**

This starts all containers visible in Docker Desktop's container list with live logs.

---

## 5. Initial Setup

### Create Required MySQL Databases

Services auto-create their databases via `EnsureCreatedAsync()` on first run. No manual SQL needed. To create databases manually:

```bash
docker exec -i cma-mysql mysql -u root -p"${MYSQL_ROOT_PASSWORD}" << 'EOF'
CREATE DATABASE IF NOT EXISTS cma_auth;
CREATE DATABASE IF NOT EXISTS cma_notifications;
CREATE DATABASE IF NOT EXISTS cma_products;
CREATE DATABASE IF NOT EXISTS cma_matching;
CREATE DATABASE IF NOT EXISTS cma_scoring;
CREATE DATABASE IF NOT EXISTS cma_scraping;

GRANT ALL PRIVILEGES ON cma_auth.* TO 'cma_app'@'%';
GRANT ALL PRIVILEGES ON cma_notifications.* TO 'cma_app'@'%';
GRANT ALL PRIVILEGES ON cma_products.* TO 'cma_app'@'%';
GRANT ALL PRIVILEGES ON cma_matching.* TO 'cma_app'@'%';
GRANT ALL PRIVILEGES ON cma_scoring.* TO 'cma_app'@'%';
GRANT ALL PRIVILEGES ON cma_scraping.* TO 'cma_app'@'%';
FLUSH PRIVILEGES;
EOF
```

### Create RabbitMQ Virtual Host

```bash
docker exec cma-rabbitmq rabbitmqctl add_vhost cma_vhost
docker exec cma-rabbitmq rabbitmqctl set_permissions -p cma_vhost cma ".*" ".*" ".*"
```

### Register a Test User

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@crossmarket.example.com","password":"Admin123!","fullName":"Admin User"}'
```

Expected response:
```json
{
  "accessToken": "eyJhbG...",
  "refreshToken": "...",
  "expiresAt": "2026-04-12T...",
  "user": {"id":"...","email":"admin@crossmarket.example.com","fullName":"Admin User"}
}
```

---

## 6. Health Verification

### All Services

```bash
echo "=== Service Health Check ==="
for svc in authservice:5005 notificationservice:5004 productservice:5001 matchingservice:5002 scoringservice:5003 gateway:8080; do
  name="${svc%%:*}"
  port="${svc##*:}"
  status=$(curl -sf "http://localhost:$port/health" 2>/dev/null | grep -o '"Status":"[^"]*"' || echo '"Status":"DOWN"')
  echo "  $name: $status"
done
```

### Container Status

```bash
docker compose ps
```

Expected output:
```
NAME                   STATUS          PORTS
cma-mysql              running (healthy)
cma-redis              running (healthy)
cma-rabbitmq           running (healthy)
cma-authservice        running         0.0.0.0:5005->8080/tcp
cma-notificationservice running        0.0.0.0:5004->8080/tcp
cma-productservice    running         0.0.0.0:5001->8080/tcp
cma-matchingservice   running         0.0.0.0:5002->8080/tcp
cma-scoringservice    running         0.0.0.0:5003->8080/tcp
cma-gateway           running         0.0.0.0:8080->8080/tcp
cma-webapp            running         0.0.0.0:3000->80/tcp
```

### Access Points

| Service | URL | Description |
|---------|-----|-------------|
| WebApp | http://localhost:3000 | React SPA |
| API Gateway | http://localhost:8080 | YARP gateway + Swagger |
| AuthService | http://localhost:5005 | Auth API direct |
| ProductService | http://localhost:5001 | Product API direct |
| RabbitMQ Management | http://localhost:15672 | RabbitMQ UI (cma / crossmarket123) |
| Swagger (via gateway) | http://localhost:8080/swagger | API documentation |

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f authservice
docker compose logs -f gateway --tail=50

# Recent errors only
docker compose logs --since=5m --filter type=stderr
```

---

## 7. Troubleshooting

### MySQL won't start

```bash
# Check logs
docker compose logs mysql

# Common cause: port 3306 already in use on host
# Solution: stop host MySQL or change port mapping
docker compose down
# Edit docker-compose.yml — change "3306:3306" to "3307:3306"
# Then update all ConnectionStrings to use port 3307
docker compose up -d
```

### Backend services fail to start

```bash
# Check health
curl http://localhost:5005/health

# Rebuild without cache
docker compose build --no-cache authservice
docker compose up -d authservice
docker compose logs -f authservice
```

### Frontend shows blank page

```bash
# Verify dist folder exists
ls src/Apps/CMA.WebApp/dist/

# Check webapp container logs
docker compose logs webapp

# Rebuild frontend
cd src/Apps/CMA.WebApp && npm run build && cd ../..
docker compose restart webapp
```

### RabbitMQ not healthy

```bash
# Reset RabbitMQ data
docker compose down -v redis rabbitmq
docker compose up -d redis rabbitmq
sleep 5
docker compose up -d
```

### WSL2 DNS issues (Windows)

```bash
# In Ubuntu terminal
sudo sh -c 'echo "nameserver 8.8.8.8" >> /etc/resolv.conf'
# Or edit /etc/wsl.conf:
sudo tee /etc/wsl.conf << 'EOF'
[wsl2]
nameserver = 8.8.8.8
EOF
# Then restart WSL: wsl --shutdown in PowerShell
```

### Port already in use (Windows)

```powershell
# Find which process uses a port
netstat -ano | findstr ":8080"
# Kill it or change the port mapping in docker-compose.yml
```

### Slow build times

```bash
# Enable Docker BuildKit for parallel builds
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1

# On Windows (PowerShell)
$env:DOCKER_BUILDKIT=1
$env:COMPOSE_DOCKER_CLI_BUILD=1
docker compose up -d --build
```

### Clean restart

```bash
# Stop everything and remove volumes (⚠️ destroys all data)
docker compose down -v --remove-orphans

# Rebuild and start fresh
docker compose up -d --build
```

---

## 8. Kubernetes (Optional)

### Quick Start with k3s (Ubuntu)

```bash
# Install k3s (lightweight Kubernetes)
curl -sfL https://get.k3s.io | sh -

# Verify
kubectl get nodes

# Apply all manifests
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmaps/
kubectl apply -f k8s/secrets/
kubectl apply -f k8s/

# Check pod status
kubectl get pods -n crossmarket
```

### Key Production Considerations

| Item | Recommendation |
|------|----------------|
| **Secrets** | Use `kubectl create secret` or Sealed Secrets — never commit `.env` to git |
| **Ingress** | Use ingress-nginx with TLS via Let's Encrypt (cert-manager) |
| **Storage** | Use persistent volumes for MySQL and RabbitMQ data |
| **Scaling** | Horizontal Pod Autoscaler: `kubectl autoscale deployment authservice --cpu-percent=70 --min=2 --max=10` |
| **Resources** | Set `resources.requests` and `resources.limits` on all containers |
| **Health checks** | Liveness probe on `/health`, readiness probe on `/swagger` |

For full Kubernetes manifests, see `docs/DEPLOYMENT.md`.
