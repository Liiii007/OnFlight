# OnFlight Roadmap

## v1.0

### 1. Task Post-Completion Script

- Each task node supports an optional `cmd` field
- After a task is marked as done, the configured shell command/script is executed automatically
- Support configuring additional running instance parameters and automatically injecting them into script execution context
- Allow script execution to produce new values and sync them back into the current running instance
- Use cases: open a URL, run a build, trigger a notification, etc.

### 2. Inline Note / Memo (Markdown)

- Both the floating window and the main editor support attaching markdown notes to the current running instance
- Users can jot down results, observations, or paste screenshots during execution
- Notes (`.md` files) and embedded images are saved to a dedicated directory (e.g. `notes/<run-id>/`)
- Notes are linked to the specific run instance for later review

### 3. Running Task Rename & Description

- Allow renaming a running task instance (currently uses the source list name)
- Add an optional description/summary field to each running instance
- Editable from both the main window and the floating window

### 4. Auto-Apply Task List Changes to Running Tasks

- When the task list is updated, apply compatible changes to current running task instances automatically
- Keep progress states stable while merging structural/content updates from the latest task definition
- Provide conflict-safe rules for deleted/renamed/reordered nodes

### 5. Memory Usage Optimization

- Reduce runtime memory footprint in long-running and multi-instance scenarios
- Optimize view model/object lifecycle and avoid unnecessary retained references
- Add baseline measurement and regression checks for memory usage

### 6. Task Grouping and Hierarchical Management

- Introduce task groups as a first-class concept
- Support creating tasks directly inside different groups
- Support expand/collapse on groups to improve hierarchy readability and navigation

## v1.1

### 1. External-Driven Task Status Auto-Update

- Support updating internal task status via external calls/events
- Research and design a robust auto-update mechanism (event source, mapping rules, conflict handling, retry strategy)
- Ensure traceability and safety when external updates are applied to running task instances

### 2. Event Export and Recovery Mechanism

- Support exporting task-related events into a portable format
- Support recovering/replaying events to restore task progress and execution context
- Define versioning and compatibility rules for long-term event archive usability

### 3. Automated Setup and Teardown Stages

- Introduce standardized setup and teardown phases for task execution lifecycle
- Support auto-running setup before task execution and teardown after completion/failure
- Ensure stage status is visible and traceable in running instance context

### 4. Task Auto-Advance

- Support automatically advancing to the next task when current task meets completion conditions
- Provide configurable auto-advance rules (manual override, dependency guard, failure stop)
- Keep auto-advance behavior consistent across main window and floating window
