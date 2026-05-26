# Diagnostics-Debugger

## 职责边界

维护 Unity ECS 三层诊断体系如何在 EX-GAS Runtime Core Debugger 中落地的规则。覆盖 Entities Journaling、Structural Changes Profiler Module 和手写 Runtime Core Debugger 的职责边界、使用时机和产出物要求。不覆盖 Unity Profiler 通用使用指南或非 ECS 相关的诊断工具。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解三层诊断体系和 Debugger 最小指标集）
2. **核心规范**（按严重度）
   - `DBG-01: 三层诊断体系各有明确职责边界，不可互相替代.md` — P0: 三层诊断体系各有明确职责边界，不可互相替代
   - `DBG-02: Entities Journaling 必须在性能测试中关闭.md` — P1: Entities Journaling 必须在性能测试中关闭
   - `DBG-03: Structural Changes Profiler 是热点定位工具，不能替代自动验收.md` — P1: Structural Changes Profiler 是热点定位工具，不能替代自动验收
   - `DBG-04: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存.md` — P1: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存
   - `DBG-05: 性能报告必须按 cost 分组统计，禁止混合归因.md` — P1: 性能报告必须按 cost 分组统计，禁止混合归因
3. **拓展阅读**（按需）
   - GAS Runtime Core phase 设计 → `System-World-SystemGroup/_index.md`
   - 性能数据流 → `数据流-系统生命周期/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | 三层诊断体系 + RuntimeCore Debugger 最小指标集 |
| `DBG-01: 三层诊断体系各有明确职责边界，不可互相替代.md` | 规范 P0 | 三层诊断体系职责边界 |
| `DBG-02: Entities Journaling 必须在性能测试中关闭.md` | 规范 P1 | Journaling 在性能测试中关闭 |
| `DBG-03: Structural Changes Profiler 是热点定位工具，不能替代自动验收.md` | 规范 P1 | Structural Changes Profiler 不能替代自动验收 |
| `DBG-04: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存.md` | 规范 P1 | Debugger 只记录计数，不做字符串拼接 |
| `DBG-05: 性能报告必须按 cost 分组统计，禁止混合归因.md` | 规范 P1 | 性能报告按 cost 分组统计 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `DBG-01` | P0 | 三层诊断体系各有职责边界 | Debugger 启动检查 Journaling 是否开启；开启且非排查则告警 |
| `DBG-02` | P1 | Journaling 在性能测试中关闭 | 性能测试启动脚本检查 journaling 状态 |
| `DBG-03` | P1 | Structural Changes Profiler 不能替代自动验收 | Debugger 包含按 system 分组的结构变化计数 |
| `DBG-04` | P1 | Debugger 只记录计数，不做字符串拼接 | 搜索 Debugger hot path 中的 `$""`、`string.Format` |
| `DBG-05` | P1 | 性能报告按 cost 分组统计 | Debugger validation summary 包含 cost 分组表格 |

## 跨主题引用

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `DBG-05` | System-World-SystemGroup | 性能报告 phase 成本拆分 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| GAS Runtime Core phase 设计 | `System-World-SystemGroup/API与EX-GAS解读.md` |
| 数据流确定性与生命周期 | `数据流-系统生命周期/_index.md` |

## 验收指标

1. 每轮性能测试输出 core / physics / render / runner 分组。
2. 能定位 top N system、query count、lookup count、structural changes、buffer spill。
3. 发现架构问题时能反哺当前架构事实。
4. Debugger 不增加可观测的主线程阻塞。
5. Journaling 在性能测试中自动检测并阻止。
