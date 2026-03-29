# OnFlight

OnFlight 是一个可重入（re-entrant）的 Todo List 项目，用于跟踪重复性任务与相关流程在不同运行实例中的执行进度。

> This project is built almost entirely through vibe coding.

## 项目简介

OnFlight 面向“同一流程需要反复执行”的场景：
- 一份任务清单可以多次启动
- 每次启动形成独立的 running instance
- 各实例分别记录完成状态与进度

## 核心能力

- 管理结构化任务清单与流程节点
- 启动并跟踪多个运行中的任务实例
- 在主窗口与悬浮窗口中快速查看和更新当前进度
- 支持对重复任务进行持续、可追踪的过程管理

## 设计灵感

项目灵感主要来自：
1. 航空飞行检查单（Flight Checklist）对步骤完整性、顺序性与可追踪性的要求
2. 构建流水线（Build Pipeline）对重复执行、状态可视化和流程控制的实践

因此，OnFlight 采用“可重入 Todo List”方式，专门用于跟踪不同重复任务及其相关流程的进度。
