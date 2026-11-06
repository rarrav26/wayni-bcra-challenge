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
  - [Swagger](#swagger)
  - [Interfaz web](#interfaz-web)
  - [Notificaciones (Mailpit)](#notificaciones-mailpit)
  - [Logs](#logs)
  - [Probar los endpoints (archivo `.http`)](#probar-los-endpoints-archivo-http)
- [Endpoints](#endpoints)
  - [Manejo de errores](#manejo-de-errores)
- [Arquitectura](#arquitectura)
- [Testing](#testing)
- [Configuración](#configuración)
- [Alcance](#alcance)
- [Formato del archivo del BCRA](#formato-del-archivo-del-bcra)

---

## Objetivo funcional

A partir del archivo `deudores.txt` del BCRA, el sistema produce dos agregaciones:

- **Deudores** (agrupados por número de identificación — campo 4):
  - Situación crediticia de mayor gravedad entre sus registros (campo 6).
  - Suma total de financiación (campo 7).
- **Entidades financieras** (agrupadas por código de entidad — campo 1):
  - Suma total de financiación (campo 7).

Y las expone mediante los endpoints de consulta definidos más abajo.

> **Sobre la "situación máxima":** en la escala del BCRA la situación crediticia no es un valor ordinal
> (p. ej. "11 — con asistencias" no es peor que "01 — normal"), por lo que tomar el máximo numérico del
> campo no reflejaría el riesgo real. Se interpreta "máxima" como la de **mayor gravedad crediticia**
> según un ranking documentado, y se persiste siempre el código original del BCRA. Para un deudor con
> situaciones "01" y "05" se devuelve "05".

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
| Observabilidad | Logs estructurados a stdout + CloudWatch Logs (LocalStack), correlados por import |
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
- **LocalStack** (puerto 4566): emula S3, SQS y CloudWatch Logs. Al arrancar crea el bucket, la cola,
  la DLQ, el log group y la notificación S3 → SQS.
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
- **Deudores:** consulta un deudor por identificación y lista el top N por financiación, con un filtro
  opcional por situación crediticia.
- **Entidades:** consulta una entidad financiera por código.

> La interfaz consume la API vía la variable `VITE_API_URL` (por defecto `http://localhost:8080`, la
> URL de la API vista desde el navegador). La API habilita CORS para el origen del frontend
> (`http://localhost:5173`), configurable mediante `Cors__AllowedOrigins__0`. El frontend se construye
> como sitio estático y se sirve con nginx (imagen liviana, estilo productivo).

### Notificaciones (Mailpit)

Al finalizar cada importación, el sistema envía un email de notificación con el resumen del proceso.
Los emails se capturan en la interfaz web de Mailpit:

```
http://localhost:8025
```

Subí un archivo desde la interfaz web y, al completarse el procesamiento, el email aparece en Mailpit.

### Logs

Los logs son estructurados (`ILogger`) y se escriben a `stdout` (estándar para contenedores) y, de
forma centralizada, a **CloudWatch Logs** (emulado con LocalStack). Cada operación de importación lleva
su `ImportJobId` en todos los logs (API y Worker), de modo que puede trazarse de punta a punta.

**Opción rápida — logs por servicio (stdout):**

```bash
docker-compose logs -f worker   # o api, postgres, etc.
```

**Opción centralizada — CloudWatch Logs (API + Worker en un mismo grupo):**

```bash
# Ver los últimos eventos (API y Worker juntos)
docker-compose exec localstack awslocal logs filter-log-events --log-group-name wayni-bcra-logs --limit 50

# Trazar una importación concreta por su id (aprovecha la correlación por ImportJobId)
docker-compose exec localstack awslocal logs filter-log-events \
  --log-group-name wayni-bcra-logs \
  --filter-pattern "<importJobId>"
```

> Se usa `filter-log-events` (compatible con el AWS CLI v1 incluido en la imagen de LocalStack). El
> logging estructurado a stdout está listo para conectar cualquier backend de observabilidad
> (CloudWatch, Seq, Datadog) sin tocar el código.

### Probar los endpoints (archivo `.http`)

En `src/WayniBcra.Api/WayniBcra.Api.http` hay ejemplos listos para ejecutar: Visual Studio los reconoce
con su botón *Send Request*, y también funcionan con la extensión *REST Client* de VS Code o con Rider.
Cubre el health check, el paso 1 de la carga (`POST /upload`) y las consultas. El paso 2 (el `PUT`
binario directo a S3) se hace con `curl` o desde la interfaz web: el cliente `.http` de Visual Studio
no envía bien el cuerpo binario, así que queda documentado como comentario dentro del archivo.

---

## Endpoints

Rutas según el enunciado (en español). Los campos JSON de las respuestas también usan los nombres del
enunciado (`nro_identificacion`, `situacion_maxima`, `suma_total_prestamos`, `codigo_entidad`).

### `POST /upload`

Inicia una importación. **No recibe el archivo**: el padrón del BCRA pesa varios GB, así que el archivo
se sube **directamente a S3** mediante una URL pre-firmada (patrón _pre-signed URL_), sin pasar por la
API. El upload se hace en dos pasos:

**Paso 1 — registrar la importación y obtener la URL.** Se envía solo el nombre del archivo; responde
`202 Accepted` con el id del import y la URL pre-firmada:

```bash
curl -X POST http://localhost:8080/upload \
  -H "Content-Type: application/json" \
  -d '{ "fileName": "deudores.txt" }'
```

```json
{
  "importJobId": "eb598c34-9d5d-4e94-a635-9861c2dbad78",
  "uploadUrl": "http://localhost:4566/wayni-bcra-imports/imports/eb59.../deudores.txt?X-Amz-..."
}
```

**Paso 2 — subir el archivo a la URL pre-firmada** (PUT directo a S3). Reemplazá la URL por el valor
`uploadUrl` del paso anterior (entre comillas, por los `&` del query string):

```bash
curl -X PUT --data-binary "@deudores.txt" "http://localhost:4566/wayni-bcra-imports/imports/eb59.../deudores.txt?X-Amz-..."
```

> Usá `--data-binary "@archivo"` (con `@`), no `-d`: así se preservan los saltos de línea del formato
> de ancho fijo. El mismo comando está en el archivo `.http` como referencia.

Al completarse el upload, S3 emite un evento que dispara el procesamiento asíncrono (S3 → SQS → Worker).
El estado del import se registra en la base y, al finalizar, se envía la notificación por email.

> **Interfaz web:** la página de carga encadena estos dos pasos de forma transparente — se elige el
> archivo y se sube con un solo clic. Los dos pasos manuales (arriba) aplican al usar `curl`/Postman.
>
> **Swagger:** el paso 1 se puede ejecutar desde Swagger UI; el paso 2 (PUT a S3) no, porque la URL no
> pertenece a la API. Usá la interfaz web o `curl` para la subida.

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

Acepta el parámetro opcional `situacion` para filtrar por situación crediticia máxima (código de 2
dígitos del BCRA). Devuelve los N deudores con esa situación, ordenados por financiación:

```bash
curl "http://localhost:8080/deudores/top/5?situacion=05"
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

### Manejo de errores

Las respuestas de error siguen el estándar **ProblemDetails (RFC 7807)**: un cuerpo JSON uniforme con
`status`, `title` y `detail`. Aplica tanto a los errores de validación (p. ej. `400` por un `n <= 0` en
`/deudores/top/{n}`) como a las excepciones no controladas (`500`), gracias a un handler global. Los
códigos de respuesta de cada endpoint están documentados en Swagger vía `[ProducesResponseType]`.

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
                          ├─ crea un ImportJob (estado Pending)
                          ├─ genera una URL pre-firmada (S3) para ese import
                          └─ responde 202 Accepted { importJobId, uploadUrl }

Cliente → PUT (uploadUrl) → S3        (el archivo sube DIRECTO a S3, no pasa por la API)
                             └─ al completarse, S3 emite un evento ObjectCreated → SQS

Cola (SQS) → Worker: consumidor de importación
              ├─ descarga el archivo de S3 por streaming (memoria acotada)
              ├─ lo parsea según el layout del BCRA (ancho fijo)
              ├─ ingesta a tabla staging (COPY) y agrega en PostgreSQL
              ├─ persiste resultados en debtors y financial_entities (transaccional)
              ├─ marca el ImportJob (Completed/Failed) y registra el mensaje de notificación
              │  en la MISMA transacción (patrón Outbox)
              └─ registra un log estructurado con el tiempo total y el resumen de carga

Worker: dispatcher de notificaciones (en paralelo)
              ├─ toma los mensajes pendientes del outbox (polling con lock)
              ├─ envía el email de finalización (SMTP → Mailpit)
              └─ reintenta ante fallos; reprograma los que no se pudieron enviar
```

El archivo no pasa por la API (sube directo a S3 con la URL pre-firmada), lo que permite padrones de
varios GB. El procesamiento corre en el Worker, por streaming, sin bloquear al cliente ni cargar el
archivo en memoria.

La notificación se registra en la misma transacción que completa la importación (patrón Outbox) y se
envía por separado: así una caída del servidor de correo no afecta el resultado de la importación.

---

## Testing

Hay tres proyectos de tests:

- **Unitarios** (`WayniBcra.UnitTests`): parser del archivo (offsets, formato numérico, líneas inválidas,
  códigos de situación), transiciones de estado de `ImportJob` y `OutboxMessage` (incluido el backoff de
  reintentos), y el handler de procesamiento (outbox transaccional, reintento de descarga ante fallos
  transitorios).
- **Integración** (`WayniBcra.IntegrationTests`): el procesamiento completo (`COPY` + agregación SQL)
  contra un PostgreSQL real (Testcontainers), cubriendo el ranking de gravedad, los totales por entidad y
  la idempotencia del reproceso; y la consulta de deudores con el filtro opcional por situación.
- **API** (`WayniBcra.ApiTests`): rutas y status de los endpoints, los nombres JSON en español del
  contrato, y las respuestas de error (ProblemDetails), con `WebApplicationFactory`.

```bash
# Toda la suite
dotnet test

# Un proyecto puntual (los de integración y API requieren Docker)
dotnet test tests/WayniBcra.UnitTests
```

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
- Upload directo a S3 mediante URL pre-firmada (el archivo de varios GB no pasa por la API); el
  procesamiento se dispara por el evento de S3.
- Lectura por streaming y parseo del archivo de ancho fijo del BCRA.
- Ingesta masiva con `COPY` y agregación en PostgreSQL (por deudor y por entidad).
- API REST de consulta (los tres endpoints del enunciado), con filtro opcional por situación en el top N.
- Persistencia con EF Core Code First y migraciones versionadas.
- Resiliencia: reintentos vía SQS, Dead Letter Queue, reproceso idempotente.
- Notificación de finalización por email (SMTP → Mailpit), registrada transaccionalmente (Outbox)
  y enviada por un dispatcher con reintentos. Además, log estructurado con tiempo total y resumen.
- Health check (`/health`) con verificación de PostgreSQL, S3 y SQS, y desglose por dependencia.
- Manejo de errores con ProblemDetails (RFC 7807) y handler global de excepciones; status documentados
  en OpenAPI.
- Observabilidad: logs estructurados a stdout + CloudWatch Logs (LocalStack) centralizados y correlados
  por `ImportJobId`.
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
