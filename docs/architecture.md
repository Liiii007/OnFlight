# OnFlight — Architecture Blueprint

> **Maintenance rule**: Always update this document in-place to reflect the latest code changes. Do not keep outdated information.

## Overview

OnFlight is a reentrant, pipeline-programmable Todo List application built with Avalonia UI (.NET 8, cross-platform). It supports nested task lists, a flow-engine with Fork/Join(Todo:Loop/Branch) control nodes, running task instances with archive lifecycle, event-driven history logging, and a floating widget for quick access.

## Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | Avalonia UI on .NET 8 (cross-platform) | net8.0 |
| Architecture | MVVM | CommunityToolkit.Mvvm 8.4.2 |
| UI Theme | Avalonia Fluent Theme (Apple-style customization) | Avalonia 11.x |
| Icons | Material.Icons.Avalonia | 3.x |
| Database | SQLite | Microsoft.Data.Sqlite 10.0.5 |
| ORM | Dapper | 2.1.72 |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.5 |
| Logging (abstractions) | Microsoft.Extensions.Logging.Abstractions | 10.0.5 |
| Logging (backend) | Serilog + Serilog.Extensions.Logging + Serilog.Sinks.File + Serilog.Sinks.Debug | 4.x / 10.0.0 / 7.0.0 / 3.0.0 |
| Serialization | System.Text.Json (built-in) | — |

## Solution Structure

```
OnFlight/
├── OnFlight.sln
├── docs/
│   ├── architecture.md          # This file
│   ├── data-and-sync.md         # Database schema & sync architecture
│   ├── sync-schema.json         # Cross-platform JSON Schema contract
│   ├── roadmap.md               # Feature roadmap
│   ├── style-guide.md           # UI style guide
│   └── running-task-multiinstance.md  # Running task multi-instance design
└── src/
    ├── OnFlight.Contracts/      # Platform-agnostic data contracts (pure C# library)
    ├── OnFlight.Core/           # Business logic, data access, services
    └── OnFlight.App/            # Avalonia UI application layer (UI + DI bootstrap)
```

## Project Dependency Graph

```
OnFlight.App ──► OnFlight.Core ──► OnFlight.Contracts
      │                                    ▲
      └────────────────────────────────────┘
```

- **Contracts** has zero external dependencies (pure POCO + enums + interfaces)
- **Core** depends on Contracts + Dapper/SQLite
- **App** depends on both Core and Contracts + Avalonia/FluentTheme/Material.Icons/MVVM/DI

---

## OnFlight.Contracts

Platform-agnostic data contract layer. Designed to be mirrored by a Swift implementation for future iOS port.

| File | Purpose |
|------|---------|
| `Enums/TodoStatus.cs` | Task lifecycle states: `Pending` (locked/not yet actionable), `Ready` (currently active/unlocked), `Done` (completed), `Skipped` (reserved for future use) |
| `Enums/FlowNodeType.cs` | Pipeline node types: `Task`, `Loop`, `Fork`, `Join`, `Branch` |
| `Enums/OperationType.cs` | Audit log operation types: `Add` (reserved), `Delete` (reserved), `Update` (reserved), `Check` (active), `Uncheck` (active), `Reorder` (reserved), `Restore` (reserved). Only `Check`/`Uncheck` are currently logged. |
| `Enums/RunningState.cs` | Running instance lifecycle: `Running` (in progress), `AllDone` (all tasks completed) |
| `Models/TodoItemDto.cs` | DTO for a single todo item with JSON serialization attributes |
| `Models/TodoListDto.cs` | DTO for a todo list (contains a list of `TodoItemDto`) |

| `Models/OperationLogDto.cs` | DTO for an operation audit log entry |
| `Models/RunningInstanceDto.cs` | DTO for a running task instance (items with status, fork/sub-task context) |
| `Sync/ISyncProvider.cs` | Interface defining `Push`/`Pull`/`ResolveConflict` operations |
| `Sync/SyncPayload.cs` | Data packet carrying change sets for sync |
| `Sync/SyncResult.cs` | Result of a sync operation (success/error/count) |
| `Sync/ConflictResolution.cs` | Conflict strategy enum + `SyncConflict` model |
| `Schema/SchemaVersion.cs` | Database schema version constant (`Current = 1`) |

---

## OnFlight.Core

Core business logic layer. No UI dependencies.

### Models (`Models/`)

Domain models with sync metadata fields (`UpdatedAt`, `DeviceId`, `IsDeleted`) and navigation properties.

