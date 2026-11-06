# Wayni BCRA Challenge

Solución para el procesamiento del padrón de deudores del BCRA (Banco Central de la República
Argentina). Lee el archivo `deudores.txt` de registros de ancho fijo, calcula agregaciones por
deudor y por entidad financiera, las persiste en PostgreSQL y las expone mediante una API REST.
El procesamiento es asíncrono y distribuido, desacoplado mediante una cola de mensajes (SQS).

---

## Índice

- [Objetivo funcional](#objetivo-funcional)
- [Stack tecnológico](#stack-tecnológico)
- [Cómo ejecutar](#cómo-ejecutar)
- [Endpoints](#endpoints)
- [Arquitectura](#arquitectura)
- [Testing](#testing)
- [Configuración](#configuración)
- [Alcance](#alcance)
- [Formato del archivo del BCRA](#formato-del-archivo-del-bcra)

---

## Objetivo funcional

A partir del archivo `deudores.txt` del BCRA, el sistema produce dos agregaciones:

- **Deudores** (agrupados por número de identificación — campo 4):
  - Situación crediticia máxima entre sus registros (campo 6).
  - Suma total de financiación (campo 7).
- **Entidades financieras** (agrupadas por código de entidad — campo 1):
  - Suma total de financiación (campo 7).

Y las expone mediante los endpoints de consulta definidos más abajo.

---

## Stack tecnológico

| Área | Tecnología |
|---|---|
| Backend | .NET 10, ASP.NET Core, C# |
| Frontend | React + Vite + MUI (JavaScript) |
| Arquitectura | Clean Architecture + SOLID |
| Procesamiento asíncrono | .NET Worker Service (`BackgroundService`) |
| Persistencia | PostgreSQL + Entity Framework Core (Code First) |
| Ingesta masiva | Npgsql `COPY` |
| Mensajería | Amazon SQS (emulado con LocalStack) |
| Almacenamiento | Amazon S3 (emulado con LocalStack) |
| Notificaciones | Email vía SMTP (Mailpit) y log estructurado |
| Testing | xUnit (unitarios) + Testcontainers (integración) |
| Infraestructura | Docker / Docker Compose |

---

## Cómo ejecutar

### Requisitos previos

- [Docker](https://www.docker.com/) y Docker Compose (Docker Desktop en Windows/macOS).

No hace falta instalar .NET, PostgreSQL ni nada más: todo corre en contenedores.

### Pasos

```bash
# 1. Copiar la plantilla de variables de entorno
cp .env.example .env

# 2. Levantar todos los servicios
docker-compose up --build
```

Eso levanta seis servicios:

- **PostgreSQL** (puerto 5432): base de datos.
- **LocalStack** (puerto 4566): emula S3 y SQS. Al arrancar crea el bucket, la cola y la DLQ.
- **API** (puerto 8080): expone los endpoints. Aplica las migraciones al iniciar.
- **Worker**: consume la cola, procesa los archivos y envía las notificaciones.
- **Frontend** (puerto 5173): interfaz web para subir el archivo y consultar resultados.
- **Mailpit** (puerto 8025): captura los emails de notificación y los muestra en su interfaz web.

Una vez levantado, la API queda disponible en `http://localhost:8080` y la interfaz web en
`http://localhost:5173`.

### Swagger

La documentación interactiva de la API está en:

```
http://localhost:8080/swagger
```

Desde ahí se pueden probar todos los endpoints, incluido el upload del archivo.

> El servicio `api` corre con `ASPNETCORE_ENVIRONMENT=Development` en el compose para que Swagger UI
> esté disponible dentro del contenedor (comodidad para la evaluación). En un entorno productivo se
> correría en `Production` y Swagger no se expondría.

### Interfaz web

Una interfaz web sencilla (React + Vite + MUI) queda disponible en:

```
http://localhost:5173
```

Permite usar la API desde el navegador, en tres secciones:

- **Subir archivo:** carga el padrón y muestra el identificador del import.
- **Deudores:** consulta un deudor por identificación y lista el top N por financiación.
- **Entidades:** consulta una entidad financiera por código.

> La interfaz consume la API vía la variable `VITE_API_URL` (por defecto `http://localhost:8080`, la
> URL de la API vista desde el navegador). La API habilita CORS para el origen del frontend
> (`http://localhost:5173`), configurable mediante `Cors__AllowedOrigins__0`. En el compose el frontend
> corre con el dev server de Vite (suficiente para la evaluación); en producción se serviría como build
> estático detrás de un servidor web.

### Notificaciones (Mailpit)

Al finalizar cada importación, el sistema envía un email de notificación con el resumen del proceso.
Los emails se capturan en la interfaz web de Mailpit:

```
http://localhost:8025
```

Subí un archivo desde la interfaz web y, al completarse el procesamiento, el email aparece en Mailpit.

---

## Endpoints

Rutas según el enunciado (en español). Los campos JSON de las respuestas también usan los nombres del
enunciado (`nro_identificacion`, `situacion_maxima`, `suma_total_prestamos`, `codigo_entidad`).

### `POST /upload`

Carga el archivo del BCRA e inicia el procesamiento asíncrono. Recibe el archivo como
`multipart/form-data` (campo `file`). Responde `202 Accepted` con el id del import.

```bash
curl -F "file=@deudores.txt" http://localhost:8080/upload
```

```json
{ "importJobId": "eb598c34-9d5d-4e94-a635-9861c2dbad78" }
```

El procesamiento ocurre en segundo plano (Worker). El estado del import se registra en la base.

### `GET /deudores/{cuit}`

Devuelve el deudor por número de identificación.

```bash
curl http://localhost:8080/deudores/20004256337
```

```json
{
  "nro_identificacion": "20004256337",
  "situacion_maxima": "05",
  "suma_total_prestamos": 0.0
}
```

Devuelve `404` si no existe.

### `GET /deudores/top/{n}`

Devuelve los N deudores con mayor suma de financiación, ordenados de mayor a menor.

```bash
curl http://localhost:8080/deudores/top/5
```

### `GET /entidades/{codigo}`

Devuelve la entidad financiera por código.

```bash
curl http://localhost:8080/entidades/00007
```

```json
{
  "codigo_entidad": "00007",
  "suma_total_prestamos": 700382.0
}
```

### `GET /health`

Verifica el estado de las dependencias de runtime (PostgreSQL, S3 y SQS). Devuelve `200` con estado
`Healthy` si todas responden, o `503` con estado `Unhealthy` si alguna falla. La respuesta incluye el
desglose por dependencia. Pensado para orquestadores de contenedores y monitoreo.

```bash
curl http://localhost:8080/health
```

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1599524",
  "checks": [
    { "name": "postgres", "status": "Healthy", "duration": "00:00:00.0066522", "description": null },
    { "name": "s3",       "status": "Healthy", "duration": "00:00:00.1108963", "description": null },
    { "name": "sqs",      "status": "Healthy", "duration": "00:00:00.1102253", "description": null }
  ]
}
```

Cuando una dependencia falla, su `status` pasa a `Unhealthy` y `description` indica el motivo.

---

## Arquitectura

El proyecto sigue Clean Architecture, con las dependencias apuntando hacia el dominio (las capas
externas conocen a las internas, nunca al revés).

```
src/
├── WayniBcra.Domain          # Entidades y reglas de dominio. Sin dependencias externas.
├── WayniBcra.Application      # Casos de uso e interfaces (puertos). Depende solo de Domain.
├── WayniBcra.Infrastructure   # Implementaciones: EF Core, Npgsql/COPY, S3, SQS. Depende de Application.
├── WayniBcra.Api              # Host HTTP. Controllers y DTOs. Depende de Application + Infrastructure.
└── WayniBcra.Worker           # BackgroundService que consume la cola. Depende de Application + Infrastructure.
```

La API y el Worker corren como **contenedores separados**, comunicados por la cola SQS y compartiendo
PostgreSQL como fuente de verdad. Esto permite escalarlos de forma independiente y aísla fallos.

### Flujo de procesamiento (asíncrono)

```
Cliente → POST /upload → API
                          ├─ guarda el archivo en S3
                          ├─ crea un ImportJob (estado Pending)
                          ├─ publica un mensaje en la cola (SQS)
                          └─ responde 202 Accepted

Cola (SQS) → Worker: consumidor de importación
              ├─ descarga el archivo de S3
              ├─ lo lee por streaming y parsea según el layout del BCRA
              ├─ ingesta a tabla staging (COPY) y agrega en PostgreSQL
              ├─ persiste resultados en debtors y financial_entities (transaccional)
              ├─ actualiza el estado del ImportJob (Completed/Failed) con su resumen
              ├─ registra el mensaje de notificación en la misma transacción (outbox)
              └─ registra un log estructurado con el tiempo total y el resumen de carga

Worker: dispatcher de notificaciones (en paralelo)
              ├─ toma los mensajes pendientes del outbox
              ├─ envía el email de finalización (SMTP → Mailpit)
              └─ reintenta ante fallos; reprograma los que no se pudieron enviar
```

El procesamiento no ocurre en el request HTTP: la API responde de inmediato y el trabajo pesado corre
en el Worker. Esto permite tolerar archivos grandes y reintentos sin bloquear al cliente.

La notificación se registra en la misma transacción que completa la importación (patrón Outbox) y se
envía por separado: así una caída del servidor de correo no afecta el resultado de la importación.

---

## Testing

```bash
# Tests unitarios (rápidos, sin dependencias)
dotnet test tests/WayniBcra.UnitTests

# Tests de integración (requieren Docker: levantan un PostgreSQL efímero con Testcontainers)
dotnet test tests/WayniBcra.IntegrationTests
```

- **Unitarios:** parser del archivo (offsets, formato numérico, líneas inválidas, códigos de situación),
  y transiciones de estado del `ImportJob`.
- **Integración:** el procesamiento completo (`COPY` + agregación SQL) contra un PostgreSQL real, cubriendo
  el ranking de gravedad, los totales por entidad y la idempotencia del reproceso.

---

## Configuración

Las credenciales y configuraciones se externalizan mediante variables de entorno (`.env`), nunca
hardcodeadas en el repositorio. Se provee `.env.example` como plantilla documentada.

- **Docker:** las variables vienen del `.env` (ver `.env.example`).
- **Desarrollo local** (corriendo con `dotnet run` fuera de Docker): la connection string se toma de
  User Secrets.

---

## Alcance

Funcionalidades implementadas:

- Importador asíncrono y distribuido: `POST /upload` → S3 → SQS → Worker.
- Lectura por streaming y parseo del archivo de ancho fijo del BCRA.
- Ingesta masiva con `COPY` y agregación en PostgreSQL (por deudor y por entidad).
- API REST de consulta (los tres endpoints del enunciado).
- Persistencia con EF Core Code First y migraciones versionadas.
- Resiliencia: reintentos vía SQS, Dead Letter Queue, reproceso idempotente.
- Notificación de finalización por email (SMTP → Mailpit), registrada transaccionalmente (Outbox)
  y enviada por un dispatcher con reintentos. Además, log estructurado con tiempo total y resumen.
- Health check (`/health`) con verificación de PostgreSQL, S3 y SQS, y desglose por dependencia.
- Interfaz web (React + Vite + MUI) para subir el archivo y consultar deudores y entidades.
- Contenerización completa con Docker Compose (`docker-compose up`).
- Tests unitarios (parser, dominio, notificaciones) y de integración (procesamiento contra PostgreSQL real).

---

## Formato del archivo del BCRA

El archivo `deudores.txt` es de ancho fijo (171 caracteres por línea, sin separadores). Los campos
utilizados por esta solución son:

| Campo | Nombre | Posición (0-based) | Longitud |
|---|---|---|---|
| 1 | Código de entidad | `[0:5]` | 5 |
| 4 | Número de identificación | `[13:24]` | 11 |
| 6 | Situación | `[27:29]` | 2 |
| 7 | Financiación | `[29:41]` | 12 |

Los importes están expresados en miles de pesos con un decimal.
