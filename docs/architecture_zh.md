# OnFlight — 架构蓝图

> **维护规则**：将最新修改直接更新到本文档中，不保留过往的过时信息。

## 概述

OnFlight 是一款支持重入、可编程执行管线的TODO List应用，Windows版本基于 Avalonia UI构建。支持嵌套任务列表、带有 Fork/Join（待添加：Loop/Branch）控制节点的流程引擎、带有执行任务的历史记录，以及用于快速访问的浮动小窗口。

## 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 框架 | Avalonia UI on .NET 8（Windows平台） | net8.0 |
| 架构 | MVVM | CommunityToolkit.Mvvm 8.4.2 |
| UI 主题 | Avalonia Fluent Theme（Apple 风格定制） | Avalonia 11.x |
| 图标 | Material.Icons.Avalonia | 3.x |
| 数据库 | SQLite | Microsoft.Data.Sqlite 10.0.5 |
| ORM | Dapper | 2.1.72 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 10.0.5 |
| 日志（抽象层） | Microsoft.Extensions.Logging.Abstractions | 10.0.5 |
| 日志（后端） | Serilog + Serilog.Extensions.Logging + Serilog.Sinks.File + Serilog.Sinks.Debug | 4.x / 10.0.0 / 7.0.0 / 3.0.0 |
| 序列化 | System.Text.Json（内置） | — |

## 解决方案结构

```
OnFlight/
├── OnFlight.sln
├── docs/
│   ├── architecture.md          # 英文版架构文档
│   ├── architecture_zh.md       # 本文件（中文版）
│   ├── data-and-sync.md         # 数据库 schema 与同步架构
│   ├── sync-schema.json         # 跨平台 JSON Schema 契约
│   ├── roadmap.md               # 功能路线图
│   ├── style-guide.md           # UI 样式指南
│   └── running-task-multiinstance.md  # 运行中任务多实例设计
└── src/
    ├── OnFlight.Contracts/      # 平台无关的数据契约（纯 C# 类库）
    ├── OnFlight.Core/           # 业务逻辑、数据访问、服务
    └── OnFlight.App/            # Avalonia UI 应用层（UI + DI 引导）
```

## 项目依赖关系图

```
OnFlight.App ──► OnFlight.Core ──► OnFlight.Contracts
      │                                    ▲
      └────────────────────────────────────┘
```

- **Contracts** 零外部依赖（纯 POCO + 枚举 + 接口）
- **Core** 依赖 Contracts + Dapper/SQLite
- **App** 依赖 Core 和 Contracts + Avalonia/FluentTheme/Material.Icons/MVVM/DI

---

## OnFlight.Contracts

平台无关的数据契约层。设计上可由 Swift 实现镜像，为未来 iOS 移植做准备。

| 文件 | 用途 |
|------|------|
| `Enums/TodoStatus.cs` | 任务生命周期状态：`Pending`（锁定/尚未可操作）、`Ready`（当前活跃/已解锁）、`Done`（已完成）、`Skipped`（已跳过，预留扩展） |
| `Enums/FlowNodeType.cs` | 管线节点类型：`Task`、`Loop`、`Fork`、`Join`、`Branch` |
| `Enums/OperationType.cs` | 审计日志操作类型：`Add`（预留）、`Delete`（预留）、`Update`（预留）、`Check`（活跃）、`Uncheck`（活跃）、`Reorder`（预留）、`Restore`（预留）。当前仅 `Check`/`Uncheck` 会实际写入日志。 |
| `Enums/RunningState.cs` | 运行实例生命周期：`Running`（进行中）、`AllDone`（全部完成） |
| `Models/TodoItemDto.cs` | 单个待办项的 DTO，带 JSON 序列化属性 |
| `Models/TodoListDto.cs` | 待办列表的 DTO（包含 `TodoItemDto` 列表） |

| `Models/OperationLogDto.cs` | 操作审计日志条目的 DTO |
| `Models/RunningInstanceDto.cs` | 运行中任务实例的 DTO（含状态的项、Fork/子任务上下文） |
| `Sync/ISyncProvider.cs` | 定义 `Push`/`Pull`/`ResolveConflict` 操作的接口 |
| `Sync/SyncPayload.cs` | 携带变更集用于同步的数据包 |
| `Sync/SyncResult.cs` | 同步操作结果（成功/错误/计数） |
| `Sync/ConflictResolution.cs` | 冲突策略枚举 + `SyncConflict` 模型 |
| `Schema/SchemaVersion.cs` | 数据库 schema 版本常量（`Current = 1`） |

