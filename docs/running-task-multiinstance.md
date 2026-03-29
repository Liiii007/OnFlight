# Running Task Multi-Instance Architecture

## Overview

Running Task 多实例改造：每次点 Start 从当前 task list 克隆一份独立执行上下文，各实例互不干扰，支持 Fork/Join 流程节点。悬浮窗以 flat view 显示纯 Task 列表，主窗口以 editor-like view 显示完整流程结构。

支持两种执行模式：
- **Sequential（默认）**：严格顺序执行，每个作用域只解锁第一个未完成 task
- **Free（乱序）**：Join 之前的所有 task 可自由勾选，Join 作为屏障阻塞后续任务

## Data Model

### `running_instances` Table

| Column | Type | Constraint | Description |
|--------|------|-----------|-------------|
| Id | TEXT | PK | UUID |
| SourceListId | TEXT | NOT NULL | Original task list ID |
| ListName | TEXT | NOT NULL | Display name (denormalized for quick query) |
| StateJson | TEXT | NOT NULL | Full serialized `RunningInstanceDto` JSON |
| CreatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| UpdatedAt | TEXT | NOT NULL | ISO 8601 UTC |
| DeviceId | TEXT | NOT NULL | Originating device |

Index: `idx_running_instances_source ON running_instances(SourceListId)`

### RunningInstanceDto

```json
{
  "id": "guid",
  "sourceListId": "guid",
  "listName": "string",
  "state": "Running | AllDone",
  "allowOutOfOrder": false,
  "createdAt": "ISO 8601",
  "items": [
    {
      "id": "guid",
      "sourceItemId": "guid",
      "title": "string",
      "description": "string?",
      "status": "Pending | Done | Skipped",
      "sortOrder": 0,
      "nodeType": "Task | Fork | Join | Loop | Branch",
      "depth": 0,
      "isForkChild": false,
      "parentForkTitle": "string?",
      "isSubTaskChild": false,
      "parentTaskTitle": "string?",
      "flowConfigJson": "string?"
    }
  ]
}
```

### RunningState Enum

| Value | Meaning |
|-------|---------|
| `Running` | In progress, not all tasks done |
| `AllDone` | All tasks completed |

## Architecture

### Layer Structure

```
Contracts   RunningInstanceDto, RunningInstanceItemDto, RunningState
Core        RunningInstance (DB model), IRunningInstanceRepository, IRunningTaskService
App         RunningTaskInstanceViewModel, RunningTaskManager, FloatingViewModel
```

### Instance Creation Flow

```
User clicks "Start New Run"
  → RunningTaskService.CreateInstanceAsync(listId)
    → Load TodoList from DB
    → Clone all items (Task/Fork/Join/Branch/Loop)
    → For each Fork: expand target list items inline (depth=1, isForkChild=true)
    → Serialize to StateJson, insert into running_instances
  → RunningTaskManager adds VM to Instances collection
  → FloatingViewModel auto-updates via PropertyChanged
```

### Dual View Model

`RunningTaskInstanceViewModel` maintains two item collections:

| Collection | Content | Used By |
|------------|---------|---------|
| `Items` | All nodes (Task + Fork + Join + ...) with depth/indentation | Main window Running Tab |
| `FlatTaskItems` | Tasks only, with `ForkJoinTag` context info | Floating window card |
| `FlatPageItems` | Current page (max 7) of `FlatTaskItems` | Floating window pagination |

### Auto-Archive

当一个 Running Instance 的所有 Task 完成后，启动 5 秒倒计时，倒计时结束后自动归档：

```
ToggleDone → UpdateStats → IsAllDone becomes true
  → StartArchiveCountdown()
    → IsPendingArchive = true, ArchiveCountdown = 5
    → 每秒递减 countdown
    → 5s 后 AllDoneReached event fires
  → RunningTaskManager.OnInstanceAllDone()
  → ArchiveInstance(): move from Instances to ArchivedInstances
```

倒计时期间 UI 显示绿色 banner："Archiving in Ns..." + UNDO 按钮：
- **UNDO**：取消倒计时，将最后一个已完成 task 恢复为 Pending 状态
- **反悔（取消最后完成的 task）**：如果在倒计时期间将某个 task 取消完成（使 IsAllDone 变回 false），倒计时自动取消

归档后的实例仍然保留在 DB 中，下次启动时根据 `IsAllDone` 状态自动分类到对应集合。

### Floating Window Design

悬浮窗以 carousel 形式展示 instances，每次显示一个 instance 卡片，通过 prev/next 导航切换：

