# read-database-with-mongodb-change-streams

A working demo of the **read-database pattern** where a source-of-truth lives in
MongoDB and a denormalized, query-optimized read model is maintained in
Elasticsearch in near real-time via **MongoDB Change Streams**.

The repo splits the demo across four small .NET 10 services so you can see the
full picture: three write-source services own their respective collections,
and one listener subscribes to the `Orders` collection's change stream,
enriches every change by fanning out to the other services, and writes the
result into Elasticsearch.

The goal is to make the pattern tangible, including the parts that bite in production: resume-token persistence, oplog-rotation recovery, poison-message isolation, snapshot replay for cold-start / catch-up, fan-out enrichment with caching, and partial vs. full updates on the read model.

---

## What it demonstrates

- **MongoDB Change Streams** as the integration mechanism between an
  operational store and a derived read model. `MongoChangeStreamService<TEntity>`
  is the generic core, Insert / Update / Replace / Delete events are routed
  to a per-entity handler.
- **Read/write separation**: writes go to the source service
  (`Services.Orders`), reads are served from a separately maintained
  Elasticsearch index (`order-details`).
- **Fan-out enrichment**: every change is augmented with customer and
  product data fetched from sibling services in parallel, with per-id
  `IMemoryCache` entries (24h TTL) so the snapshot path does not re-fetch
  the same record for every batch.
- **Resume token persistence** so the listener can crash, restart, and pick
  up exactly where it left off. The token is saved **only after** the
  handler completes successfully; failures re-throw and the hosted-service
  restart loop replays from the last good token.
- **Oplog-rotation recovery**: when the saved resume token is too old to
  resume from, the listener clears it and continues from the current time
  instead of crashing.
- **Proactive token refresh** during idle periods so the cursor does not
  fall off the oplog window. Configurable via
  `resumeTokenRefreshIntervalHours` / `maxResumeTokenAgeHours`.
- **Poison-message handling**: a single bad document cannot block the
  pipeline forever. Data/logic failures count toward
  `maxConsecutiveFailuresPerDocument`; infrastructure errors (Mongo
  connection / timeout / cancellation) retry indefinitely without counting.
- **Snapshot replay**: `POST /init-snapshot` re-emits the entire `Orders`
  collection through the same enrichment pipeline, for initial load and
  recovery. Paginates with a composite `(CreatedAt, Id)` cursor, because
  single-key GUID cursors are non-sequential and `CreatedAt` alone has
  collisions; the composite avoids both.
- **Field-level partial updates**: when the change-stream event reports
  only `Status` changed, the listener sends an Elasticsearch `_update` with
  a `doc` payload containing just that field. Full replace is reserved for
  `Replace` events and for the case where the document does not yet exist
  on the read side. `OrderFieldMappings` is the source-of-truth for which
  Order fields propagate.

---

## Prerequisites

- .NET 10 SDK.
- Docker (for MongoDB + Elasticsearch).
- Optional: Postman for hitting the endpoints visually.

---

## Quick start

```bash
# 1. Bring up Mongo (single-node replica set) + Elasticsearch.
docker compose up -d

# 2. Start all four .NET services in Release mode, logs streamed into one terminal.
bash .scripts/start_all.sh

# 3. From a second terminal, seed the customer and product catalogs.
#    These use fixed UUIDs and are idempotent, so they are safe to re-run.
bash .scripts/seed_customers.sh
bash .scripts/seed_products.sh

# 4. Generate 10 random orders. Each one triggers an insert through
#    Services.Orders → MongoDB change stream → listener → Elasticsearch.
bash .scripts/generate_orders.sh

# 5. Verify the read model. (Elastichsearch Index)
curl http://localhost:9200/order-details/_count
curl 'http://localhost:9200/order-details/_search?pretty&size=3'
```

---

## Running modes

### 1. Scripted

`.scripts/start_all.sh` runs every service with
`dotnet run -c Release`

```
[customers ] Now listening on: http://localhost:5101
[products  ] Now listening on: http://localhost:5102
[orders    ] Now listening on: http://localhost:5103
[listener  ] Starting MongoDB Change Stream Service for collection Orders.
```

### 2. VS Code (Debug)

`.vscode/launch.json` has individual launch configs for each service plus
a **compound launch called "All"** that starts all four with the debugger
attached. Hit F5, pick "All", and you can set breakpoints in every service
simultaneously.

```bash
cd src/Services.Orders
dotnet run                          # uses launchSettings.json → :5103, Development
```

---

## Test scripts

All under `.scripts/`. Every script pauses with `Press Enter to close...`
when run interactively, so a failure is visible even if you launched it by
double-click.

### `seed_customers.sh`

Idempotently upserts five customers via `PUT /customers/{id}`. The UUIDs
are fixed (`11111111-…-101` through `…-105`) so subsequent scripts can
reference them.

### `seed_products.sh`

Same idea for products: 20 fixed UUIDs (`22222222-…-201` through `…-220`)
and a plausible product catalog (mice, monitors, drones, …).

### `generate_orders.sh`

Creates **10 random orders per run** (override with `ORDER_COUNT=50 …`).
Each order picks:

- a random customer from the five seeded ids,
- 1 to 4 random products (sampled without replacement),
- quantity 1–5 per item,
- a random payment method and currency.

The order id is left for the server to generate, so every run produces
fresh orders. The `Services.Orders` POST handler fetches each referenced
product's price from `Services.Products` and computes the totals; the
client only sends `{productId, quantity}`. After the script exits, the
listener will have already processed every insert and written enriched
`OrderDetail` documents into Elasticsearch.

**Run order matters**: seed scripts must run before `generate_orders.sh`,
otherwise the order service rejects unknown product ids with a 400.

### `start_all.sh` / `stop_all.sh`

`start_all` launches all four services in Release mode and streams logs.
`stop_all` finds and kills whatever is listening on ports 5100–5103. It
does not rely on PID files, so it cleans up zombies left by VS Code
launches, terminal-window closes, etc.

---

## Poking at endpoints

A Postman collection sits at
`.postman/read-database-with-mongodb-change-streams.postman_collection.json`. Import
it into Postman and you get one example request per endpoint across all
four services plus two Elasticsearch convenience queries. The collection
ships with collection-scoped variables for the seeded customer and product
UUIDs so the create / update / delete examples work out of the box.