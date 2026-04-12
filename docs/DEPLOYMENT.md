# P4-T06: Production Deployment Guide — v2.0

## Prerequisites

- Docker & Docker Compose installed
- Kubernetes cluster (or Docker Compose for single-node)
- Domain: `app.crossmarket.example.com`
- SMTP: SendGrid account (or configurable SMTP)
- Telegram Bot: [@CrossMarketBot](https://t.me/) created via BotFather

---

## Environment Variables

### AuthService

```bash
JWT_SECRETKEY="<min-32-char-random-string>"
ConnectionStrings__DefaultConnection="Server=mysql;Database=cma_auth;User=cma_app;Password=<secret>;SslMode=Preferred"
```

### NotificationService

```bash
ConnectionStrings__DefaultConnection="Server=mysql;Database=cma_notifications;User=cma_app;Password=<secret>;SslMode=Preferred"
SENDGRID_APIKEY="<sendgrid-api-key>"
TELEGRAM_BOTTOKEN="<telegram-bot-token>"
App__BaseUrl="https://app.crossmarket.example.com"
```

### ProductService, MatchingService, ScoringService

```bash
ConnectionStrings__DefaultConnection="Server=mysql;Database=cma_<service>;User=cma_app;Password=<secret>;SslMode=Preferred"
Redis__ConnectionString="redis:6379"
RabbitMq__Host="rabbitmq"
```

---

## Docker Compose (Single-Node Production)

```yaml
# docker-compose.prod.yml (abbreviated)
version: '3.9'
services:
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: cma_auth
      MYSQL_USER: cma_app
      MYSQL_PASSWORD: ${MYSQL_APP_PASSWORD}
    volumes:
      - mysql_data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]

  rabbitmq:
    image: rabbitmq:3-management
    environment:
      RABBITMQ_DEFAULT_USER: cma
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]

  authservice:
    build: ./src/Services/AuthService/AuthService.Api
    ports:
      - "5005:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Jwt__SecretKey=${JWT_SECRETKEY}
      - ConnectionStrings__DefaultConnection=Server=mysql;Database=cma_auth;User=cma_app;Password=${MYSQL_APP_PASSWORD}
    depends_on:
      mysql:
        condition: service_healthy

  notificationservice:
    build: ./src/Services/NotificationService/NotificationService.Api
    ports:
      - "5004:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=mysql;Database=cma_notifications;User=cma_app;Password=${MYSQL_APP_PASSWORD}
      - SendGrid__ApiKey=${SENDGRID_APIKEY}
      - Telegram__BotToken=${TELEGRAM_BOTTOKEN}
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy

  gateway:
    build: ./src/Services/CMA.Gateway
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production

  webapp:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./dist:/usr/share/nginx/html:ro
      - ./nginx.conf:/etc/nginx/conf.d/default.conf:ro
    depends_on:
      - gateway

volumes:
  mysql_data:
```

---

## Health Checks

All services expose `/health`:

```bash
# Verify all services are healthy
for svc in 5001 5002 5003 5004 5005 8080; do
  curl -sf "http://localhost:$svc/health" || echo "FAIL: $svc"
done
```

---

## Kubernetes (Multi-Node)

Apply manifests in order:

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmaps/
kubectl apply -f k8s/secrets/
kubectl apply -f k8s/mysql.yaml
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/rabbitmq.yaml
kubectl apply -f k8s/authservice.yaml
kubectl apply -f k8s/notificationservice.yaml
kubectl apply -f k8s/gateway.yaml
kubectl apply -f k8s/webapp.yaml
```

Horizontal Pod Autoscaler for AuthService:

```bash
kubectl autoscale deployment authservice \
  --cpu-percent=70 --min=2 --max=10
```

---

## Post-Deployment Checklist

- [ ] All `/health` endpoints return 200
- [ ] DNS resolves `app.crossmarket.example.com` → Load Balancer IP
- [ ] TLS certificate issued (Let's Encrypt or commercial CA)
- [ ] JWT secret set via env var (not in source code)
- [ ] MySQL databases created: `cma_auth`, `cma_notifications`
- [ ] Redis connectivity verified
- [ ] RabbitMQ virtual host and users created
- [ ] SendGrid API key validated
- [ ] Telegram bot token validated
- [ ] Swagger UI accessible at `/swagger` (Development only)
- [ ] Playwright E2E tests pass against production URL
- [ ] Security audit findings addressed (see `docs/security/SECURITY_AUDIT.md`)
- [ ] Monitoring: Prometheus metrics scraped from all services
- [ ] Alerting: PagerDuty/OpsGenie connected for critical alerts
- [ ] Backup: MySQL automated daily backup configured
