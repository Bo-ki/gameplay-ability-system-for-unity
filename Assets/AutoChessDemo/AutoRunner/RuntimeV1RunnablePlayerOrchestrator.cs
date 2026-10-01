using System;
using GAS.Runtime;
using UnityEngine;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 在 Development Player 启动前执行 Runtime V1 三向量验收并以原子文件交付结果。
    /// </summary>
    public static class RuntimeV1RunnablePlayerOrchestrator
    {
        private const string ProductionInstallNotEvaluated = "NotEvaluated";
        private const string SupportProfilePassed = "Passed";

        /// <summary>
        /// 仅响应独立 Runtime V1 flag；成功与失败都显式写证据并退出进程。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunBeforeSceneLoad()
        {
            if (!RuntimeV1RunnableEvidence.IsRequested())
                return;

            var manifestPath = RuntimeV1RunnableEvidence.GetCommandLineValue(
                RuntimeV1RunnableEvidence.ManifestArgument);
            var expectedSha256 = RuntimeV1RunnableEvidence.GetCommandLineValue(
                RuntimeV1RunnableEvidence.ManifestSha256Argument);
            var requestedRunId = RuntimeV1RunnableEvidence.GetCommandLineValue(
                RuntimeV1RunnableEvidence.RunIdArgument);
            RuntimeV1RunnableRunManifest manifest = null;
            var exitCode = 1;
            try
            {
                manifest = RuntimeV1RunnableEvidence.ReadValidatedManifest(
                    manifestPath, expectedSha256);
                ValidateRunIdentity(manifest, requestedRunId);
                var result = Execute(manifest);
                RuntimeV1RunnableEvidence.WritePlayerResultAtomic(manifest.ManifestPath, result);
                exitCode = result.Passed ? 0 : 1;
                Debug.Log("RuntimeV1RunnablePlayer: passed=" + result.Passed
                          + ", runId=" + result.RunId);
            }
            catch (Exception exception)
            {
                TryWriteFailure(manifestPath, requestedRunId, manifest, exception);
                Debug.LogException(exception);
            }
            finally
            {
                try
                {
                    AutoChessBattleManager.ShutdownRuntime();
                }
                catch (Exception shutdownException)
                {
                    Debug.LogError("RuntimeV1RunnablePlayer shutdown failed: " + shutdownException);
                    exitCode = 1;
                }
                finally
                {
                    Application.Quit(exitCode);
                }
            }
        }

        /// <summary>
        /// 顺序执行 Ability、9203 与 AutoChess，避免共享 World 污染三向量身份。
        /// </summary>
        private static RuntimeV1RunnablePlayerResult Execute(
            RuntimeV1RunnableRunManifest manifest)
        {
            var ability = RuntimeV1RunnableScenarioRunner.RunAbilityVector();
            var gameplayEffect = RuntimeV1RunnableScenarioRunner.RunGameplayEffect9203Vector();
            var autoChess = RuntimeV1RunnableScenarioRunner.RunAutoChessVector();
            var supportAdmission = autoChess.SupportProfileAdmission;
            var supportPassed = string.Equals(
                                    ability.SupportProfileAdmission,
                                    GasStageBSpawnFaultReason.None.ToString(),
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    gameplayEffect.SupportProfileAdmission,
                                    GasStageBSpawnFaultReason.None.ToString(),
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    supportAdmission,
                                    GasStageBSpawnFaultReason.None.ToString(),
                                    StringComparison.Ordinal);
            var passed = supportPassed && ability.Vector.Passed
                         && gameplayEffect.Vector.Passed && autoChess.Vector.Passed;
            return new RuntimeV1RunnablePlayerResult
            {
                RunId = manifest.RunId,
                ProducerFingerprint = manifest.ProducerFingerprint,
                GeneratedArtifactIdentity = manifest.GeneratedArtifactIdentity,
                FinalSourceFingerprint = manifest.FinalSourceFingerprint,
                BuildHash = manifest.BuildHash,
                RunManifestSha256 = manifest.RunManifestSha256,
                SupportProfileAdmission = supportPassed
                    ? SupportProfilePassed
                    : "Failed:" + ability.SupportProfileAdmission + "/"
                      + gameplayEffect.SupportProfileAdmission + "/" + supportAdmission,
                ProductionInstallAdmission = ProductionInstallNotEvaluated,
                DeclaredFullSemanticEligibility = false,
                Scale = RuntimeV1RunnableScenarioRunner.FrozenScale,
                AscCount = RuntimeV1RunnableScenarioRunner.FrozenAscCount,
                Ability = ability.Vector,
                GameplayEffect9203 = gameplayEffect.Vector,
                AutoChess = autoChess.Vector,
                SemanticHash = RuntimeV1RunnableScenarioRunner.ComputeAggregateHash(
                    ability.Vector, gameplayEffect.Vector, autoChess.Vector, true),
                StateHash = RuntimeV1RunnableScenarioRunner.ComputeAggregateHash(
                    ability.Vector, gameplayEffect.Vector, autoChess.Vector, false),
                Passed = passed,
                Failure = BuildFailure(ability, gameplayEffect, autoChess, supportPassed),
            };
        }

        /// <summary>
        /// 校验 launcher 参数与 manifest 内部 RunId 是同一个不可变运行身份。
        /// </summary>
        private static void ValidateRunIdentity(
            RuntimeV1RunnableRunManifest manifest,
            string requestedRunId)
        {
            if (string.IsNullOrWhiteSpace(requestedRunId)
                || !string.Equals(manifest.RunId, requestedRunId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Runtime V1 Player RunId does not match the validated manifest.");
            }
        }

        /// <summary>
        /// 汇总失败原因；成功时保持空字符串。
        /// </summary>
        private static string BuildFailure(
            RuntimeV1RunnableAbilityEvidence ability,
            RuntimeV1RunnableGameplayEffectEvidence gameplayEffect,
            RuntimeV1RunnableAutoChessEvidence autoChess,
            bool supportPassed)
        {
            if (supportPassed && ability.Vector.Passed
                && gameplayEffect.Vector.Passed && autoChess.Vector.Passed)
                return string.Empty;
            return "support=" + supportPassed
                   + "; ability=" + (ability.Failure.Length > 0 ? ability.Failure : ability.Vector.Summary)
                   + "; effect9203=" + (gameplayEffect.Failure.Length > 0
                       ? gameplayEffect.Failure : gameplayEffect.Vector.Summary)
                   + "; autoChess=" + (autoChess.Failure.Length > 0
                       ? autoChess.Failure : autoChess.Vector.Summary);
        }

        /// <summary>
        /// 顶层异常仍尽力生成同 schema 失败结果；证据路径不可用时只保留进程错误日志。
        /// </summary>
        private static void TryWriteFailure(
            string manifestPath,
            string requestedRunId,
            RuntimeV1RunnableRunManifest manifest,
            Exception exception)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                return;
            try
            {
                var result = new RuntimeV1RunnablePlayerResult
                {
                    RunId = manifest != null ? manifest.RunId : requestedRunId,
                    ProducerFingerprint = manifest != null ? manifest.ProducerFingerprint : string.Empty,
                    GeneratedArtifactIdentity = manifest != null
                        ? manifest.GeneratedArtifactIdentity : string.Empty,
                    FinalSourceFingerprint = manifest != null
                        ? manifest.FinalSourceFingerprint : string.Empty,
                    BuildHash = manifest != null ? manifest.BuildHash : string.Empty,
                    RunManifestSha256 = manifest != null
                        ? manifest.RunManifestSha256 : string.Empty,
                    SupportProfileAdmission = string.Empty,
                    ProductionInstallAdmission = ProductionInstallNotEvaluated,
                    DeclaredFullSemanticEligibility = false,
                    Scale = RuntimeV1RunnableScenarioRunner.FrozenScale,
                    AscCount = RuntimeV1RunnableScenarioRunner.FrozenAscCount,
                    Passed = false,
                    Failure = exception.ToString(),
                };
                RuntimeV1RunnableEvidence.WritePlayerResultAtomic(manifestPath, result);
            }
            catch (Exception writeException)
            {
                Debug.LogError("RuntimeV1RunnablePlayer failure evidence write failed: "
                               + writeException);
            }
        }
    }
}