```
┌──────────────────────────┐
│ OnFlight              [×]│
│ [New run from list... ][+]│
│                          │
│ ◀  1/3  ▶                │
│ ┌── Instance Card ──────┐│
│ │ ListName          [×] ││
│ │ HH:mm · 3/5    Free ○ ││
│ │ ✓ Task 1              ││
│ │ ○ Task 2              ││
│ │ 🔒 Task 3             ││
│ │       < page >        ││
│ └───────────────────────┘│
│                          │
│ ▸ Archived (2)           │
│ ┌ ListA ✓  01-15 10:30 ┐│
│ └ ListB ✓  01-14 09:00 ┘│
└──────────────────────────┘
```

- 每个 card 显示文字进度（`HH:mm · 3/5`）、Free 开关、分页 task 列表（无进度条）
- 顶部 carousel 导航（`◀ index/total ▶`）切换当前显示的 instance
- 归档区域默认折叠，点击展开查看已完成实例
- 归档实例仅显示名称、完成标记和时间，可删除

### Editor Item List Design

主窗口 editor 的 item list 采用扁平化横条设计，与 running instance 风格一致：

- 去掉卡片阴影和圆角，改用底部 1px `#EEE` 分割线
- Padding 从 16 缩小到 `6,5`，ListBoxItem Padding/Margin 归零
- CheckBox、NodeType 图标、Title 水平紧凑排列在左侧
- 操作按钮（子列表、新建子任务、删除）缩小到 24×24 靠右排列
- Fork child 行背景 `#FAFAFA`，hover 时 `#F5F5F5`
- 完成的 item 文字变灰 `#999` + 删除线
- Fork 来源标签字号 10，颜色 `#BDBDBD`

#### 非 Task 节点处理

- **CheckBox 隐藏**：非 Task 节点（Fork/Join/Branch/Loop）不显示 CheckBox，仅显示图标和标题
- **不参与进度**：`UpdateProgress` 只统计 `NodeType == Task` 的 item
- **不可 toggle**：`ToggleItemAsync` 对非 Task 节点直接 return
- **禁用 sub-task**：`CanHaveSubList` = `IsTaskNode && !IsForkChild`，非 Task 节点不显示 "Add sub-tasks" 按钮

#### 拖拽排序

支持在 editor 中拖拽调整 task 位置：

- ListBox 启用 `AllowDrop`，通过 `PreviewMouseMove` 启动 drag、`DragOver` 实时交换位置、`Drop` 持久化
- Fork child 不可拖拽也不可作为拖放目标
- `DragOver` 中实时 `Items.Move()` 提供即时视觉反馈，`Drop` 时调用 `PersistItemOrderAsync` 写入数据库
- `FindVisualAncestor` 兼容非 Visual 元素（如 `Run`），通过 `LogicalTreeHelper` 跳转到最近的 Visual 父级

### Join 配置简化

Join 节点不再需要用户手动选择等待哪些 Fork：

- 移除了 Config 面板中的 "Wait for Forks" 多选 UI
- Join 自动阻塞所有前面的 Fork 及其子任务
- Config 面板仅显示说明文字
- 保留旧 `forkItemIds` 的解析逻辑以兼容历史数据，新保存时不再写入该字段

### Animations

`Styles/Theme.axaml` 中定义的动画样式：

| Style Key | Target | Trigger | Effect |
|-----------|--------|---------|--------|
| `AnimatedCard` | `Border` | `Loaded` | 从下方 12px 滑入 + 淡入（0.3s，CubicEase） |
| `LockableItem` | `Border` | `IsLocked` DataTrigger | opacity 0.4 ↔ 1.0 渐变（0.25s，CubicEase） |
| `StrikethroughLine` | `Rectangle` | `ShowStrikethroughAnim` DataTrigger | 覆盖在标题文字上的删除线，从左到右 ScaleX 0→1 展开（0.45s，CubicEase），仅刚被标记完成的 item 播放 |
| `UnlockFlash` | `Border` | `IsJustUnlocked` DataTrigger | 解锁时背景闪绿 `#C8E6C9`→透明（1.5s，EaseInOut，0.15s 延迟），仅刚被解锁的 item 播放 |

#### 动画门控机制

`TodoItemViewModel` 新增三个 transient 属性控制动画是否播放：