---

## OnFlight.Core

核心业务逻辑层。无 UI 依赖。

### 模型 (`Models/`)

领域模型，带有同步元数据字段（`UpdatedAt`、`DeviceId`、`IsDeleted`）和导航属性。

| 文件 | 用途 |
|------|------|
| `TodoItem.cs` | 核心实体：任务，含标题、状态、排序、节点类型、流程配置，以及可选的子列表引用 |
| `TodoList.cs` | TodoItem 的容器；可通过 `ParentItemId` 实现嵌套 |

| `OperationLog.cs` | 任务状态变更的审计追踪条目 |
| `RunningInstance.cs` | 运行中任务实例实体，带 JSON 序列化状态 |

### 映射 (`Mapping/`)

| 文件 | 用途 |
|------|------|
| `DtoMapper.cs` | 领域模型 ↔ DTO 双向映射的扩展方法 |

### 数据 (`Data/`)

| 文件 | 用途 |
|------|------|
| `DatabaseInitializer.cs` | 首次运行时创建所有 SQLite 表、索引，并初始化 schema 版本 |
| `DbConnectionFactory.cs` | 根据连接字符串生产已打开的 `IDbConnection` 实例的工厂 |
| `DapperTypeHandlers.cs` | 自定义 Dapper 类型处理器，用于 `Guid`、`Guid?`、`DateTime` ↔ SQLite TEXT 映射 |

### 仓储 (`Data/Repositories/`)

每个仓储遵循接口隔离模式（接口 + 实现）。

| 接口 | 实现 | 用途 |
|------|------|------|
| `ITodoListRepository` | `TodoListRepository` | `todo_lists` 表的 CRUD |
| `ITodoItemRepository` | `TodoItemRepository` | `todo_items` 表的 CRUD，含排序管理 |

| `IOperationLogRepository` | `OperationLogRepository` | `operation_logs` 表的插入/查询/搜索 |
| `IRunningInstanceRepository` | `RunningInstanceRepository` | 运行中任务实例的 CRUD（JSON 序列化存储至 SQLite） |

### 服务 (`Services/`)

| 接口 | 实现 | 用途 |
|------|------|------|
| `ITodoService` | `TodoService` | 高级任务 CRUD：增删改查项目、管理子列表、重新排序、切换状态。纯数据操作，不记录日志。 |
| `ILogService` | `LogService` | 记录和查询操作审计日志，支持关键字/时间范围搜索。 |
| `IRunningTaskService` | `RunningTaskService` | 创建/加载/保存/删除运行中任务实例。将列表树（包括 Fork 子项和子任务子项）展平为每个实例一个项目列表。 |

| — | `NoOpSyncProvider` | 占位符 `ISyncProvider` 实现（所有方法返回成功/空）。 |

---

## OnFlight.App

Avalonia UI 应用层。引导 DI，组装 UI。

### 引导

| 文件 | 用途 |
|------|------|
| `Program.cs` | 应用入口点：`AppBuilder.Configure<App>().UsePlatformDetect()` |
| `App.axaml` | Avalonia 资源：FluentTheme（跟随系统明/暗模式）、自定义 `Theme.axaml`、转换器实例 |
| `App.axaml.cs` | DI 容器配置：配置 Serilog 文件日志 + 全局异常处理，注册所有仓储、服务、ViewModel 和 `TaskEventBus`。启动时初始化数据库。设置 `ShutdownMode.OnExplicitShutdown`，通过 `TrayIcon` 管理后台驻留生命周期（系统托盘图标：左键恢复主窗口，右键 NativeMenu：Show Main Window / New Floating Window / Exit）。暴露 `ShowMainWindow()`、`OpenNewFloatingWindow()`、`QuitApplication()`（退出前通过 try/finally 释放托盘后调用 `desktop.Shutdown()`）及 `IsApplicationQuitting` 标志。 |