| File | Purpose |
|------|---------|
| `TodoItem.cs` | Core entity: task with title, status, sort order, node type, flow config, and optional sub-list reference |
| `TodoList.cs` | Container for TodoItems; can be nested via `ParentItemId` |

| `OperationLog.cs` | Audit trail entry for task state changes |
| `RunningInstance.cs` | Running task instance entity with JSON-serialized state |

### Mapping (`Mapping/`)

| File | Purpose |
|------|---------|
| `DtoMapper.cs` | Extension methods for bidirectional Domain ↔ DTO mapping |

### Data (`Data/`)

| File | Purpose |
|------|---------|
| `DatabaseInitializer.cs` | Creates all SQLite tables, indexes, and initializes schema version on first run |
| `DbConnectionFactory.cs` | Factory producing open `IDbConnection` instances from connection string |
| `DapperTypeHandlers.cs` | Custom Dapper type handlers for `Guid`, `Guid?`, `DateTime` ↔ SQLite TEXT mapping |

### Repositories (`Data/Repositories/`)

Each repository follows the interface-segregation pattern (I + implementation).

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `ITodoListRepository` | `TodoListRepository` | CRUD for `todo_lists` table |
| `ITodoItemRepository` | `TodoItemRepository` | CRUD for `todo_items` table, with sort-order management |

| `IOperationLogRepository` | `OperationLogRepository` | Insert/query/search for `operation_logs` table |
| `IRunningInstanceRepository` | `RunningInstanceRepository` | CRUD for running task instances (JSON-serialized to SQLite) |

### Services (`Services/`)

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `ITodoService` | `TodoService` | High-level task CRUD: add/delete/update items, manage sub-lists, reorder, toggle status. Pure data operations, no logging. |
| `ILogService` | `LogService` | Record and query operation audit logs with keyword/time-range search. |
| `IRunningTaskService` | `RunningTaskService` | Create/load/save/delete running task instances. Flattens list tree (including Fork children and sub-task children) into a single item list per instance. |

| — | `NoOpSyncProvider` | Placeholder `ISyncProvider` implementation (all methods return success/empty). |

---

## OnFlight.App

Avalonia UI application layer. Bootstraps DI, assembles the UI.

### Bootstrap

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point: `AppBuilder.Configure<App>().UsePlatformDetect()` |
| `App.axaml` | Avalonia resources: FluentTheme (follows system Light/Dark), custom `Theme.axaml`, converter instances |
| `App.axaml.cs` | DI container setup: configures Serilog file logging + global exception handlers, registers all repositories, services, ViewModels, and `TaskEventBus`. Initializes database on startup. Sets `ShutdownMode.OnExplicitShutdown` and manages background-resident lifecycle via `TrayIcon` (system tray icon with left-click restore and right-click NativeMenu: Show Main Window / New Floating Window / Exit). Exposes `ShowMainWindow()`, `OpenNewFloatingWindow()`, `QuitApplication()` (disposes tray via try/finally before `desktop.Shutdown()`), and `IsApplicationQuitting` flag. |

### ViewModels (`ViewModels/`)

