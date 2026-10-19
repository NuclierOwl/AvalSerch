# Docker: AvalNeroSercher

Инструкция по сборке и запуску приложения в Docker.

Приложение — Avalonia GUI с распознаванием (Emgu.CV + ML) и HTTP API на порту **5050**. В контейнере GUI работает через виртуальный дисплей Xvfb; снаружи удобнее пользоваться API.

## Требования

- [Docker](https://docs.docker.com/get-docker/) (Desktop на Windows/macOS или Engine на Linux)
- Папка с ML-моделями `*.zip` (по умолчанию `AvalNeroSercher/Ml_Model`)

## Сборка образа

Из корня репозитория:

```bash
docker build -t avalnero-searcher .
```

Первая сборка займёт несколько минут (SDK + publish + нативные зависимости OpenCV).

## Запуск (API)

```bash
docker run --rm -it ^
  -p 5050:5050 ^
  -v "%CD%\AvalNeroSercher\Ml_Model:/app/Ml_Model" ^
  avalnero-searcher
```

Linux / macOS / Git Bash:

```bash
docker run --rm -it \
  -p 5050:5050 \
  -v "$(pwd)/AvalNeroSercher/Ml_Model:/app/Ml_Model" \
  avalnero-searcher
```

После старта API доступен на хосте:

| Метод | URL | Описание |
|-------|-----|----------|
| GET | `http://localhost:5050/api/status` | Статус приложения |
| GET | `http://localhost:5050/api/cameras` | Список камер |
| GET | `http://localhost:5050/api/models` | Список моделей |
| POST | `http://localhost:5050/api/models/load` | Загрузить модель |
| POST | `http://localhost:5050/api/predict` | Предсказание по изображению |

### Примеры запросов

Статус:

```bash
curl http://localhost:5050/api/status
```

Загрузка модели по имени файла:

```bash
curl -X POST http://localhost:5050/api/models/load ^
  -H "Content-Type: application/json" ^
  -d "{\"command\":\"models/load\",\"data\":{\"name\":\"model.zip\"}}"
```

Предсказание по файлу (multipart):

```bash
curl -X POST http://localhost:5050/api/predict -F "file=@digit.png"
```

## Камера внутри контейнера

Передайте устройство видео (Linux / WSL2 с пробросом USB):

```bash
docker run --rm -it \
  -p 5050:5050 \
  --device=/dev/video0:/dev/video0 \
  -v "$(pwd)/AvalNeroSercher/Ml_Model:/app/Ml_Model" \
  avalnero-searcher
```

На Windows Docker Desktop доступ к веб-камере часто ограничен; для проверки удобнее API `/api/predict` с загрузкой изображения.

Затем:

```bash
curl -X POST http://localhost:5050/api/camera/select \
  -H "Content-Type: application/json" \
  -d "{\"data\":{\"index\":0}}"

curl -X POST http://localhost:5050/api/capture/start
curl -X POST http://localhost:5050/api/capture/stop
```

## Переменные окружения

| Переменная | По умолчанию в Docker | Описание |
|------------|------------------------|----------|
| `AVALNERO_API_HOST` | `0.0.0.0` | Адрес привязки HTTP API |
| `AVALNERO_API_PORT` | `5050` | Порт API |
| `DISPLAY` | `:99` | X-дисплей (Xvfb) |

Пример с другим портом:

```bash
docker run --rm -it -p 8080:8080 \
  -e AVALNERO_API_PORT=8080 \
  -v "$(pwd)/AvalNeroSercher/Ml_Model:/app/Ml_Model" \
  avalnero-searcher
```

## GUI на экране хоста (Linux)

Если нужен видимый интерфейс Avalonia, пробросьте X11:

```bash
xhost +local:docker

docker run --rm -it \
  -e DISPLAY=$DISPLAY \
  -v /tmp/.X11-unix:/tmp/.X11-unix \
  -p 5050:5050 \
  -v "$(pwd)/AvalNeroSercher/Ml_Model:/app/Ml_Model" \
  --entrypoint ./AvalNeroSercher \
  avalnero-searcher
```

`--entrypoint` отключает встроенный Xvfb и использует дисплей хоста.

На Windows для GUI обычно достаточно запускать приложение локально (`dotnet run`), а Docker использовать для API.

## docker-compose (опционально)

```bash
docker compose up --build
```

См. файл `docker-compose.yml` в корне репозитория.

## Структура файлов

```
Dockerfile              # многостадийная сборка
.dockerignore           # исключает bin/obj и лишнее
docker/entrypoint.sh    # старт Xvfb + приложение
DOCKER.md               # эта инструкция
```

## Типичные проблемы

**Нет моделей** — смонтируйте каталог с `*.zip` в `/app/Ml_Model`.

**`Cannot open display`** — entrypoint не запустился; пересоберите образ и не переопределяйте `ENTRYPOINT` без необходимости.

**Камера не видна** — нужен `--device=/dev/video0` и права на устройство; на Windows чаще используйте `/api/predict`.

**Порт занят** — смените проброс `-p 5051:5050` или задайте `AVALNERO_API_PORT`.