| Property | Type | Purpose |
|----------|------|---------|
| `IsJustChanged` | `bool` | 当前用户操作刚改变了此 item 的状态（ToggleDone 时设为 true） |
| `IsJustUnlocked` | `bool` | 当前操作导致此 item 从 locked→unlocked |
| `ShowStrikethroughAnim` | `bool` (computed) | `IsDone && IsJustChanged`，仅对刚完成的 item 触发删除线动画 |
| `ShowUndoneAnim` | `bool` (computed) | `!IsDone && IsJustChanged`，预留反向动画 |

**流程**：`ToggleDoneAsync` → `ClearTransientFlags()` → 修改状态 → 设置 `IsJustChanged=true` → 快照 lock 状态 → `UpdateLockStates()` → 对比前后 lock 状态设置 `IsJustUnlocked` → 同步 flat items

这样从 DB 加载和翻页时所有 transient flag 均为 false，不会触发动画。

动画设计原则：
- **删除线动画**：`StrikethroughLine` 是一个 1.5px 的 `Rectangle` 覆盖在标题上方，通过 `ScaleTransform.ScaleX` 从左到右展开，模拟手动划删除线的效果。`ScaleTransform` 必须内联在每个 `Rectangle` 元素上（不能放在 Style Setter 中），否则会被 WPF freeze 导致动画报错
- **删除线宽度限制**：包含 TextBlock + Rectangle 的 Grid 使用 `HorizontalAlignment="Left"` 让宽度按文字内容自适应，避免删除线超出文字范围
- **仅对变化的 item 播放**：通过 `IsJustChanged` / `IsJustUnlocked` 门控，从 DB 恢复的已有完成状态不播放动画
- **解锁提示**：`UnlockFlash` 在 item 被解锁时闪一下绿色背景（1.5s 缓慢渐隐），提示用户有新任务可执行
- **卡片入场**：`AnimatedCard` 用于 instance card 首次加载时的入场动画
- **锁定渐变**：`LockableItem` 让 lock/unlock 状态切换有透明度过渡感

### Fork/Join Auto-Completion

Fork and Join nodes are **not manually toggleable**. Their status is computed:

- **Fork**: auto-completes when all its child items (`isForkChild && parentForkTitle == fork.Title`) are Done/Skipped
- **Join**: auto-completes when all preceding Fork nodes and their children are Done/Skipped

### Flat View Tag

In the floating window, each Task item carries an optional `ForkJoinTag`:

- Tasks with `IsForkChild == true`: `"Fork: <parentForkTitle>"`
- Tasks outside any Fork group: no tag（主序列 task 即使位于 Fork/Join 之间也不标记）

#### Fork 颜色区分

不同 fork group 的标签使用不同颜色，方便视觉区分来源：

- `RunningTaskInstanceViewModel` 维护 `_forkColorMap`（fork title → color hex）
- 预定义 12 种高区分度颜色，按 fork 出现顺序循环分配
- `TodoItemViewModel.ForkColorHex` 属性存储分配的颜色
- XAML 中通过 `HexToBrushConverter` 绑定到 `Foreground`

### Execution Modes & Lock States

每个 Running Instance 有一个 `AllowOutOfOrder` flag，可在运行时通过 UI toggle 切换。

#### Barriers（两种模式通用）

Fork 和 Join 均为执行屏障：

| 屏障类型 | 触发条件 | 阻塞范围 |
|----------|----------|----------|
| **Fork** | Fork 之前的所有主序列 task 尚未全部完成 | Fork 的所有子 task 被锁定 |
| **Join** | Join 自身未完成（即 Fork + 所有子 task 未全部完成） | Join 之后的所有 task 被锁定 |

执行流示例：

```
[Task A] → [Task B] → Fork → [child 1] [child 2] [child 3] → Join → [Task C]
                        ↑                                       ↑
                  A,B 完成后解锁子 task               子 task 全部完成后解锁 C
```

#### Sequential Mode（`AllowOutOfOrder = false`，默认）

| 规则 | 说明 |
|------|------|
| 主序列顺序 | depth=0 非 fork-child 的 task 只能按顺序完成，只解锁第一个未完成项 |
| Fork 组顺序 | 每个 fork 组（同一 `parentForkTitle`）内部只能按顺序完成 |
| Fork 屏障 | Fork 前所有主序列 task 完成后才解锁子 task |
| Join 屏障 | Fork + 所有子 task 完成后才解锁 Join 后续 task |
| 非 Task 节点 | Fork/Join/Loop/Branch 始终锁定，状态由系统自动计算 |

#### Free Mode（`AllowOutOfOrder = true`）