| File | Purpose |
|------|---------|
| `MainViewModel.cs` | Central VM: manages root list selection/creation/deletion, item CRUD, breadcrumb navigation into sub-lists, progress tracking. Recursively expands Fork target lists and SubTask children inline with depth-based indentation via `ExpandChildrenRecursiveAsync` / `ExpandChildrenRecursiveWithStatusAsync` / `ExpandChildrenRecursiveDraftAsync`. Subscribes to `TaskEventBus` for History refresh and Running instance sync. |
| `TodoItemViewModel.cs` | Per-item VM wrapping a `TodoItem` for list binding. Exposes computed `IsDone`, `NodeTypeIcon`, lock/transition states. Carries `SourceItemId` to maintain traceability to the original source item when used inside running instances. |
| `ItemConfigViewModel.cs` | Right-panel config VM: edit title/description/status/node-type. Fork config picks a target list; Join config picks a Fork item. Auto-saves to DB with 500ms debounce on any property change. |
| `HistoryViewModel.cs` | Loads operation logs for the current list from `ILogService`. Refreshed automatically via `TaskEventBus` events. |
| `FloatingViewModel.cs` | VM for the floating widget: manages instance carousel (prev/next), creates new runs, delegates toggle to shared `RunningTaskInstanceViewModel`. |
| `RunningTaskInstanceViewModel.cs` | Per-instance VM: manages a flattened item list with Fork/Join barriers, sequential lock enforcement, pagination for floating window, archive countdown. Uses `SourceItemId` for DTO round-trip to preserve original item references. Publishes `TaskChecked`/`TaskUnchecked`/`RunArchived` events via `TaskEventBus`. |
| `RunningTaskManager.cs` | Manages the collection of active and archived running instances. Creates/removes instances, handles archive lifecycle. |
| `TaskEventBus.cs` | Singleton event bus for unified task state change handling. See [Event Bus Architecture](#event-bus-architecture). |
| `BreadcrumbItem` (in MainViewModel.cs) | Simple model for breadcrumb navigation path. |
| `RootListItem` (in MainViewModel.cs) | Simple model for the root-list selector dropdown. |

### Views (`Views/`)

| File | Purpose |
|------|---------|
| `MainWindow.axaml` | Primary window (Apple-style): five-column layout — left sidebar (list selector), splitter, center (breadcrumbs + progress + add-item + item list with drag-reorder), splitter, right panel (tabbed Config/Running/History). Title bar with floating-window and run buttons. |
| `MainWindow.axaml.cs` | Code-behind: resolves MainViewModel from DI, handles drag-reorder logic, Enter-key shortcuts, inline rename, running card click-to-attach, opens FloatingWindow. Implements close-to-tray behavior: `OnCloseClick` hides on normal close, calls `QuitApplication()` on Shift+close; `OnMainWindowClosing` cancels system close (Alt+F4) and hides unless `IsApplicationQuitting`. Tracks Shift state via KeyDown/KeyUp + Deactivated reset. |
| `FloatingWindow.axaml` | Borderless, transparent, topmost floating widget with Apple-style rounded card. Instance carousel with task list, checkboxes, pagination, archive countdown. |
| `FloatingWindow.axaml.cs` | Code-behind: `BeginMoveDrag` on header pointer-pressed, double-click to return to main window, close. `Activated` handler calls `LoadAvailableListsAsync()` (with try/catch + Serilog error logging) to refresh the available lists dropdown when the user switches back to a floating window. |

### Converters (`Converters/`)

| File | Purpose |
|------|---------|
| `BoolToVisibilityConverter.cs` | `bool`/`int`/`string` → `bool` for `IsVisible`, supports `Invert` parameter |
| `BoolToOpacityConverter.cs` | `bool` → `double` opacity value |
| `StatusToBrushConverter.cs` | `TodoStatus` → colored `SolidColorBrush` |
| `NodeTypeToIconConverter.cs` | `FlowNodeType` → `MaterialIconKind` enum |
| `DepthToMarginConverter.cs` | `int` depth → left `Thickness` for tree indentation |
| `StatusToStrikethroughConverter.cs` | `TodoStatus.Done` → Avalonia `TextDecorationCollection` |
| `HexToBrushConverter.cs` | Hex color string → `SolidColorBrush` via `Color.Parse()` |
| `DoneToBrushConverter.cs` | `bool` IsDone → check circle color brush |
| `DoneToTextBrushConverter.cs` | `bool` IsDone → text foreground (gray when done) |
| `DoneToIconKindConverter.cs` | `bool` IsDone → `MaterialIconKind` (circle outline / check circle) |

### Styles (`Styles/`)

| File | Purpose |
|------|---------|
| `Theme.axaml` | Apple-style design tokens (Light/Dark theme dictionaries, accent colors, corner radii) + custom styles: `Card`, `SectionHeader`, `Breadcrumb`, `Apple` ProgressBar, `ListItem` with hover transition, `IconBtn`, `PlainIconBtn`, `CheckBtn`, `Fab`, `FloatingBall`, `WinBtn`/`WinClose`, TextBox/ComboBox theming, inline-rename underline |

---

## Key Design Decisions

### Event Bus Architecture

All task state changes flow through a single `TaskEventBus` (Singleton), ensuring consistent behavior regardless of which window initiates the action.

```
┌──────────────────┐     ┌──────────────────┐
│   MainWindow     │     │  FloatingWindow  │
│  (MainViewModel) │     │(FloatingViewModel)│
└───────┬──────────┘     └───────┬──────────┘
        │                        │
        │  toggle / archive      │  toggle / archive
        ▼                        ▼
┌─────────────────────────────────────────┐
│       RunningTaskInstanceViewModel      │
│  (shared instance — same object ref)    │
└───────────────────┬─────────────────────┘
                    │
                    │ PublishAsync(TaskEvent)
                    ▼
          ┌─────────────────┐
          │  TaskEventBus   │  (Singleton)
          │                 │
          │  1. Write log   │──► ILogService ──► operation_logs DB
          │  2. Notify subs │
          └────────┬────────┘
                   │ EventPublished
          ┌────────┴────────┐
          ▼                 ▼
   MainViewModel      (future subscribers)
   └─► Refresh History
   └─► Sync Running UI
```

**Event types:**

| `TaskEventKind` | Trigger | Logged as |
|-----------------|---------|-----------|
| `TaskChecked` | Any task toggled to Done (from any window) | `OperationType.Check` |
| `TaskUnchecked` | Any task toggled back to Pending | `OperationType.Uncheck` |
| `RunArchived` | All tasks in a running instance completed + 5s countdown elapsed | `OperationType.Check` |

**Key invariant:** Only Check/Uncheck/Archive events are logged. CRUD operations (Add, Delete, Update, Reorder) do not produce history entries.

### Running Task Instances

A "Run" is a snapshot-like copy of a task list's items at creation time, stored as a JSON blob in the `running_instances` table. Each run has its own independent status tracking.

- **Sequential execution**: By default, tasks are locked in order. Fork/Join nodes act as barriers.
- **AllowOutOfOrder mode**: Unlocks all tasks for free-form completion.
- **Archive lifecycle**: When all tasks are done → 5s countdown → auto-archive (movable to archived list). User can UNDO during countdown.
- **Shared instances**: `RunningTaskManager` holds all instances as Singletons. Both MainWindow and FloatingWindow reference the same `RunningTaskInstanceViewModel` objects — changes from either window are immediately visible to the other.
- **Unique Item Ids**: When `RunningTaskService.ExpandItemsAsync` flattens fork/sub-task children, each expanded item receives a new `Guid.NewGuid()` as its `Id`, while `SourceItemId` preserves the original item reference. This prevents duplicate Id collisions when the same target list is referenced by multiple Fork nodes. Top-level items (not fork/sub-task children) retain their original Id.
- **Status mapping with composite key**: `MainViewModel.SyncFromRunningInstanceAsync` builds a status map keyed by `(SourceItemId, ParentForkTitle)` to correctly distinguish the same source item appearing under different Fork branches. `ExpandChildrenRecursiveWithStatusAsync` uses the same composite key for lookup.
- **Recursive inline expansion**: `MainViewModel` recursively expands Fork target lists and SubTask children in the main content view. Each nesting level increments `depth`, which the `DepthToMarginConverter` translates to `depth * 24px` left indentation. The UI shows `↳ Fork:` hints for fork children and `↳ Sub:` hints for subtask children.

### Pipeline Node Types

Each TodoItem has a `NodeType` field controlling its behavior in the flow engine:

| Type | Behavior |
|------|----------|
| **Task** | Normal todo item. Executed sequentially. |
| **Loop** | Repeats its sub-list N times. Config: `{"loopCount": N}` |
| **Fork** | Dispatches to a **target task list** (not a sub-list). Config: `{"targetListId": "<guid>"}`. The target list is recursively expanded (nested Forks supported); each leaf item is treated as a parallel branch. |
| **Join** | Waits for **all preceding Forks at the same depth** and their children to complete. Auto-computed status. A single Join can barrier multiple same-depth Forks (N:1 relationship). |
| **Branch** | Conditional execution within a Fork. Config: `{"condition": "var op value"}` |

### Fork/Join Recursive Barrier Mechanism

Fork/Join uses a depth-based recursive barrier model:

```
[Task A] → [Task B] → Fork F1 → [child 1] [child 2] → Join J1 → [Task C]
                        ↑                                 ↑
                  A,B done → unlock children      all children done → unlock C
```

**Core rules:**

- **Depth matching**: A Join only matches Fork nodes at the **same depth**. Inner Fork/Join pairs do not bleed into outer levels.
- **N:1 relationship**: A single Join barriers **all same-depth Forks** before it. Each Fork's children range extends to the next same-depth Fork. The Join is Done only when every Fork is Done.
- **Recursive expansion**: If a Fork's target list contains nested Fork/Join nodes, they are expanded at depth+1 and form independent barriers.
- **DAG cycle detection**: When configuring a Fork's `targetListId`, `WouldCreateCycleAsync` recursively checks for cycles. If a cycle is detected, the change is rejected and a warning dialog is shown.

**Nested Fork example:**

```
depth=0: [Task X] → Fork F1 ──────────────────────────────── Join J1 → [Task C]
                        │                                       ↑
depth=1:          [Task Y] → Fork F2 ─────── Join J2 → [Task Z]
                                 │              ↑
depth=2:                   [Task W1] [Task W2]
```

- J2 only joins F2 (same depth=1), does not affect F1
- J1 only joins F1 (same depth=0), waits for all F1 children (including the full F2/J2 subtree)

**Auto-computed status:**

- Fork status = whether all items in its children range are Done/Skipped
- Join status = whether all same-depth preceding Forks are Done

**Lock logic (`UpdateLockStatesForRange`):**

1. On encountering a Fork, collect all same-depth Forks up to the Join
2. Check whether all same-level Tasks before the first Fork are done (`AllPriorTasksDone`)
3. Not done → all Fork children are locked
4. Done → each Fork's children range is recursively locked independently
5. Content after Join depends on Join's completion status

### Condition Evaluation

Simple `variable operator value` expressions:
- Numeric: `>`, `<`, `>=`, `<=`, `==`, `!=`
- String: `==`, `!=`
- Variables come from flow engine runtime context (e.g. `loopIndex`)

### DI Registration

```
Singleton:  DbConnectionFactory, all Repositories, all Services,
            TaskEventBus, RunningTaskManager, MainViewModel
Transient:  FloatingViewModel (one per floating window instance)
```

`TaskEventBus` is registered as Singleton so all ViewModels share the same event bus. `RunningTaskManager` is Singleton to ensure a single source of truth for running instances across all windows.

### Logging Architecture

The application uses **Serilog** as the logging backend, bridged to `Microsoft.Extensions.Logging` (`ILogger<T>`) so all components use the standard abstraction.

**Configuration** (in `App.axaml.cs`):

- **File sink**: Rolling daily log files at `%LOCALAPPDATA%/OnFlight/logs/onflight-{date}.log`, retained for 7 days.
- **Debug sink**: Writes to `System.Diagnostics.Debug` (visible in VS Output window during development).
- **Minimum level**: `Information` globally, `Warning` for `Microsoft.*` namespaces.
- **Global exception handlers**: `AppDomain.UnhandledException` (Fatal) and `TaskScheduler.UnobservedTaskException` (Error) are captured. `Log.CloseAndFlush()` is called on application shutdown.

**Logging coverage**:

| Component | Logger | Key log points |
|-----------|--------|----------------|
| `TodoService` | `ILogger<TodoService>` | Info: list/item CRUD. Error: transaction rollback on delete/create-sublist failures. |
| `RunningTaskService` | `ILogger<RunningTaskService>` | Info: instance create/delete. Warning: FlowConfig parse failures. |
| `TaskEventBus` | `ILogger<TaskEventBus>` | Info: event publishing. Error: event handler failures. |
| `DatabaseInitializer` | `ILogger<DatabaseInitializer>` | Info: DB initialized, data cleared. |
| `SettingsService` | `ILogger<SettingsService>` | Info: settings saved. Warning: settings load failure. |
| `MainViewModel` | `ILogger<MainViewModel>` | Warning: targetListId parse failure. |
| `ItemConfigViewModel` | `ILogger<ItemConfigViewModel>` | Warning: FlowConfig parse failure. Error: auto-save failure. |
| `FloatingViewModel` | `ILogger<FloatingViewModel>` | (Reserved for future use) |

### Background Resident & System Tray

The application stays running in the background when the user closes the main window, using a system tray icon to manage visibility and exit.

**Lifecycle:**

```
App Start → MainWindow + TrayIcon visible
         ↓
Close (no Shift) → MainWindow.Hide() → process stays alive, TrayIcon remains
         ↓
Tray left-click → MainWindow.Show() + Activate() + restore from Minimized
Tray menu "New Floating Window" → new FloatingWindow instance (multi-instance)
Tray menu "Exit" / Shift+Close → QuitApplication()
         ↓
QuitApplication → _isQuitting=true → try{Remove from TrayIcons} finally{Dispose _trayIcon} → desktop.Shutdown()
```

**Key implementation points:**

- `ShutdownMode.OnExplicitShutdown` prevents automatic process exit when the last window is hidden.
- `_isQuitting` flag coordinates `MainWindow.Closing` handler: when false, cancels close and hides; when true, allows the close to proceed.
- Close button uses `Click` event (not `PointerPressed`, which `Button` swallows internally). Shift state is tracked via `KeyDown`/`KeyUp` + `Deactivated` reset.
- `TrayIcon` is created in code-behind (`EnsureTrayIcon`), not XAML, with icon loaded from `avares://OnFlight.App/Assets/tray.png`.
- Tray menu items use English text: "Show Main Window", "New Floating Window", "Exit".
- FloatingWindow refreshes its available lists dropdown on `Activated` to stay in sync after changes in MainWindow.