### 视图模型 (`ViewModels/`)

| 文件 | 用途 |
|------|------|
| `MainViewModel.cs` | 核心 VM：管理根列表的选择/创建/删除、项目 CRUD、面包屑导航至子列表、进度追踪。通过 `ExpandChildrenRecursiveAsync` / `ExpandChildrenRecursiveWithStatusAsync` / `ExpandChildrenRecursiveDraftAsync` 递归展开 Fork 目标列表和子任务子项，按 depth 缩进内联显示。订阅 `TaskEventBus` 以刷新历史记录和同步运行中实例 UI。 |
| `TodoItemViewModel.cs` | 单项 VM，包装 `TodoItem` 用于列表绑定。暴露计算属性 `IsDone`、`NodeTypeIcon`、锁定/过渡状态。携带 `SourceItemId` 以在运行实例中保持对源项的可追溯性。 |
| `ItemConfigViewModel.cs` | 右侧面板配置 VM：编辑标题/描述/状态/节点类型。Fork 配置选取目标列表；Join 配置选取 Fork 项。属性变更时以 500ms 防抖自动保存至数据库。 |
| `HistoryViewModel.cs` | 从 `ILogService` 加载当前列表的操作日志。通过 `TaskEventBus` 事件自动刷新。 |
| `FloatingViewModel.cs` | 浮动小窗口的 VM：管理实例轮播（上一个/下一个）、创建新运行、委托切换至共享的 `RunningTaskInstanceViewModel`。 |
| `RunningTaskInstanceViewModel.cs` | 单实例 VM：管理带有 Fork/Join 屏障的展平项目列表、顺序锁定执行、浮动窗口分页、归档倒计时。DTO 往返时使用 `SourceItemId` 保留源项引用。通过 `TaskEventBus` 发布 `TaskChecked`/`TaskUnchecked`/`RunArchived` 事件。 |
| `RunningTaskManager.cs` | 管理活跃和已归档运行实例的集合。创建/移除实例，处理归档生命周期。 |
| `TaskEventBus.cs` | 单例事件总线，用于统一的任务状态变更处理。参见[事件总线架构](#事件总线架构)。 |
| `BreadcrumbItem`（在 MainViewModel.cs 中） | 面包屑导航路径的简单模型。 |
| `RootListItem`（在 MainViewModel.cs 中） | 根列表选择器下拉菜单的简单模型。 |

### 视图 (`Views/`)

| 文件 | 用途 |
|------|------|
| `MainWindow.axaml` | 主窗口（Apple 风格）：五列布局 — 左侧边栏（列表选择器）、分割条、中间区域（面包屑 + 进度条 + 添加项 + 可拖拽重排序的项目列表）、分割条、右侧面板（分页式配置/运行中/历史）。标题栏含浮动窗口和运行按钮。 |
| `MainWindow.axaml.cs` | Code-behind：从 DI 解析 MainViewModel，处理拖拽重排序逻辑、Enter 键快捷操作、内联重命名、运行卡片点击附加、打开浮动窗口。实现关闭进托盘行为：`OnCloseClick` 无 Shift 时隐藏窗口，Shift+关闭时调用 `QuitApplication()`；`OnMainWindowClosing` 拦截系统关闭（Alt+F4）并隐藏，除非 `IsApplicationQuitting`。通过 KeyDown/KeyUp + Deactivated 重置追踪 Shift 状态。 |
| `FloatingWindow.axaml` | 无边框、透明、置顶的浮动小窗口，带 Apple 风格圆角卡片。实例轮播含任务列表、复选框、分页、归档倒计时。 |
| `FloatingWindow.axaml.cs` | Code-behind：标题栏按下时 `BeginMoveDrag`，双击返回主窗口，关闭。`Activated` 处理器调用 `LoadAvailableListsAsync()`（带 try/catch + Serilog 错误日志），在用户切换回悬浮窗时刷新可用列表下拉框。 |

### 转换器 (`Converters/`)

| 文件 | 用途 |
|------|------|
| `BoolToVisibilityConverter.cs` | `bool`/`int`/`string` → `bool` 用于 `IsVisible`，支持 `Invert` 参数 |
| `BoolToOpacityConverter.cs` | `bool` → `double` 不透明度值 |
| `StatusToBrushConverter.cs` | `TodoStatus` → 彩色 `SolidColorBrush` |
| `NodeTypeToIconConverter.cs` | `FlowNodeType` → `MaterialIconKind` 枚举 |
| `DepthToMarginConverter.cs` | `int` 深度 → 左侧 `Thickness`，用于树形缩进 |
| `StatusToStrikethroughConverter.cs` | `TodoStatus.Done` → Avalonia `TextDecorationCollection` |
| `HexToBrushConverter.cs` | 十六进制颜色字符串 → `SolidColorBrush`，通过 `Color.Parse()` |
| `DoneToBrushConverter.cs` | `bool` IsDone → 勾选圆圈颜色画刷 |
| `DoneToTextBrushConverter.cs` | `bool` IsDone → 文本前景色（完成时灰色） |
| `DoneToIconKindConverter.cs` | `bool` IsDone → `MaterialIconKind`（空心圆圈 / 勾选圆圈） |

### 样式 (`Styles/`)

| 文件 | 用途 |
|------|------|
| `Theme.axaml` | Apple 风格设计令牌（明/暗主题字典、强调色、圆角半径）+ 自定义样式：`Card`、`SectionHeader`、`Breadcrumb`、`Apple` ProgressBar、带悬停过渡的 `ListItem`、`IconBtn`、`PlainIconBtn`、`CheckBtn`、`Fab`、`FloatingBall`、`WinBtn`/`WinClose`、TextBox/ComboBox 主题、内联重命名下划线 |

---

## 关键设计决策

### 事件总线架构

所有任务状态变更通过单一 `TaskEventBus`（单例）流转，确保无论哪个窗口发起操作都有一致的行为。

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
│  （共享实例 — 相同对象引用）              │
└───────────────────┬─────────────────────┘
                    │
                    │ PublishAsync(TaskEvent)
                    ▼
          ┌─────────────────┐
          │  TaskEventBus   │  （单例）
          │                 │
          │  1. 写入日志     │──► ILogService ──► operation_logs DB
          │  2. 通知订阅者   │
          └────────┬────────┘
                   │ EventPublished
          ┌────────┴────────┐
          ▼                 ▼
   MainViewModel      （未来订阅者）
   └─► 刷新历史记录
   └─► 同步运行中 UI
```

**事件类型：**

| `TaskEventKind` | 触发条件 | 记录为 |
|-----------------|----------|--------|
| `TaskChecked` | 任何任务在任意窗口切换为 Done | `OperationType.Check` |
| `TaskUnchecked` | 任何任务切换回 Pending | `OperationType.Uncheck` |
| `RunArchived` | 运行实例中所有任务完成 + 5秒倒计时结束 | `OperationType.Check` |

**关键不变量：** 只有 Check/Uncheck/Archive 事件会被记录。CRUD 操作（Add、Delete、Update、Reorder）不产生历史记录条目。

### 运行中任务实例

"运行"是任务列表项在创建时的快照式副本，以 JSON blob 形式存储在 `running_instances` 表中。每次运行都有独立的状态追踪。

- **顺序执行**：默认情况下，任务按顺序锁定。Fork/Join 节点充当屏障。
- **AllowOutOfOrder 模式**：解锁所有任务，允许自由完成顺序。
- **归档生命周期**：当所有任务完成 → 5秒倒计时 → 自动归档（可移至已归档列表）。用户可在倒计时期间撤销。
- **共享实例**：`RunningTaskManager` 以单例方式持有所有实例。MainWindow 和 FloatingWindow 引用相同的 `RunningTaskInstanceViewModel` 对象 — 任一窗口的更改对另一窗口立即可见。
- **唯一项 Id**：当 `RunningTaskService.ExpandItemsAsync` 展开 fork/子任务子项时，每个展开的子项获得一个新的 `Guid.NewGuid()` 作为 `Id`，同时 `SourceItemId` 保留对源项的原始引用。这避免了同一目标列表被多个 Fork 节点引用时的重复 Id 冲突。顶层项（非 fork/子任务子项）保持原始 Id 不变。
- **复合键状态映射**：`MainViewModel.SyncFromRunningInstanceAsync` 以 `(SourceItemId, ParentForkTitle)` 为组合键构建状态映射表，以正确区分同一源项在不同 Fork 分支下的不同状态。`ExpandChildrenRecursiveWithStatusAsync` 查找时也使用相同的复合键。
- **递归内联展开**：`MainViewModel` 在主内容区递归展开 Fork 目标列表和子任务子项。每层嵌套递增 `depth`，由 `DepthToMarginConverter` 转换为 `depth * 24px` 左缩进。UI 对 Fork 子项显示 `↳ Fork:` 提示，对子任务子项显示 `↳ Sub:` 提示。
- **自动推进**：`TryAutoAdvanceAsync` 在任务完成后尝试自动推进下一个任务。触发条件为 OR 逻辑——当前完成项设置了 `autoAdvanceSuccessor` **或**下一项设置了 `autoStartWhenPredecessorDone`，满足任一即可触发。支持递归链式推进（深度上限 `MaxAutoAdvanceDepth = 50`）。对 `ConsoleExecute` 类型节点会自动启动命令执行而非直接标记完成。

### 管线节点类型

每个 TodoItem 都有一个 `NodeType` 字段，控制其在流程引擎中的行为：

| 类型 | 行为 |
|------|------|
| **Task** | 普通待办项。按顺序执行。 |
| **Loop** | 将其子列表重复 N 次。配置：`{"loopCount": N}` |
| **Fork** | 分派到**目标任务列表**（非子列表）。配置：`{"targetListId": "<guid>"}`。目标列表递归展开（支持嵌套 Fork），每个叶子项被视为一个并行分支。 |
| **Join** | 等待**同一 depth 层级的所有前置 Fork** 及其子项完成。自动计算状态。一个 Join 可同时阻塞多个同层 Fork（N:1 关系）。 |
| **Branch** | Fork 内的条件执行。配置：`{"condition": "var op value"}` |

### Fork/Join 递归屏障机制

Fork/Join 采用基于 depth 的递归屏障模型：

```
[Task A] → [Task B] → Fork F1 → [child 1] [child 2] → Join J1 → [Task C]
                        ↑                                 ↑
                  A,B 完成后解锁子 task            子 task 全部完成后解锁 C
```

**核心规则：**

- **深度匹配**：Join 仅匹配**同 depth**的 Fork 节点。内层 Fork/Join 不会穿透到外层。
- **N:1 关系**：一个 Join 阻塞前方**所有同 depth 的 Fork**，每个 Fork 的 children 范围到下一个同 depth Fork 为止。Join 仅在所有 Fork 均完成后变为 Done。
- **递归展开**：Fork 的目标列表中如果包含嵌套的 Fork/Join，会递归展开为更深层级（depth+1），独立形成屏障。
- **DAG 环检测**：配置 Fork 的 targetListId 时，通过 `WouldCreateCycleAsync` 递归检查是否会形成环。检测到环时拒绝修改并弹窗提示。

**嵌套 Fork 示例：**

```
depth=0: [Task X] → Fork F1 ──────────────────────────────── Join J1 → [Task C]
                        │                                       ↑
depth=1:          [Task Y] → Fork F2 ─────── Join J2 → [Task Z]
                                 │              ↑
depth=2:                   [Task W1] [Task W2]
```

- J2 仅 join F2（同 depth=1），不会影响 F1
- J1 仅 join F1（同 depth=0），等待 F1 的所有 children（含 F2/J2 的完整子树）完成

**状态自动计算：**

- Fork 状态 = 其 children 范围内所有项目是否全部 Done/Skipped
- Join 状态 = 所有同 depth 前置 Fork 是否全部 Done

**锁定逻辑（`UpdateLockStatesForRange`）：**

1. 遇到 Fork 时，收集到 Join 之间所有同 depth 的 Fork
2. 检查 Fork 前的同层 Task 是否全部完成（`AllPriorTasksDone`）
3. 未完成 → Fork 的所有 children 全部锁定
4. 已完成 → 各 Fork 的 children 范围独立递归应用锁定逻辑
5. Join 之后的内容取决于 Join 的完成状态

### 条件求值

简单的 `变量 运算符 值` 表达式：
- 数值：`>`、`<`、`>=`、`<=`、`==`、`!=`
- 字符串：`==`、`!=`
- 变量来自流程引擎运行时上下文（如 `loopIndex`）

### DI 注册

```
Singleton:  DbConnectionFactory、所有 Repository、所有 Service、
            TaskEventBus、RunningTaskManager、MainViewModel
Transient:  FloatingViewModel（每个浮动窗口实例一个）
```

`TaskEventBus` 注册为 Singleton，使所有 ViewModel 共享同一事件总线。`RunningTaskManager` 为 Singleton，确保跨所有窗口的运行实例有唯一的数据源。

### 日志架构

应用使用 **Serilog** 作为日志后端，通过 `Serilog.Extensions.Logging` 桥接到 `Microsoft.Extensions.Logging`（`ILogger<T>`），所有组件使用标准抽象。

**配置**（在 `App.axaml.cs` 中）：

- **文件 Sink**：按日滚动写入 `%LOCALAPPDATA%/OnFlight/logs/onflight-{date}.log`，保留 7 天。
- **Debug Sink**：写入 `System.Diagnostics.Debug`（开发时在 VS 输出窗口可见）。
- **最低级别**：全局 `Information`，`Microsoft.*` 命名空间为 `Warning`。
- **全局异常处理**：捕获 `AppDomain.UnhandledException`（Fatal）和 `TaskScheduler.UnobservedTaskException`（Error）。应用退出时调用 `Log.CloseAndFlush()`。

**日志覆盖范围**：

| 组件 | Logger | 关键日志点 |
|------|--------|-----------|
| `TodoService` | `ILogger<TodoService>` | Info：列表/项目 CRUD。Error：删除/创建子列表事务回滚。 |
| `RunningTaskService` | `ILogger<RunningTaskService>` | Info：实例创建/删除。Warning：FlowConfig 解析失败。 |
| `TaskEventBus` | `ILogger<TaskEventBus>` | Info：事件发布。Error：事件处理器失败。 |
| `DatabaseInitializer` | `ILogger<DatabaseInitializer>` | Info：数据库初始化、数据清除。 |
| `SettingsService` | `ILogger<SettingsService>` | Info：设置已保存。Warning：设置加载失败。 |
| `MainViewModel` | `ILogger<MainViewModel>` | Warning：targetListId 解析失败。 |
| `ItemConfigViewModel` | `ILogger<ItemConfigViewModel>` | Warning：FlowConfig 解析失败。Error：自动保存失败。 |
| `FloatingViewModel` | `ILogger<FloatingViewModel>` | （预留未来使用） |

### 后台驻留与系统托盘

应用在用户关闭主窗口时保持后台运行，通过系统托盘图标管理窗口显隐与退出。

**生命周期：**

```
应用启动 → 主窗口 + TrayIcon 可见
         ↓
关闭（无 Shift） → MainWindow.Hide() → 进程继续存活，托盘图标保留
         ↓
托盘左键 → MainWindow.Show() + Activate() + 从最小化恢复
托盘菜单 "New Floating Window" → 新建 FloatingWindow 实例（多实例）
托盘菜单 "Exit" / Shift+关闭 → QuitApplication()
         ↓
QuitApplication → _isQuitting=true → try{从 TrayIcons 移除} finally{Dispose _trayIcon} → desktop.Shutdown()
```

**关键实现要点：**

- `ShutdownMode.OnExplicitShutdown` 防止最后一个窗口隐藏后进程自动退出。
- `_isQuitting` 标志协调 `MainWindow.Closing` 处理：为 false 时取消关闭并隐藏；为 true 时允许真正关闭。
- 关闭按钮使用 `Click` 事件（而非 `PointerPressed`，后者会被 `Button` 内部吞掉）。Shift 状态通过 `KeyDown`/`KeyUp` + `Deactivated` 重置追踪。
- `TrayIcon` 在 Code-behind 中创建（`EnsureTrayIcon`），图标从 `avares://OnFlight.App/Assets/tray.png` 加载。
- 托盘菜单文案为英文："Show Main Window"、"New Floating Window"、"Exit"。
- FloatingWindow 在 `Activated` 时刷新可用列表下拉框，保持与主窗口的数据同步。
