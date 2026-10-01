[CmdletBinding()]
param(
    [string]$UnityExe = "",
    [string]$TestFilter = "",
    [string]$TestResultsPath = "",
    [string]$LogFile = "",
    [switch]$IgnoreOpenEditorCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

throw @"
Run-StackingRuntimeTests.ps1 已退役：原 GAS.Runtime.Tests 测试源验证 legacy Entity/EffectRuntimeUtility，当前仓库已删除该 authority，禁止重定向后冒充 Runtime v1 Stacking 通过。

请改用 Tools/Tests/Run-RuntimeV1ConformanceTests.ps1。TB-09 当前仍为 Pending；局部 effect lifecycle 测试通过不等于 9203 端到端语义 Green。
"@
