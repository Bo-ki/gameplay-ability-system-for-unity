using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GAS.AutoChessDemo;
using GAS.Editor;
using GAS.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证同一 RuntimeV1SupportProfile RuleId 在 raw normalization 前与 production Blob 上均 fail-closed。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableSupportProfileTests
    {
        /// <summary>
        /// 验证 production raw/Blob 通过，并用紧凑 mutation 表覆盖本轮必须拒绝的 authoring 族。
        /// </summary>
        [Test]
        public void RawAndBlobProfile_RejectUnsupported()
        {
            var sources = RawSources.Find();
            var sourceBytes = sources.Paths.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
            Assert.That(Validate(sources).Succeeded, Is.True, "Production raw authoring must pass before normalization.");

            AssertRawReject(sources, RawDocument.Ability, token => AbilityIds(token, new JValue(9201)));
            AssertRawReject(sources, RawDocument.Ability, token => AbilityIds(token, new JArray()));
            AssertRawReject(sources, RawDocument.Ability, token => AbilityIds(token, new JArray(9201, 9202)));
            AssertRawReject(sources, RawDocument.Ability, token => AbilityIds(token, new JArray(0)));
            AssertRawReject(sources, RawDocument.Ability, token => Ability(token)["ID"] = 9199);
            AssertRawReject(sources, RawDocument.Ability, token => AbilityIds(token, new JArray(9202)));
            AssertRawReject(sources, RawDocument.Ability, token => Ability(token)["Cost"] = 1);
            AssertRawReject(sources, RawDocument.Ability, token => Ability(token)["ActivationRequiredTags"] = new JObject());
            AssertRawReject(sources, RawDocument.Ability, token => Ability(token)["AbilityExecution"]["$type"] = "TimelineRef");
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["CueOnTick"] = new JArray(9301));
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["GrantedTags"] = new JArray(1));
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["ApplicationRequiredTags"] = new JObject());
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["SetByCaller"] = new JObject());
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["Period"]["Effects"] = new JArray(9204));
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["Stacking"]["OverflowEffects"] = new JArray(9204));
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["RuntimeV1Evaluator"]["Kind"] = 99);
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["RuntimeV1TargetPolicy"]["Life"] = 99);
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["ID"] = 9299);
            AssertRawReject(sources, RawDocument.Effect, token => Poison(token)["CueOnApply"] = new JArray(9399));
            AssertRawReject(sources, RawDocument.Attribute, token => CombatAttribute(token, 4)["ID"] = 5);
            AssertRawReject(sources, RawDocument.Sidecar, token => ((JObject)token)["gameplayEffects"] = new JArray());
            AssertRawReject(sources, RawDocument.Sidecar, token => ((JArray)token["units"])[0]["attack"] = 0f);
            AssertRawReject(sources, RawDocument.Sidecar,
                token => ((JObject)token["validationScenario"]).Property("maxTicks").Remove());
            AssertRawReject(sources, RawDocument.Sidecar, token => token["validationScenario"]["scale"] = 0);
            AssertRawReject(sources, RawDocument.Sidecar, token => Unit(token, 0)["team"] = "Unknown");
            AssertRawReject(sources, RawDocument.Sidecar, token => Unit(token, 0)["primaryAbility"] = "Unknown");
            AssertRawReject(sources, RawDocument.Sidecar,
                token => Unit(token, 0)["primaryTargetPolicy"] = "Unknown");

            foreach (var pair in sourceBytes)
                Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), $"Mutation test changed production source: {pair.Key}");

            using var catalog = AutoChessRuntimeV1CatalogAccess.BuildProductionCatalog(Allocator.Temp, out var expectation);
            ref var root = ref catalog.Value;
            Assert.That(GasDefinitionCatalogValidator.Validate(ref root, in expectation).Succeeded, Is.True);
            var profile = GasRuntimeV1SupportProfile.ValidateBlob(ref root);
            Assert.That(profile.Succeeded, Is.True);
            Assert.That(profile.RuleId, Is.EqualTo(GasRuntimeV1SupportProfile.RuleId));
            AssertBlobRejects(ref root);
        }

        /// <summary>
        /// 逐项 mutation production Blob，验证闭世界 identity 与 9203 Attack capture 均 fail-closed。
        /// </summary>
        private static void AssertBlobRejects(ref GasDefinitionCatalogBlob root)
        {
            ref var attribute = ref root.AttributeLayout.Entries[1];
            attribute.AttributeId = 5;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.ClosedWorldShapeMismatch);
            attribute.AttributeId = 2;

            ref var ability = ref root.Abilities[0];
            ability.DefinitionId = 9199;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.AbilityUnsupported);
            ability.DefinitionId = 9101;

            ref var node = ref root.DirectEffectProgramNodes[0];
            node.EffectDefinitionId = 9202;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.AbilityUnsupported);
            node.EffectDefinitionId = 9201;

            ref var effect = ref root.GameplayEffects[0];
            effect.DefinitionId = 9299;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.EffectUnsupported);
            effect.DefinitionId = 9201;

            ref var capture = ref root.CaptureDescriptors[0];
            capture.Binding = GasCaptureBinding.Live;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.CaptureUnsupported);
            capture.Binding = GasCaptureBinding.Snapshot;
            capture.AttributeLayoutIndex = 1;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.CaptureUnsupported);
            capture.AttributeLayoutIndex = 2;

            ref var cue = ref root.CueReferences[0];
            cue.CueDefinitionId = 9399;
            AssertBlobError(ref root, GasRuntimeV1SupportProfileError.CueUnsupported);
            cue.CueDefinitionId = 9301;
            Assert.That(GasRuntimeV1SupportProfile.ValidateBlob(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 断言当前 Blob mutation 返回指定 typed profile error。
        /// </summary>
        private static void AssertBlobError(
            ref GasDefinitionCatalogBlob root,
            GasRuntimeV1SupportProfileError expected)
        {
            var result = GasRuntimeV1SupportProfile.ValidateBlob(ref root);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(result.RuleId, Is.EqualTo(GasRuntimeV1SupportProfile.RuleId));
        }

        /// <summary>
        /// 在独立临时目录复制 production raw 文档、应用单一 mutation 并断言 typed reject。
        /// </summary>
        private static void AssertRawReject(RawSources sources, RawDocument document, Action<JToken> mutate)
        {
            var root = Path.Combine(Path.GetTempPath(), "RuntimeV1RunnableRawProfile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var copy = sources.CopyTo(root);
                var path = copy.GetPath(document);
                var token = JToken.Parse(File.ReadAllText(path));
                mutate(token);
                File.WriteAllText(path, token.ToString());
                var result = Validate(copy);
                Assert.That(result.Succeeded, Is.False, $"Mutation unexpectedly passed: {document}");
                Assert.That(result.RuleId, Is.EqualTo(GasRuntimeV1SupportProfile.RuleId));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// 调用 public adapter 校验一组 raw 文档。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult Validate(RawSources sources)
        {
            return RuntimeV1RawAuthoringSupportProfileAdapter.ValidateFiles(
                sources.Ability,
                sources.Effect,
                sources.Attribute,
                sources.Sidecar);
        }

        /// <summary>
        /// 修改第一条 AutoChess Ability 的 direct Effect ID token。
        /// </summary>
        private static void AbilityIds(JToken token, JToken ids)
        {
            Ability(token)["AbilityExecution"]["Param"]["IDs"] = ids;
        }

        /// <summary>
        /// 返回第一条 AutoChess Ability raw row。
        /// </summary>
        private static JObject Ability(JToken token)
        {
            return ((JArray)token).OfType<JObject>()
                .First(row => ((string)row["Name"]).StartsWith("AutoChess", StringComparison.Ordinal));
        }

        /// <summary>
        /// 返回 9203 稳定名称对应的 raw row。
        /// </summary>
        private static JObject Poison(JToken token)
        {
            return ((JArray)token).OfType<JObject>()
                .Single(row => (string)row["Name"] == "AutoChessPlayerPoison");
        }

        /// <summary>
        /// 返回 AutoChessCombat 中指定 ID 的 raw Attribute row。
        /// </summary>
        private static JObject CombatAttribute(JToken token, int attributeId)
        {
            var set = ((JArray)token).OfType<JObject>()
                .Single(row => (string)row["Name"] == "AutoChessCombat");
            return ((JArray)set["Attribute"]).OfType<JObject>()
                .Single(row => (int)row["ID"] == attributeId);
        }

        /// <summary>
        /// 返回 sidecar 中指定位置的 unit 对象。
        /// </summary>
        private static JObject Unit(JToken token, int index)
        {
            return (JObject)((JArray)token["units"])[index];
        }

        /// <summary>
        /// 区分 mutation 应写入的 raw 文档。
        /// </summary>
        private enum RawDocument : byte
        {
            Ability = 1,
            Effect = 2,
            Attribute = 3,
            Sidecar = 4,
        }

        /// <summary>
        /// 保存 production 或临时复制的四个 raw 文档绝对路径。
        /// </summary>
        private readonly struct RawSources
        {
            public RawSources(string ability, string effect, string attribute, string sidecar)
            {
                Ability = ability;
                Effect = effect;
                Attribute = attribute;
                Sidecar = sidecar;
            }

            public string Ability { get; }
            public string Effect { get; }
            public string Attribute { get; }
            public string Sidecar { get; }
            public string[] Paths => new[] { Ability, Effect, Attribute, Sidecar };

            /// <summary>
            /// 从 Unity project root 解析官方 Luban JSON 与 authoritative sidecar。
            /// </summary>
            public static RawSources Find()
            {
                var projectRoot = FindProjectRoot();
                var luban = Path.Combine(projectRoot, "Assets/DataGenerated/Luban/Json/GAS");
                return new RawSources(
                    Path.Combine(luban, "exgas_tbability.json"),
                    Path.Combine(luban, "exgas_tbgameplayeffect.json"),
                    Path.Combine(luban, "exgas_tbattributeset.json"),
                    Path.Combine(projectRoot,
                        "EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/AutoChessDemo/autochess.sourcegen.json"));
            }

            /// <summary>
            /// 将四个 raw 文档复制到隔离 mutation 目录。
            /// </summary>
            public RawSources CopyTo(string directory)
            {
                var copy = new RawSources(
                    Path.Combine(directory, Path.GetFileName(Ability)),
                    Path.Combine(directory, Path.GetFileName(Effect)),
                    Path.Combine(directory, Path.GetFileName(Attribute)),
                    Path.Combine(directory, Path.GetFileName(Sidecar)));
                for (var index = 0; index < Paths.Length; index++)
                    File.Copy(Paths[index], copy.Paths[index]);
                return copy;
            }

            /// <summary>
            /// 返回指定 raw 文档路径。
            /// </summary>
            public string GetPath(RawDocument document)
            {
                return document switch
                {
                    RawDocument.Ability => Ability,
                    RawDocument.Effect => Effect,
                    RawDocument.Attribute => Attribute,
                    RawDocument.Sidecar => Sidecar,
                    _ => throw new ArgumentOutOfRangeException(nameof(document), document, null),
                };
            }

            /// <summary>
            /// 向上查找包含 ProjectSettings 的 Unity project root。
            /// </summary>
            private static string FindProjectRoot()
            {
                var current = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (current != null)
                {
                    if (File.Exists(System.IO.Path.Combine(current.FullName, "ProjectSettings/ProjectVersion.txt")))
                        return current.FullName;
                    current = current.Parent;
                }
                throw new DirectoryNotFoundException("Unity project root was not found.");
            }
        }
    }
}
