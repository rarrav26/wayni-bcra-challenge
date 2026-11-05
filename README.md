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
- [Decisiones de diseño](#decisiones-de-diseño)
- [Testing](#testing)
- [Configuración](#configuración)
- [Alcance y mejoras futuras](#alcance-y-mejoras-futuras)
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
| Arquitectura | Clean Architecture + SOLID |
| Procesamiento asíncrono | .NET Worker Service (`BackgroundService`) |
| Persistencia | PostgreSQL + Entity Framework Core (Code First) |
| Ingesta masiva | Npgsql `COPY` |
| Mensajería | Amazon SQS (emulado con LocalStack) |
| Almacenamiento | Amazon S3 (emulado con LocalStack) |
| Notificaciones | Log estructurado con tiempo total y resumen de carga |
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

# 2. Levantar todos los servicios (API, Worker, PostgreSQL, LocalStack)
docker-compose up --build
```

Eso levanta cuatro servicios:

- **PostgreSQL** (puerto 5432): base de datos.
- **LocalStack** (puerto 4566): emula S3 y SQS. Al arrancar crea el bucket, la cola y la DLQ.
- **API** (puerto 8080): expone los endpoints. Aplica las migraciones al iniciar.
- **Worker**: consume la cola y procesa los archivos.

Una vez levantado, la API queda disponible en `http://localhost:8080`.

### Swagger

La documentación interactiva de la API está en:

```
http://localhost:8080/swagger
```

Desde ahí se pueden probar todos los endpoints, incluido el upload del archivo.

> El servicio `api` corre con `ASPNETCORE_ENVIRONMENT=Development` en el compose para que Swagger UI
> esté disponible dentro del contenedor (comodidad para la evaluación). En un entorno productivo se
> correría en `Production` y Swagger no se expondría.

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

Cola (SQS) → Worker (BackgroundService)
              ├─ descarga el archivo de S3
              ├─ lo lee por streaming y parsea según el layout del BCRA
              ├─ ingesta a tabla staging (COPY) y agrega en PostgreSQL
              ├─ persiste resultados en debtors y financial_entities (transaccional)
              ├─ actualiza el estado del ImportJob (Completed/Failed) con su resumen
              └─ registra un log estructurado con el tiempo total y el resumen de carga
```

El procesamiento no ocurre en el request HTTP: la API responde de inmediato y el trabajo pesado corre
en el Worker. Esto permite tolerar archivos grandes y reintentos sin bloquear al cliente.

---

## Decisiones de diseño

### Procesamiento por streaming + agregación en PostgreSQL

El archivo real del BCRA tiene ~34,4 millones de registros (~5,8 GB), con ~19,9 millones de
identificaciones únicas y 485 entidades (verificado midiendo el archivo provisto). Cargar todo en
memoria para agregar no es viable de forma robusta. La estrategia:

1. Leer el archivo por **streaming** (memoria acotada, sin cargarlo entero).
2. Parsear solo los campos necesarios del layout de ancho fijo.
3. Cargar los registros a una **tabla staging** mediante `COPY` (la vía más rápida de ingesta en Postgres).
4. Calcular las agregaciones con SQL (`GROUP BY`), delegando en PostgreSQL el agrupado de millones de filas.
5. Persistir los agregados en las tablas definitivas, de forma transaccional (todo o nada).

Las tablas definitivas guardan los resultados ya agregados; los endpoints de consulta no recalculan nada.
El detalle por registro no se persiste (solo vive en staging durante el procesamiento); el archivo
original queda archivado en S3 para trazabilidad/reproceso.

### Procesamiento asíncrono con SQS

`POST /upload` y el procesamiento están desacoplados mediante una cola. Esto aporta resiliencia
(reintentos, DLQ) y permite responder al cliente sin esperar. La comunicación con la cola se abstrae
mediante una interfaz en Application; la implementación usa el SDK de AWS SQS, apuntando a LocalStack en
local y pudiendo apuntar a AWS real sin cambios de código.

**Resiliencia:** si el procesamiento de un mensaje falla, no se confirma (no se borra de la cola) y SQS
lo reintenta tras el visibility timeout; luego de 3 intentos va a la Dead Letter Queue. El reproceso es
idempotente (upsert), por lo que reintentar es seguro.

### EF Core Code First

El esquema se define desde el código mediante migraciones versionadas, manteniendo el dominio
independiente de la base y haciendo el esquema reproducible (la API migra al arrancar). EF Core se usa
para el modelo y las consultas; la ingesta masiva usa `COPY` por rendimiento.

### Nomenclatura

- **Código y base de datos en inglés** (estándar de la industria); tablas y columnas en `snake_case`.
- **Rutas HTTP en español**, respetando el contrato textual del enunciado.
- **Campos JSON de las respuestas en español**, con los nombres del enunciado, mapeados mediante
  `[JsonPropertyName]` sobre DTOs. El dominio permanece en inglés.

> **Equivalencia de términos:** el campo interno `total_financing_amount` corresponde al
> `suma_total_prestamos` del enunciado. Se eligió "financing" porque el campo 7 del layout del BCRA
> abarca préstamos, otros créditos por intermediación financiera, créditos por arrendamientos
> financieros, créditos diversos y otros conceptos, no solo préstamos. El contrato JSON expone el
> nombre del enunciado.

> **Situación máxima:** el enunciado pide el "máximo valor" del campo 6. Como los códigos del BCRA no
> siguen un orden numérico de gravedad (p. ej. 21 "Riesgo Bajo" es numéricamente mayor que 05
> "Irrecuperable"), se interpreta "máxima" como la **situación de mayor gravedad crediticia**, mediante
> un ranking de severidad documentado. Se persiste y devuelve siempre el código original del BCRA.

### LocalStack

Se usa `localstack/localstack:4.14`, la última versión Community que **no requiere auth token**. Desde
marzo 2026 LocalStack unificó su imagen y las versiones posteriores exigen una cuenta y un token; usar
una versión previa mantiene el `docker-compose up` funcionando sin cuentas ni trámites externos. S3 y
SQS funcionan igual (el SDK de AWS habla la API estándar).

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

## Alcance y mejoras futuras

### Implementado

- Importador asíncrono y distribuido: `POST /upload` → S3 → SQS → Worker (`BackgroundService`).
- Lectura por streaming y parseo del archivo de ancho fijo del BCRA.
- Ingesta masiva con `COPY` y agregación en PostgreSQL (por deudor y por entidad).
- API REST de consulta (los tres endpoints del enunciado).
- Persistencia con EF Core Code First y migraciones versionadas.
- Resiliencia: reintentos vía SQS, Dead Letter Queue, reproceso idempotente.
- Notificación de finalización mediante log estructurado (tiempo total y resumen de carga).
- Contenerización completa con Docker Compose (`docker-compose up`).
- Tests unitarios (parser, dominio) y de integración (procesamiento contra PostgreSQL real).

### Mejoras futuras (fuera del alcance de esta entrega)

Priorizadas por tiempo; varias corresponden a los _bonus_ del enunciado:

- **Interfaz de administración** (frontend React + Vite).
- **CI/CD** (build, tests y análisis estático con SonarQube).
- **Observabilidad centralizada** (CloudWatch vía LocalStack) con correlación de logs entre servicios.
- **Notificaciones** por email (Mailpit) y webhook, con _retry_ y _circuit breaker_ (Polly).
- **Health checks** (`/health`) para PostgreSQL, S3 y SQS.
- **Manejo global de errores** con `ProblemDetails` y mensajes localizados.

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