| 规则 | 说明 |
|------|------|
| 自由执行 | 同一屏障区间内的所有 task 均可自由勾选，无顺序约束 |
| Fork 屏障 | 同上，Fork 前 task 完成后子 task 全部同时解锁 |
| Join 屏障 | 同上，Join 后 task 在 Join 完成后全部同时解锁 |
| 非 Task 节点 | 同上，始终锁定 |

#### UI 呈现

- **悬浮窗**：锁定的 task 按钮显示锁头图标且禁用点击；无进度条，仅文字进度
- **主窗口 Running Tab**：锁定的 item 以 40% 透明度渐变（`LockableItem` 动画）
- **"Free" 开关**：悬浮窗 info 行右侧 + 主窗口实例卡片标题行右侧均有 toggle 开关

### Paging & Smart Start

- Page size: 7 items
- Smart start: page begins at the last consecutive completed item, so the user sees their current progress position

## Key Files

| File | Purpose |
|------|---------|
| `Contracts/Enums/RunningState.cs` | `Running`, `AllDone` enum |
| `Contracts/Models/RunningInstanceDto.cs` | DTO with full item list (includes `Depth`, `IsForkChild`, `ParentForkTitle`, `IsSubTaskChild`, `ParentTaskTitle`, `FlowConfigJson`) + `AllowOutOfOrder` flag |
| `Core/Models/RunningInstance.cs` | DB row model with `StateJson` field |
| `Core/Data/Repositories/IRunningInstanceRepository.cs` / `RunningInstanceRepository.cs` | Repository interface + Dapper implementation |
| `Core/Services/IRunningTaskService.cs` / `RunningTaskService.cs` | Create/GetAll/Save/Delete; clones list items including Fork and sub-task expansion |
| `App/ViewModels/RunningTaskInstanceViewModel.cs` | Per-instance VM: dual view (`Items` + `FlatTaskItems`), paging, toggle, fork/join auto-state, sequential/free lock logic, archive countdown, animation gating flags |
| `App/ViewModels/RunningTaskManager.cs` | Singleton: manages `Instances` + `ArchivedInstances`, auto-archive on AllDone, `ShowArchived` toggle |
| `App/ViewModels/FloatingViewModel.cs` | Carousel VM: prev/next navigation between instances, `AvailableLists` for new run creation |
| `App/Converters/HexToBrushConverter.cs` | Hex color string → `SolidColorBrush` for fork tag coloring |

## DI Registration

```csharp
services.AddSingleton<IRunningInstanceRepository, RunningInstanceRepository>();
services.AddSingleton<IRunningTaskService, RunningTaskService>();
services.AddSingleton<RunningTaskManager>();
services.AddSingleton<MainViewModel>();
services.AddTransient<FloatingViewModel>();
```

`RunningTaskManager` is Singleton — shared between `MainViewModel` and `FloatingViewModel`. `FloatingViewModel` is Transient (one per floating window).

## Key Design Decisions

- **JSON 全量序列化**: 每次状态变更后整个 DTO 重新序列化写入 `StateJson`，对几十个 item 的规模性能可忽略
- **克隆而非引用**: Running Task 的 items 从原 task list 克隆，执行不影响编辑数据
- **Dual View**: `Items` (full structure) 用于主窗口展示和持久化，`FlatTaskItems` (tasks only) 用于悬浮窗简洁交互
- **Fork 展开**: 创建实例时自动将 Fork 的 target list items 内联展开，depth=1，后续不再查询 DB
- **自动归档**: 所有 task 完成后通过 `AllDoneReached` 事件自动从 `Instances` 移到 `ArchivedInstances`，DB 数据不删除
- **Fork/Join 即屏障**: 无论 Sequential 还是 Free 模式，Fork 和 Join 节点始终作为执行屏障
- **乱序 flag 运行时可切换**: `AllowOutOfOrder` 持久化到 `StateJson`，切换时即时重算 lock 状态
- **Fork 标签颜色区分**: 使用 12 色循环分配方案 + `HexToBrushConverter`，同一 fork title 总是得到相同颜色
- **拖拽排序实时反馈**: DragOver 时直接 `Items.Move()` 而非 Drop 时才排序，体验更流畅
- **Join 隐式阻塞**: 取消手动 fork 选择，Join 自动阻塞前方所有 Fork；简化用户配置
- **非 Task 节点隔离**: Fork/Join/Branch/Loop 不参与进度计算、不可 toggle、不显示 CheckBox、不支持 sub-task，保持清晰的关注点分离
- **ScaleTransform 内联定义**: 避免 WPF 在 Style Setter 中 freeze `Freezable` 对象导致动画异常
