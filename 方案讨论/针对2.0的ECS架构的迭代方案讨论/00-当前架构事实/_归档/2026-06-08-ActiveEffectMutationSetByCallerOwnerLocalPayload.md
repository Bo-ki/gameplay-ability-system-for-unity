# ActiveEffectMutation set-by-caller owner-local payload 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton stream owner / ActiveMutation command payload

## 变更事实

1. 新增 `ActiveEffectMutationSetByCallerValueBuffer`，作为 ASC owner-local 的 ActiveMutation command payload carrier。
2. `ASCEntityFactory`、`GASRuntimeEntityArchetypes.ASC()`、`GASRuntimeQueryLayoutPlan` 已纳入该 buffer；`ActiveEffectOwnerLocalMutationFramePrepareSystem` 每帧同时清空 command、set-by-caller payload 与 mutation output。
3. `GEEffectCommandCatalogNormalizeSystem` 在投影 `GEEffectCommandKind.ActiveMutation` 到目标 ASC 时，会把 stream `GESetByCallerValueBuffer` 中对应 command sequence 的 range 复制到目标 ASC 的 `ActiveEffectMutationSetByCallerValueBuffer`，并把 command 的 `SetByCallerStart/Count` 重映射为 owner-local range。
4. `GEActiveEffectMutationOwnerCommandCollectJob` 从 ASC owner-local command + set-by-caller payload 收集，压平成 frame-local `NativeList<GEEffectCommandBuffer>` 与 `NativeList<GESetByCallerValueBuffer>`。
5. `GEActiveEffectMutationChunkApplyJob` 的 ActiveMutation 主路径不再以 `CommandSetByCallerLookup.HasBuffer(StreamEntity)` 作为处理前置条件；magnitude resolution 与 persistent slot `CopySetByCallerSnapshot(...)` 均读取 frame-local owner payload list。
6. `GasGlueCodeGenPhases.cs` 模板已同步 normalize、collect、apply 和 helper 签名，避免 SourceGenerator 回生 stream-backed set-by-caller active mutation path。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 已新增防回流门：ASC archetype、frame prepare、query layout、normalize projection、collect flatten、apply payload consumption 和 template 同步均被断言覆盖。

## 明确不能推出的结论

1. 本切片不证明 active mutation 全链路完全退出 singleton command/spec stream；原始 `GEEffectCommandBuffer` producer 仍先进入 singleton stream 后由 normalize 投影到 ASC。
2. overflow / period derived command append 仍保留 stream fallback；本切片只保证 ActiveMutation 本体的 set-by-caller payload 在消费侧不再依赖 singleton stream buffer。
3. instant spec build、execution extension、Boundary fact export、Unity headless AutoChess x50/x100/x1000 与 profiler pass 未由本切片证明。

## 本轮验证

1. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
2. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，保留既有 `MSB3277` warning。
3. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，保留既有 `MSB3277` warning。

## 后续入口

后续 P0-D/R3 继续处理 `GEEffectCommandBuffer` producer 直写 owner-local lane、instant spec build / execution extension 的 stream 依赖；P0-C/R5 继续迁出 overflow / period derived command append。
