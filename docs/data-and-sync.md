# OnFlight — Database & Sync Architecture

## Database

### Engine

SQLite via `Microsoft.Data.Sqlite` + `Dapper`. Database file located at:

```
%LOCALAPPDATA%/OnFlight/onflight.db
```

### Schema Version

Table `schema_version` stores a single integer. Current version: **1**. Managed by `SchemaVersion.cs` constant + `DatabaseInitializer`.

### Type Mapping

SQLite has no native `Guid` or `DateTime` types. All are stored as `TEXT`:

| C# Type | SQLite Type | Format | Dapper Handler |
|---------|-------------|--------|----------------|
| `Guid` | TEXT | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` | `GuidTypeHandler` |
| `Guid?` | TEXT (nullable) | same or NULL | `NullableGuidTypeHandler` |
| `DateTime` | TEXT | ISO 8601 (`o` format) | `DateTimeTypeHandler` |
| `enum` | INTEGER | numeric value | built-in |
| `bool` | INTEGER | 0 / 1 | built-in |

Handlers are registered at startup via `DapperConfig.RegisterTypeHandlers()`.

---

### Tables

#### `todo_lists`

| Column | Type | Constraint | Description |
|--------|------|-----------|-------------|
| Id | TEXT | PK | UUID |
| Name | TEXT | NOT NULL | Display name |
| ParentItemId | TEXT | nullable | If set, this list is a sub-list owned by a TodoItem |
| CreatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| UpdatedAt | TEXT | NOT NULL | ISO 8601 UTC, updated on every mutation |
| DeviceId | TEXT | NOT NULL | Originating device identifier |
| IsDeleted | INTEGER | NOT NULL, default 0 | Soft delete flag |

#### `todo_items`

| Column | Type | Constraint | Description |
|--------|------|-----------|-------------|
| Id | TEXT | PK | UUID |
| Title | TEXT | NOT NULL | Task title |
| Description | TEXT | nullable | Optional description |
| Status | INTEGER | NOT NULL, default 0 | `TodoStatus` enum value |
| SortOrder | INTEGER | NOT NULL, default 0 | Position within parent list |
| ParentListId | TEXT | NOT NULL, FK→todo_lists | Owning list |
| SubListId | TEXT | nullable | Optional nested sub-list |
| NodeType | INTEGER | NOT NULL, default 0 | `FlowNodeType` enum value |
| FlowConfigJson | TEXT | nullable | JSON config for Loop/Fork/Join/Branch |
| CreatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| UpdatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| DeviceId | TEXT | NOT NULL | Originating device |
| IsDeleted | INTEGER | NOT NULL, default 0 | Soft delete flag |

**FlowConfigJson examples:**

```json
// Loop
{"loopCount": 3}

// Fork — references a target task list
{"targetListId": "a1b2c3d4-..."}

// Join — references a Fork item in the same list
{"forkItemId": "e5f6g7h8-..."}

// Branch — conditional execution
{"condition": "count > 3"}
```

#### `operation_logs`

| Column | Type | Constraint | Description |
|--------|------|-----------|-------------|
| Id | TEXT | PK | UUID |
| ListId | TEXT | NOT NULL, FK→todo_lists | Which list was affected |
| OperationType | INTEGER | NOT NULL | `OperationType` enum value |
| Detail | TEXT | nullable | Human-readable description |
| Timestamp | TEXT | NOT NULL | ISO 8601 UTC |
| DeviceId | TEXT | NOT NULL | Originating device |

#### `running_instances`

| Column | Type | Constraint | Description |
|--------|------|-----------|-------------|
| Id | TEXT | PK | UUID |
| SourceListId | TEXT | NOT NULL | Source list this run was created from |
| ListName | TEXT | NOT NULL | Display name at creation time |
| StateJson | TEXT | NOT NULL | Full serialized `RunningInstanceDto` JSON |
| CreatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| UpdatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| DeviceId | TEXT | NOT NULL | Originating device |

### Indexes

```sql
idx_todo_items_parent   ON todo_items(ParentListId)
idx_todo_items_sort     ON todo_items(ParentListId, SortOrder)
idx_operation_logs_list ON operation_logs(ListId)
idx_operation_logs_time ON operation_logs(Timestamp)
idx_running_instances_source ON running_instances(SourceListId)
```

### ER Diagram

```
todo_lists 1──*  todo_items
todo_items 1──?  todo_lists       (SubListId → sub-list)
todo_lists 1──*  operation_logs
todo_lists 1──*  running_instances  (SourceListId)
```

---

## Cross-Platform Sync Architecture

### Design Principles

1. **Contract-first**: `OnFlight.Contracts` defines all DTOs and enums. Swift side implements the same structures following `docs/sync-schema.json`.
2. **Sync-ready metadata**: Every entity carries `UpdatedAt` (UTC ISO 8601) + `DeviceId` for future last-write-wins or CRDT strategies.
3. **Soft delete**: `IsDeleted` flag instead of physical deletion, ensuring delete operations propagate correctly across devices.
4. **UUID primary keys**: `Guid` (UUID v4) for all entity IDs — no collision risk across distributed devices.

### ISyncProvider Interface

```csharp
public interface ISyncProvider
{
    Task<SyncResult> PushChangesAsync(SyncPayload payload, CancellationToken ct = default);
    Task<SyncPayload> PullChangesAsync(DateTime since, CancellationToken ct = default);
    Task<ConflictResolution> ResolveConflictAsync(SyncConflict conflict, CancellationToken ct = default);
}
```

### Current State

Currently using `NoOpSyncProvider` — all methods return success/empty. This is a placeholder for future sync backends.

### Planned Sync Backends

| Backend | Platform | Notes |
|---------|----------|-------|
| CloudKit | iOS | Native Apple sync, good for Apple ecosystem |
| Custom REST/gRPC server | All | Self-hosted, full control |
| P2P direct | LAN | No server dependency |

### Sync Flow

```
Device A                          Server / Peer                      Device B
   │                                  │                                 │
   ├── PushChanges(payload) ────────► │                                 │
   │   (SyncPayload with              │                                 │
   │    changed Lists/Items/Nodes)     │                                 │
   │                                  │ ◄──── PullChanges(since) ───────┤
   │                                  │ ────► SyncPayload ──────────────┤
   │                                  │                                 │
   │   ◄── conflict detected ────────│                                 │
   ├── ResolveConflict() ───────────► │                                 │
   │                                  │                                 │
```

### Conflict Resolution Strategies

| Strategy | Description |
|----------|-------------|
| `LocalWins` | Keep local version |
| `RemoteWins` | Accept remote version |
| `LastWriteWins` | Compare `UpdatedAt`, newest wins |
| `Manual` | Present both versions to user for manual merge |

### JSON Schema Contract

`docs/sync-schema.json` defines the canonical wire format for all entities. Key conventions:

- Enum values serialized as **strings** in JSON (e.g. `"Pending"`, `"Task"`) for cross-language readability
- Enum values stored as **integers** in SQLite for efficiency
- All timestamps in **ISO 8601** format
- All IDs in **UUID** format

Both C# and future Swift implementations must conform to this schema to ensure interoperability.

### SQLite Schema Parity

The exact same `CREATE TABLE` SQL is intended to be used on both platforms. `SchemaVersion` controls migrations — both C# `DatabaseInitializer` and the future Swift equivalent check and apply the same version sequence.
