# Wayni BCRA Challenge

Solución para el procesamiento del padrón de deudores del BCRA (Banco Central de la República
Argentina). Lee el archivo `deudores.txt` de registros de ancho fijo, calcula agregaciones por
deudor y por entidad financiera, las persiste en PostgreSQL y las expone mediante una API REST.
El procesamiento es asíncrono, desacoplado mediante una cola de mensajes.

> **Estado del proyecto:** en desarrollo. Las secciones marcadas con _(en construcción)_ se
> completan a medida que avanza la implementación. El historial de commits refleja esa evolución.

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
| Notificaciones | Log estructurado; email (Mailpit) y webhook como extensiones |
| Frontend | React + Vite _(planificado)_ |
| Testing | xUnit |
| Infraestructura | Docker / Docker Compose |

---

## Arquitectura

El proyecto sigue Clean Architecture con dependencias apuntando hacia el dominio (las capas
externas conocen a las internas, nunca al revés).

```
src/
├── WayniBcra.Domain          # Entidades, enums y reglas de dominio. Sin dependencias externas.
├── WayniBcra.Application      # Casos de uso y puertos (interfaces). Depende solo de Domain.
├── WayniBcra.Infrastructure   # Implementaciones: EF Core, Npgsql/COPY, S3, SQS. Depende de Application.
├── WayniBcra.Api              # Host HTTP. Controllers y DTOs. Depende de Application + Infrastructure.
└── WayniBcra.Worker           # BackgroundService que consume la cola. Depende de Application + Infrastructure.
```

### Flujo de procesamiento (asíncrono)

```
Cliente → POST /upload → API
                          ├─ persiste el archivo (S3)
                          ├─ crea un ImportJob (estado Pending)
                          ├─ publica un mensaje en la cola (SQS)
                          └─ responde 202 Accepted

Cola (SQS) → Worker (BackgroundService)
              ├─ lee el archivo por streaming
              ├─ parsea los campos según el layout del BCRA
              ├─ ingesta a tabla staging (COPY) y agrega en PostgreSQL
              ├─ persiste resultados en debtors y financial_entities
              ├─ actualiza el estado del ImportJob
              └─ notifica la finalización
```

El procesamiento no ocurre en el request HTTP: la API responde de inmediato y el trabajo pesado
corre en el Worker. Esto permite tolerar archivos grandes y reintentos sin bloquear al cliente.

---

## Decisiones de diseño

Estas son las decisiones relevantes y su justificación.

### Procesamiento por streaming + agregación en PostgreSQL

El archivo real del BCRA tiene ~34,4 millones de registros (~5,8 GB) con ~19,9 millones de
identificaciones únicas y 485 entidades. Cargar todo en memoria para agregar no es viable de forma
robusta. La estrategia elegida:

1. Leer el archivo por **streaming** (sin cargarlo entero en memoria).
2. Parsear solo los campos necesarios según el layout de ancho fijo.
3. Cargar los registros a una **tabla staging** mediante `COPY` (la vía más eficiente de ingesta en
   PostgreSQL).
4. Calcular las agregaciones con SQL (`GROUP BY` + `MAX` / `SUM`), delegando en PostgreSQL el trabajo
   pesado de agrupar millones de filas (que resuelve con spill a disco de forma robusta).
5. Persistir los resultados en las tablas definitivas, de forma transaccional (todo o nada).

Las tablas definitivas almacenan los resultados ya agregados, de modo que los endpoints de consulta
no recalculan nada.

### Procesamiento asíncrono con SQS

La carga (`POST /upload`) y el procesamiento están desacoplados mediante una cola. Esto aporta
resiliencia (reintentos, tolerancia a fallos) y permite responder al cliente sin esperar a que
termine el procesamiento. La comunicación con la cola se abstrae mediante un puerto en la capa
Application; la implementación concreta usa el SDK de AWS SQS, apuntando a LocalStack en local y
pudiendo apuntar a AWS real sin cambios de código.

### EF Core Code First

El esquema se define desde el código mediante migraciones versionadas, lo que mantiene el dominio
independiente de la base y hace el esquema reproducible (`docker-compose up` levanta y migra). EF Core
se usa para el modelo y las consultas; la ingesta masiva usa `COPY` por rendimiento.

### Nomenclatura

- **Código y base de datos en inglés** (estándar de la industria); tablas y columnas en `snake_case`.
- **Rutas HTTP en español**, respetando textualmente el contrato del enunciado.
- **Campos JSON de las respuestas en español**, con los nombres del enunciado, mapeados mediante
  `[JsonPropertyName]` sobre DTOs. El dominio permanece en inglés.

> **Equivalencia de términos:** el campo `total_financing_amount` (interno) corresponde al
> `suma_total_prestamos` del enunciado. Se eligió "financing" porque el campo 7 del layout del BCRA
> abarca préstamos, otros créditos por intermediación financiera, créditos por arrendamientos
> financieros, créditos diversos y otros conceptos, no solo préstamos. El contrato JSON expone el
> nombre del enunciado.

> **Situación máxima:** se calcula como el máximo numérico del código de situación (campo 6), tal como
> indica el enunciado. Los códigos del BCRA no siguen estrictamente el orden de gravedad, por lo que
> no se reinterpretan: se respeta el valor literal.

---

## Endpoints

Rutas según el enunciado (en español). _(En construcción: ejemplos de request/response se agregan al
implementar cada endpoint.)_

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/upload` | Carga el archivo del BCRA e inicia el procesamiento asíncrono. |
| `GET` | `/deudores/{cuit}` | Devuelve el deudor por número de identificación. |
| `GET` | `/entidades/{codigo}` | Devuelve la entidad financiera por código. |
| `GET` | `/deudores/top/{n}` | Devuelve los N deudores con mayor suma de financiación. |

---

## Cómo ejecutar

_(En construcción.)_ El sistema se levantará con Docker Compose (`docker-compose up`), incluyendo API,
Worker, PostgreSQL y LocalStack. Las credenciales y configuraciones se externalizan mediante un
archivo `.env` (se provee `.env.example` como plantilla).

---

## Testing

_(En construcción.)_ Pruebas unitarias con xUnit, enfocadas en el parser del archivo y la lógica de
agregación.

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

Los importes están expresados en miles de pesos con un decimal. La especificación completa del layout
se encuentra en la documentación oficial del BCRA.
