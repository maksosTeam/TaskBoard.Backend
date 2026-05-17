# TaskBoard Backend

## Обзор проекта

TaskBoard Backend — это микросервисная платформа для управления задачами и проектами. Репозиторий содержит несколько .NET 10 сервисов, общие библиотеки, конфигурацию Docker Compose и инфраструктуру для CI/CD.

Основной публичный домен: `https://project-domain.ru/`

Фронтенд находится в отдельном репозитории:
- https://github.com/maksosTeam/TaskBoardFE

## Архитектура

Проект организован как набор контейнеризированных сервисов, связанных через общую Docker-сеть `taskboard-network`.

### Компоненты

- `nginx` — HTTPS reverse proxy и фронтенд-проxy.
  - Прокси для `/api/` направляется на `gateway-service`.
  - Прокси для `/` направляется на клиент на `client:5173`.
  - SSL-сертификаты хранятся в `docker/nginx/ssl/`.
  - Сервер настроен на `project-domain.ru`.

- `gateway-service` — главный API-шлюз, который принимает запросы со стороны frontend и маршрутизирует их к микросервисам.

- `user-service` — сервис учёта пользователей, авторизации и профилей.

- `project-service` — сервис управления проектами, досками, задачами и интеграцией с GitHub/webhook.

- `analytics-service` — сервис аналитики.

- `kafka` + `zookeeper` — система обмена событиями и очередями.
  - Используется для обмена сообщениями между сервисами и создания топиков задач, комментариев, приглашений и пр.

- Каждый ключевой сервис имеет собственную базу данных PostgreSQL.

### Дополнительные модули

- `Microservices/NotificationService` — отдельный сервис уведомлений, который идёт в составе репозитория, но не входит в основной `docker-compose.yml`.

- `Shared/` — общие библиотеки и Kafka-клиенты, используемые всеми сервисами.

## Технологии

- .NET 10 (ASP.NET Core)
- C#
- Docker / Docker Compose
- Nginx
- PostgreSQL
- Apache Kafka + Zookeeper
- GitHub Actions CI/CD

## CI/CD

В репозитории настроена автоматическая сборка и деплой через GitHub Actions:
- Файл: `.github/workflows/deploy.yml`
- Триггер: `push` в ветку `master`
- Действия:
  - сборка Docker-образов для `user-service`, `gateway-service`, `project-service`, `analytics-service`
  - публикация образов в GitHub Container Registry (`ghcr.io/maksosteam`)
  - копирование `docker-compose.yml` и каталога `docker` на сервер через SSH
  - запуск `docker compose pull` и `docker compose up -d`
  - перезапуск контейнера `nginx-proxy`

## HTTPS

HTTPS настроен на уровне `nginx`.

- Основной домен: `project-domain.ru`
- Сертификаты подгружаются из:
  - `/etc/nginx/ssl/fullchain.pem`
  - `/etc/nginx/ssl/key.pem`

Конфигурация находится в `docker/nginx/conf.d/default.conf`.

## Запуск

### 1. Предварительные требования

- Docker
- Docker Compose
- Git
- Наличие `.env` файлов для каждого сервиса или общего окружения

### 2. Запуск всех сервисов

В корне репозитория:

```powershell
cd d:\Codes\TaskBoard.Backend
docker compose up -d
```

### 3. Остановка сервисов

```powershell
docker compose down
```

### 4. Просмотр логов

```powershell
docker compose logs -f
```

## Структура репозитория

- `docker/` — Docker Compose и Dockerfile для сервисов
- `Microservices/` — исходный код микросервисов
- `Shared/` — общие библиотеки и Kafka-сервисы
- `.github/workflows/deploy.yml` — CI/CD pipeline
- `TaskBoardAPI.sln` — решение проекта

## Полезные ссылки

- Фронтенд: https://github.com/maksosTeam/TaskBoardFE

