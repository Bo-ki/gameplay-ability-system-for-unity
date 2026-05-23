using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.AutoChess
{
    public sealed class HeadlessAutoChessGeneratedDefinitionSourceTests
    {
        private const int ExpectedAbilityCount = 7;
        private const int ExpectedGameplayEffectCount = 33;
        private const int ExpectedAttributeSetCount = 1;
        private const int ExpectedAttributeCount = 6;
        private const int ExpectedGameplayTagCount = 19;
        private const int ExpectedGameplayCueCount = 1;
        private const int ExpectedTimelineCount = 7;
        private const int ExpectedSummonRowCount = 1;
        private const int ExpectedManagedPresentationDeferredEntryCount = 27;
        private const int ExpectedDeferredBoundaryCount = 108;
        private const int ExpectedGeneratedPackageRowCount =
            ExpectedAbilityCount
            + ExpectedGameplayEffectCount
            + ExpectedAttributeSetCount
            + ExpectedAttributeCount
            + ExpectedGameplayTagCount
            + ExpectedGameplayCueCount
            + ExpectedTimelineCount
            + ExpectedSummonRowCount;
        private const int ExpectedDefinitionCount =
            ExpectedAbilityCount
            + ExpectedGameplayEffectCount
            + ExpectedAttributeSetCount
            + ExpectedAttributeCount
            + ExpectedGameplayTagCount
            + ExpectedGameplayCueCount;

        [SetUp]
        public void SetUp()
        {
            ConfigRegistryDiagnostics.Clear();
            HeadlessAutoChessDefinitionSource.RegisterRuntimeProviders();
        }

        [TearDown]
        public void TearDown()
        {
            HeadlessAutoChessDefinitionSource.ClearRuntimeProviders();
            ConfigRegistryDiagnostics.Clear();
        }

        [Test]
        public void GeneratedRowsBuildSourceGeneratorStyleSnapshotWithoutRuntimeState()
        {
            var snapshot = HeadlessAutoChessDefinitionSource.CreateGeneratedRegistrySnapshot();
            var source = snapshot.CreateDefinitionSource();

            Assert.That(snapshot.AbilityRowCount, Is.EqualTo(ExpectedAbilityCount));
            Assert.That(snapshot.GameplayEffectRowCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(snapshot.AttributeSetRowCount, Is.EqualTo(ExpectedAttributeSetCount));
            Assert.That(snapshot.AttributeRowCount, Is.EqualTo(ExpectedAttributeCount));
            Assert.That(snapshot.GameplayTagRowCount, Is.EqualTo(ExpectedGameplayTagCount));
            Assert.That(snapshot.GameplayCueRowCount, Is.EqualTo(ExpectedGameplayCueCount));
            Assert.That(snapshot.TimelineRowCount, Is.EqualTo(ExpectedTimelineCount));
            Assert.That(snapshot.SummonRowCount, Is.EqualTo(ExpectedSummonRowCount));

            Assert.That(source.AbilityCodes, Is.EqualTo(HeadlessAutoChessDefinitionSource.CreateAbilityCodes()));
            Assert.That(source.GameplayEffectCodes, Is.EqualTo(HeadlessAutoChessDefinitionSource.CreateGameplayEffectCodes()));
            Assert.That(source.GameplayCueCodes, Is.EqualTo(HeadlessAutoChessDefinitionSource.CreateGameplayCueCodes()));
            Assert.That(source.TimelineIds, Is.EqualTo(HeadlessAutoChessDefinitionSource.CreateTimelineIds()));
            Assert.That(source.GameplayTags.Count, Is.EqualTo(ExpectedGameplayTagCount));
            Assert.That(source.AttributeSetConfigs.Count, Is.EqualTo(ExpectedAttributeSetCount));
            Assert.That(source.AttributeSetConfigs[0].Code, Is.EqualTo(HeadlessAutoChessScenario.AttributeSetCombat));
            Assert.That(source.AttributeSetConfigs[0].Settings, Has.Length.EqualTo(ExpectedAttributeCount));

            Assert.That(
                snapshot.TryFindAbilityRow(
                    HeadlessAutoChessScenario.AbilityPlayerManaBurst,
                    out var manaBurst),
                Is.True);
            Assert.That(manaBurst.HasCost, Is.True);
            Assert.That(manaBurst.HasCooldown, Is.True);
            Assert.That(manaBurst.HasTimeline, Is.True);
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerManaBurstDamage,
                    out var manaBurstDamage),
                Is.True);
            Assert.That(manaBurstDamage.HasDamageType, Is.True);
            Assert.That(manaBurstDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypeArcane));
            Assert.That(manaBurstDamage.HasResistanceCapture, Is.True);
            Assert.That(
                manaBurstDamage.ResistanceAttributeCode,
                Is.EqualTo(HeadlessAutoChessScenario.AttributeArcaneResistance));

            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectArcaneStormDebuff,
                    out var debuff),
                Is.True);
            Assert.That(debuff.HasDuration, Is.True);
            Assert.That(debuff.HasPeriod, Is.True);
            Assert.That(debuff.HasGrantedTag, Is.True);
            Assert.That(debuff.HasGameplayCue, Is.True);

            Assert.That(
                snapshot.TryFindAbilityRow(
                    HeadlessAutoChessScenario.AbilityPlayerControlStun,
                    out var controlStun),
                Is.True);
            Assert.That(controlStun.HasCooldown, Is.True);
            Assert.That(controlStun.HasTimeline, Is.True);
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerStun,
                    out var stun),
                Is.True);
            Assert.That(stun.HasDuration, Is.True);
            Assert.That(stun.HasGrantedTag, Is.True);
            Assert.That(stun.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessStunned));

            Assert.That(
                snapshot.TryFindAbilityRow(
                    HeadlessAutoChessScenario.AbilityPlayerBarrier,
                    out var barrier),
                Is.True);
            Assert.That(barrier.HasCooldown, Is.True);
            Assert.That(barrier.HasTimeline, Is.True);
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierShield,
                    out var shield),
                Is.True);
            Assert.That(shield.HasModifier, Is.True);
            Assert.That(shield.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeShield));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierStatus,
                    out var shieldStatus),
                Is.True);
            Assert.That(shieldStatus.HasDuration, Is.True);
            Assert.That(shieldStatus.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessShielded));

            Assert.That(
                snapshot.TryFindAbilityRow(
                    HeadlessAutoChessScenario.AbilityPlayerSummon,
                    out var summon),
                Is.True);
            Assert.That(summon.HasCooldown, Is.True);
            Assert.That(summon.HasTimeline, Is.True);
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonRequest,
                    out var summonRequest),
                Is.True);
            Assert.That(summonRequest.HasDuration, Is.True);
            Assert.That(summonRequest.HasGameplayCue, Is.True);
            Assert.That(
                snapshot.TryFindSummonRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonRequest,
                    out var summonRow),
                Is.True);
            Assert.That(summonRow.PrimaryAbilityCode, Is.EqualTo(HeadlessAutoChessScenario.AbilityPlayerStrike));
            Assert.That(summonRow.FixedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessSummoned));

            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCounterGear,
                    out var counterGear),
                Is.True);
            Assert.That(counterGear.HasDuration, Is.True);
            Assert.That(counterGear.HasModifier, Is.True);
            Assert.That(counterGear.HasGrantedTag, Is.True);
            Assert.That(counterGear.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeCounterDamage));
            Assert.That(counterGear.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessCounterReady));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCounterDamage,
                    out var counterDamage),
                Is.True);
            Assert.That(counterDamage.HasDamageType, Is.True);
            Assert.That(counterDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypePhysical));
            Assert.That(counterDamage.HasSetByCallerMagnitude, Is.True);
            Assert.That(
                counterDamage.ModifierMagnitudeKey,
                Is.EqualTo(HeadlessAutoChessScenario.SetByCallerCounterDamageAmount));

            Assert.That(
                snapshot.TryFindAbilityRow(
                    HeadlessAutoChessScenario.AbilityPlayerCleanse,
                    out var cleanse),
                Is.True);
            Assert.That(cleanse.HasCooldown, Is.True);
            Assert.That(cleanse.HasTimeline, Is.True);
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanse,
                    out var cleanseEffect),
                Is.True);
            Assert.That(cleanseEffect.HasRemoveGameplayEffectsWithTags, Is.True);
            Assert.That(
                cleanseEffect.RemoveGameplayEffectTagCode,
                Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessStunned));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanseRally,
                    out var cleanseRally),
                Is.True);
            Assert.That(cleanseRally.HasDuration, Is.True);
            Assert.That(cleanseRally.HasModifier, Is.True);
            Assert.That(cleanseRally.HasGrantedTag, Is.True);
            Assert.That(cleanseRally.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeMana));
            Assert.That(cleanseRally.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerCleanseRallyManaAmount));
            Assert.That(cleanseRally.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessCleanseRallied));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerRallyComboDamage,
                    out var rallyComboDamage),
                Is.True);
            Assert.That(rallyComboDamage.HasModifier, Is.True);
            Assert.That(rallyComboDamage.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeHealth));
            Assert.That(rallyComboDamage.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerRallyComboDamageAmount));
            Assert.That(rallyComboDamage.HasDamageType, Is.True);
            Assert.That(rallyComboDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypePhysical));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerLifeStealGear,
                    out var lifeStealGear),
                Is.True);
            Assert.That(lifeStealGear.HasDuration, Is.True);
            Assert.That(lifeStealGear.HasModifier, Is.True);
            Assert.That(lifeStealGear.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeLifeStealRatio));
            Assert.That(lifeStealGear.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessLifeStealReady));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerLifeStealHeal,
                    out var lifeStealHeal),
                Is.True);
            Assert.That(lifeStealHeal.HasModifier, Is.True);
            Assert.That(lifeStealHeal.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeHealth));
            Assert.That(lifeStealHeal.HasSetByCallerMagnitude, Is.True);
            Assert.That(lifeStealHeal.ModifierMagnitudeKey, Is.EqualTo(HeadlessAutoChessScenario.SetByCallerLifeStealHealAmount));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonStack,
                    out var poisonStack),
                Is.True);
            Assert.That(poisonStack.HasDuration, Is.True);
            Assert.That(poisonStack.HasPeriod, Is.True);
            Assert.That(poisonStack.PeriodFrames, Is.EqualTo(HeadlessAutoChessScenario.PlayerPoisonPeriodFrames));
            Assert.That(poisonStack.PeriodGameplayEffectCode, Is.EqualTo(HeadlessAutoChessScenario.GameplayEffectPlayerPoisonPeriodDamage));
            Assert.That(poisonStack.HasGrantedTag, Is.True);
            Assert.That(poisonStack.HasStacking, Is.True);
            Assert.That(poisonStack.StackingCode, Is.EqualTo(HeadlessAutoChessScenario.PoisonStackingCode));
            Assert.That(poisonStack.StackLimitCount, Is.EqualTo(HeadlessAutoChessScenario.PlayerPoisonStackLimit));
            Assert.That(poisonStack.StackType, Is.EqualTo(EffectStackType.AggregateByTarget));
            Assert.That(poisonStack.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessPoisoned));
            Assert.That(poisonStack.OverflowGameplayEffectCode, Is.EqualTo(HeadlessAutoChessScenario.GameplayEffectPlayerPoisonOverflowDamage));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonOverflowDamage,
                    out var poisonOverflowDamage),
                Is.True);
            Assert.That(poisonOverflowDamage.HasModifier, Is.True);
            Assert.That(poisonOverflowDamage.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeHealth));
            Assert.That(poisonOverflowDamage.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerPoisonOverflowDamageAmount));
            Assert.That(poisonOverflowDamage.HasDamageType, Is.True);
            Assert.That(poisonOverflowDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypePoison));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonPeriodDamage,
                    out var poisonPeriodDamage),
                Is.True);
            Assert.That(poisonPeriodDamage.HasModifier, Is.True);
            Assert.That(poisonPeriodDamage.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeHealth));
            Assert.That(poisonPeriodDamage.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerPoisonPeriodDamageAmount));
            Assert.That(poisonPeriodDamage.HasDamageType, Is.True);
            Assert.That(poisonPeriodDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypePoison));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerExecuteGear,
                    out var executeGear),
                Is.True);
            Assert.That(executeGear.HasDuration, Is.True);
            Assert.That(executeGear.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessExecutionReady));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerExecuteDamage,
                    out var executeDamage),
                Is.True);
            Assert.That(executeDamage.HasDuration, Is.True);
            Assert.That(executeDamage.HasModifier, Is.True);
            Assert.That(executeDamage.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeHealth));
            Assert.That(executeDamage.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerExecuteDamageAmount));
            Assert.That(executeDamage.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessExecuted));
            Assert.That(executeDamage.HasDamageType, Is.True);
            Assert.That(executeDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypeExecute));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstGear,
                    out var deathBurstGear),
                Is.True);
            Assert.That(deathBurstGear.HasDuration, Is.True);
            Assert.That(deathBurstGear.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessDeathBurstReady));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstDamage,
                    out var deathBurstDamage),
                Is.True);
            Assert.That(deathBurstDamage.HasModifier, Is.True);
            Assert.That(deathBurstDamage.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeHealth));
            Assert.That(deathBurstDamage.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerDeathBurstDamageAmount));
            Assert.That(deathBurstDamage.HasDamageType, Is.True);
            Assert.That(deathBurstDamage.DamageTypeCode, Is.EqualTo(HeadlessAutoChessScenario.DamageTypeDeathBurst));
            Assert.That(
                snapshot.TryFindGameplayEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerEnrage,
                    out var enrage),
                Is.True);
            Assert.That(enrage.HasDuration, Is.True);
            Assert.That(enrage.HasModifier, Is.True);
            Assert.That(enrage.ModifierAttributeCode, Is.EqualTo(HeadlessAutoChessScenario.AttributeCounterDamage));
            Assert.That(enrage.ModifierMagnitude, Is.EqualTo(HeadlessAutoChessScenario.PlayerEnrageCounterDamageBonus));
            Assert.That(enrage.HasGrantedTag, Is.True);
            Assert.That(enrage.GrantedTagCode, Is.EqualTo(HeadlessAutoChessScenario.TagAutoChessEnraged));

            AssertGeneratedRowContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageAddsLubanSourceGeneratorManifestAndValidation()
        {
            var package = HeadlessAutoChessDefinitionSource.CreateGeneratedDefinitionPackage();
            var validation = package.Validate();
            var manifest = package.Manifest;
            var snapshot = package.Snapshot;
            var source = package.CreateDefinitionSource();

            Assert.That(validation.Passed, Is.True, validation.FailureReason);
            Assert.That(validation.HasGeneratorIdentity, Is.True);
            Assert.That(validation.RowCountsMatch, Is.True);
            Assert.That(validation.ContentHashMatches, Is.True);
            Assert.That(validation.ManifestTotalRowCount, Is.EqualTo(ExpectedGeneratedPackageRowCount));
            Assert.That(validation.SnapshotTotalRowCount, Is.EqualTo(ExpectedGeneratedPackageRowCount));
            Assert.That(validation.ExpectedContentHash, Is.EqualTo(validation.ActualContentHash));
            Assert.That(validation.FailureReason, Is.Empty);

            Assert.That(manifest.SourceKind, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionSourceKind.LubanSourceGenerator));
            Assert.That(manifest.PackageName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedPackageName));
            Assert.That(manifest.SchemaVersion, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedSchemaVersion));
            Assert.That(manifest.GeneratorName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedSourceGeneratorName));
            Assert.That(manifest.SourceRevision, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedSourceRevision));
            Assert.That(manifest.AbilityRowCount, Is.EqualTo(ExpectedAbilityCount));
            Assert.That(manifest.GameplayEffectRowCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(manifest.AttributeSetRowCount, Is.EqualTo(ExpectedAttributeSetCount));
            Assert.That(manifest.AttributeRowCount, Is.EqualTo(ExpectedAttributeCount));
            Assert.That(manifest.GameplayTagRowCount, Is.EqualTo(ExpectedGameplayTagCount));
            Assert.That(manifest.GameplayCueRowCount, Is.EqualTo(ExpectedGameplayCueCount));
            Assert.That(manifest.TimelineRowCount, Is.EqualTo(ExpectedTimelineCount));
            Assert.That(manifest.SummonRowCount, Is.EqualTo(ExpectedSummonRowCount));
            Assert.That(
                manifest.ContentHash,
                Is.EqualTo(HeadlessAutoChessGeneratedDefinitionPackage.CalculateContentHash(snapshot)));

            Assert.That(source.AbilityCodes.Count, Is.EqualTo(ExpectedAbilityCount));
            Assert.That(source.GameplayEffectCodes.Count, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(source.AttributeSetConfigs.Count, Is.EqualTo(ExpectedAttributeSetCount));
            Assert.That(source.GameplayTags.Count, Is.EqualTo(ExpectedGameplayTagCount));
            Assert.That(source.GameplayCueCodes.Count, Is.EqualTo(ExpectedGameplayCueCount));
            Assert.That(source.TimelineIds.Count, Is.EqualTo(ExpectedTimelineCount));

            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageCreatesSourceGeneratorOutputBoundary()
        {
            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var validation = output.Validate();
            var manifest = output.Package.Manifest;

            Assert.That(validation.Passed, Is.True, validation.FailureReason);
            Assert.That(validation.PackageValidationPassed, Is.True);
            Assert.That(validation.HasOutputIdentity, Is.True);
            Assert.That(validation.SourceTextHashMatches, Is.True);
            Assert.That(validation.ManifestTextHashMatches, Is.True);
            Assert.That(validation.SourceTextContainsManifestFingerprint, Is.True);
            Assert.That(validation.WritesDefinitionPlaneOnly, Is.True);
            Assert.That(validation.FailureReason, Is.Empty);

            Assert.That(
                output.RuntimeSourceRelativePath,
                Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeSourceRelativePath));
            Assert.That(
                output.ManifestRelativePath,
                Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedManifestRelativePath));
            Assert.That(output.RuntimeSourceRelativePath, Does.StartWith("Assets/DataGenerated/Luban/"));
            Assert.That(output.ManifestRelativePath, Does.StartWith("Assets/DataGenerated/Luban/"));
            Assert.That(output.GeneratedNamespace, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeNamespace));
            Assert.That(output.GeneratedTypeName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeTypeName));
            Assert.That(output.SourceTextHash, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(output.SourceText)));
            Assert.That(output.ManifestTextHash, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(output.ManifestText)));
            Assert.That(output.EmitsDefinitionPackage, Is.True);
            Assert.That(output.EmitsRuntimeLifecycle, Is.False);
            Assert.That(output.EmitsPresentationBehavior, Is.False);
            Assert.That(output.EmitsEditorTypes, Is.False);
            Assert.That(output.WritesDefinitionPlaneOnly, Is.True);

            Assert.That(output.SourceText, Does.Contain(manifest.PackageName));
            Assert.That(output.SourceText, Does.Contain(manifest.SchemaVersion));
            Assert.That(output.SourceText, Does.Contain(manifest.GeneratorName));
            Assert.That(output.SourceText, Does.Contain(manifest.SourceRevision));
            Assert.That(output.SourceText, Does.Contain(manifest.TotalRowCount.ToString(CultureInfo.InvariantCulture)));
            Assert.That(output.SourceText, Does.Contain(manifest.ContentHash.ToString(CultureInfo.InvariantCulture)));
            Assert.That(output.SourceText, Does.Contain(output.ManifestTextHash.ToString(CultureInfo.InvariantCulture)));
            Assert.That(output.SourceText, Does.Contain("CreatePackage"));

            Assert.That(output.ManifestText, Does.Contain("\"packageName\""));
            Assert.That(output.ManifestText, Does.Contain("\"runtimeSourceRelativePath\""));
            Assert.That(output.ManifestText, Does.Contain("\"manifestRelativePath\""));
            Assert.That(output.ManifestText, Does.Contain("\"emitsRuntimeLifecycle\": false"));
            Assert.That(output.ManifestText, Does.Contain("\"emitsPresentationBehavior\": false"));
            Assert.That(output.ManifestText, Does.Contain("\"emitsEditorTypes\": false"));

            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(output.SourceText);
            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(output.ManifestText);
            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageWritesSourceGeneratorOutputArtifactsToProjectRoot()
        {
            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-S",
                "GeneratedProjectRoot"));
            Directory.CreateDirectory(projectRoot);

            var plan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan.Create(output, projectRoot);
            var validation = plan.Validate();

            Assert.That(validation.Passed, Is.True, validation.FailureReason);
            Assert.That(validation.OutputValidationPassed, Is.True);
            Assert.That(validation.HasProjectRoot, Is.True);
            Assert.That(validation.HasResolvedPaths, Is.True);
            Assert.That(validation.PathsStayUnderProjectRoot, Is.True);
            Assert.That(validation.SourceAndManifestAreDistinct, Is.True);
            Assert.That(validation.WritesDefinitionPlaneOnly, Is.True);
            Assert.That(validation.FailureReason, Is.Empty);

            Assert.That(plan.ProjectRoot, Is.EqualTo(projectRoot));
            Assert.That(plan.PlannedFileCount, Is.EqualTo(2));
            Assert.That(plan.RuntimeSourceAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(plan.ManifestAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(plan.RuntimeSourceAbsolutePath, Does.EndWith("HeadlessAutoChessGeneratedDefinitionPackage.g.cs"));
            Assert.That(plan.ManifestAbsolutePath, Does.EndWith("HeadlessAutoChessGeneratedDefinitionPackage.manifest.json"));

            var result = plan.WriteFiles();

            Assert.That(result.Passed, Is.True, result.FailureReason);
            Assert.That(result.PlanValidationPassed, Is.True);
            Assert.That(result.PlannedFileCount, Is.EqualTo(2));
            Assert.That(result.WrittenFileCount, Is.EqualTo(2));
            Assert.That(result.WroteExpectedFileCount, Is.True);
            Assert.That(result.RuntimeSourceFile.Passed, Is.True);
            Assert.That(result.ManifestFile.Passed, Is.True);
            Assert.That(result.RuntimeSourceFile.RelativePath, Is.EqualTo(output.RuntimeSourceRelativePath));
            Assert.That(result.ManifestFile.RelativePath, Is.EqualTo(output.ManifestRelativePath));
            Assert.That(result.RuntimeSourceFile.ExpectedTextHash, Is.EqualTo(output.SourceTextHash));
            Assert.That(result.RuntimeSourceFile.ActualTextHash, Is.EqualTo(output.SourceTextHash));
            Assert.That(result.ManifestFile.ExpectedTextHash, Is.EqualTo(output.ManifestTextHash));
            Assert.That(result.ManifestFile.ActualTextHash, Is.EqualTo(output.ManifestTextHash));
            Assert.That(result.RuntimeSourceFile.ByteCount, Is.GreaterThan(0));
            Assert.That(result.ManifestFile.ByteCount, Is.GreaterThan(0));
            Assert.That(File.Exists(result.RuntimeSourceFile.AbsolutePath), Is.True);
            Assert.That(File.Exists(result.ManifestFile.AbsolutePath), Is.True);
            Assert.That(File.ReadAllText(result.RuntimeSourceFile.AbsolutePath), Is.EqualTo(output.SourceText));
            Assert.That(File.ReadAllText(result.ManifestFile.AbsolutePath), Is.EqualTo(output.ManifestText));

            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(output.SourceText);
            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(output.ManifestText);
            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageRejectsUnsafeSourceGeneratorExportPaths()
        {
            var package = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorPackage();
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-S",
                "UnsafeProjectRoot"));
            Directory.CreateDirectory(projectRoot);

            var escapedOutput = HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.Create(
                package,
                "../Escaped/HeadlessAutoChessGeneratedDefinitionPackage.g.cs",
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedManifestRelativePath,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeNamespace,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeTypeName);
            var escapedPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan.Create(
                escapedOutput,
                projectRoot);
            var escapedValidation = escapedPlan.Validate();

            Assert.That(escapedOutput.Validate().Passed, Is.True);
            Assert.That(escapedValidation.Passed, Is.False);
            Assert.That(escapedValidation.OutputValidationPassed, Is.True);
            Assert.That(escapedValidation.HasProjectRoot, Is.True);
            Assert.That(escapedValidation.HasResolvedPaths, Is.True);
            Assert.That(escapedValidation.PathsStayUnderProjectRoot, Is.False);
            Assert.That(escapedValidation.SourceAndManifestAreDistinct, Is.True);
            Assert.That(escapedValidation.WritesDefinitionPlaneOnly, Is.True);
            Assert.That(
                escapedValidation.FailureReason,
                Is.EqualTo("Generated source export plan paths must stay under project root."));

            var escapedResult = escapedPlan.WriteFiles();

            Assert.That(escapedResult.Passed, Is.False);
            Assert.That(escapedResult.PlanValidationPassed, Is.False);
            Assert.That(escapedResult.WrittenFileCount, Is.EqualTo(0));
            Assert.That(escapedResult.RuntimeSourceFile.Exists, Is.False);
            Assert.That(escapedResult.ManifestFile.Exists, Is.False);

            var absoluteOutput = HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.Create(
                package,
                Path.GetFullPath(Path.Combine(projectRoot, "HeadlessAutoChessGeneratedDefinitionPackage.g.cs")),
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedManifestRelativePath,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeNamespace,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeTypeName);
            var absolutePlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan.Create(
                absoluteOutput,
                projectRoot);
            var absoluteValidation = absolutePlan.Validate();

            Assert.That(absoluteOutput.Validate().Passed, Is.True);
            Assert.That(absoluteValidation.Passed, Is.False);
            Assert.That(absoluteValidation.OutputValidationPassed, Is.True);
            Assert.That(absoluteValidation.HasProjectRoot, Is.True);
            Assert.That(absoluteValidation.HasResolvedPaths, Is.False);
            Assert.That(absoluteValidation.PathsStayUnderProjectRoot, Is.False);
            Assert.That(
                absoluteValidation.FailureReason,
                Is.EqualTo("Generated source export plan could not resolve output paths."));
            Assert.That(absolutePlan.WriteFiles().WrittenFileCount, Is.EqualTo(0));

            var missingProjectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-S",
                "MissingProjectRoot",
                Guid.NewGuid().ToString("N")));
            var missingPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan.Create(
                HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput(),
                missingProjectRoot);
            var missingValidation = missingPlan.Validate();

            Assert.That(Directory.Exists(missingProjectRoot), Is.False);
            Assert.That(missingValidation.Passed, Is.False);
            Assert.That(missingValidation.OutputValidationPassed, Is.True);
            Assert.That(missingValidation.HasProjectRoot, Is.False);
            Assert.That(missingValidation.FailureReason, Is.EqualTo("Generated source export plan is missing project root."));
            Assert.That(missingPlan.WriteFiles().WrittenFileCount, Is.EqualTo(0));
            Assert.That(Directory.Exists(missingProjectRoot), Is.False);
        }

        [Test]
        public void GeneratedPackageExecutesSourceGeneratorToolchainPlan()
        {
            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-T",
                "ToolchainProjectRoot"));
            var configSourceRelativePath = "EX_GAS_Config/ProjectConfigTable/exgas_config";
            Directory.CreateDirectory(projectRoot);
            Directory.CreateDirectory(Path.Combine(projectRoot, configSourceRelativePath));

            var plan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                configSourceRelativePath);
            var validation = plan.Validate();

            Assert.That(validation.Passed, Is.True, validation.FailureReason);
            Assert.That(validation.UsesLubanSourceGeneratorPackage, Is.True);
            Assert.That(validation.HasToolchainIdentity, Is.True);
            Assert.That(validation.HasConfigSourceRoot, Is.True);
            Assert.That(validation.ConfigSourceStaysUnderProjectRoot, Is.True);
            Assert.That(validation.OutputValidationPassed, Is.True);
            Assert.That(validation.ExportPlanValidationPassed, Is.True);
            Assert.That(validation.WritesDefinitionPlaneOnly, Is.True);
            Assert.That(validation.FailureReason, Is.Empty);

            Assert.That(plan.ToolchainName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedToolchainName));
            Assert.That(plan.ConfigSourceRelativePath, Is.EqualTo(configSourceRelativePath));
            Assert.That(plan.ConfigSourceAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(plan.ExportPlan.ProjectRoot, Is.EqualTo(projectRoot));
            Assert.That(plan.ExportPlan.RuntimeSourceAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(plan.ExportPlan.ManifestAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(plan.PlannedStepCount, Is.EqualTo(3));

            var result = plan.Execute();

            Assert.That(result.Passed, Is.True, result.FailureReason);
            Assert.That(result.PlanValidationPassed, Is.True);
            Assert.That(result.PackageValidationPassed, Is.True);
            Assert.That(result.OutputValidationPassed, Is.True);
            Assert.That(result.ExportResultPassed, Is.True);
            Assert.That(result.PlannedStepCount, Is.EqualTo(3));
            Assert.That(result.ExecutedStepCount, Is.EqualTo(3));
            Assert.That(result.ExecutedExpectedStepCount, Is.True);
            Assert.That(result.PackageContentHash, Is.EqualTo(output.Package.Manifest.ContentHash));
            Assert.That(result.OutputSourceTextHash, Is.EqualTo(output.SourceTextHash));
            Assert.That(result.OutputManifestTextHash, Is.EqualTo(output.ManifestTextHash));
            Assert.That(result.ExportResult.Passed, Is.True);
            Assert.That(result.ExportResult.WrittenFileCount, Is.EqualTo(2));
            Assert.That(File.Exists(result.ExportResult.RuntimeSourceFile.AbsolutePath), Is.True);
            Assert.That(File.Exists(result.ExportResult.ManifestFile.AbsolutePath), Is.True);
            Assert.That(File.ReadAllText(result.ExportResult.RuntimeSourceFile.AbsolutePath), Is.EqualTo(output.SourceText));
            Assert.That(File.ReadAllText(result.ExportResult.ManifestFile.AbsolutePath), Is.EqualTo(output.ManifestText));

            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(output.SourceText);
            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(output.ManifestText);
            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageBuildsAuthoringSnapshotFromToolchainResult()
        {
            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-U",
                "AuthoringSnapshotProjectRoot"));
            var configSourceRelativePath = "EX_GAS_Config/ProjectConfigTable/exgas_config";
            Directory.CreateDirectory(projectRoot);
            Directory.CreateDirectory(Path.Combine(projectRoot, configSourceRelativePath));

            var plan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                configSourceRelativePath);
            var pendingSnapshot = HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot.Create(
                plan,
                default);

            Assert.That(
                pendingSnapshot.Status,
                Is.EqualTo(HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.PendingExecution));
            Assert.That(pendingSnapshot.ReadyForAuthoring, Is.False);
            Assert.That(pendingSnapshot.ValidationPassed, Is.True);
            Assert.That(pendingSnapshot.PlanValidationPassed, Is.True);
            Assert.That(pendingSnapshot.PackageValidationPassed, Is.True);
            Assert.That(pendingSnapshot.OutputValidationPassed, Is.True);
            Assert.That(pendingSnapshot.ExportPlanValidationPassed, Is.True);
            Assert.That(pendingSnapshot.ToolchainResultPassed, Is.False);
            Assert.That(pendingSnapshot.TotalRowCount, Is.EqualTo(ExpectedGeneratedPackageRowCount));
            Assert.That(pendingSnapshot.PlannedStepCount, Is.EqualTo(3));
            Assert.That(pendingSnapshot.ExecutedStepCount, Is.EqualTo(0));
            Assert.That(pendingSnapshot.PlannedFileCount, Is.EqualTo(2));
            Assert.That(pendingSnapshot.WrittenFileCount, Is.EqualTo(0));
            Assert.That(pendingSnapshot.RuntimeSourceFileExists, Is.False);
            Assert.That(pendingSnapshot.ManifestFileExists, Is.False);
            Assert.That(pendingSnapshot.RuntimeSourceRelativePath, Is.EqualTo(output.RuntimeSourceRelativePath));
            Assert.That(pendingSnapshot.ManifestRelativePath, Is.EqualTo(output.ManifestRelativePath));
            Assert.That(pendingSnapshot.RuntimeSourceAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(pendingSnapshot.ManifestAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(pendingSnapshot.FormatSummary(), Does.Contain("status=PendingExecution"));

            var result = plan.Execute();
            var snapshot = HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot.Create(plan, result);
            var summary = snapshot.FormatSummary();

            Assert.That(snapshot.Status, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.Ready));
            Assert.That(snapshot.ReadyForAuthoring, Is.True, summary);
            Assert.That(snapshot.ValidationPassed, Is.True);
            Assert.That(snapshot.ArtifactFilesReady, Is.True);
            Assert.That(snapshot.ArtifactHashesMatch, Is.True);
            Assert.That(snapshot.SourceKind, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionSourceKind.LubanSourceGenerator));
            Assert.That(snapshot.ToolchainName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedToolchainName));
            Assert.That(snapshot.PackageName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedPackageName));
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedSchemaVersion));
            Assert.That(snapshot.GeneratorName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedSourceGeneratorName));
            Assert.That(snapshot.SourceRevision, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedSourceRevision));
            Assert.That(snapshot.ConfigSourceRelativePath, Is.EqualTo(configSourceRelativePath));
            Assert.That(snapshot.ConfigSourceAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(snapshot.ProjectRoot, Is.EqualTo(projectRoot));
            Assert.That(snapshot.GeneratedNamespace, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeNamespace));
            Assert.That(snapshot.GeneratedTypeName, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeTypeName));
            Assert.That(snapshot.AbilityRowCount, Is.EqualTo(ExpectedAbilityCount));
            Assert.That(snapshot.GameplayEffectRowCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(snapshot.AttributeSetRowCount, Is.EqualTo(ExpectedAttributeSetCount));
            Assert.That(snapshot.AttributeRowCount, Is.EqualTo(ExpectedAttributeCount));
            Assert.That(snapshot.GameplayTagRowCount, Is.EqualTo(ExpectedGameplayTagCount));
            Assert.That(snapshot.GameplayCueRowCount, Is.EqualTo(ExpectedGameplayCueCount));
            Assert.That(snapshot.TimelineRowCount, Is.EqualTo(ExpectedTimelineCount));
            Assert.That(snapshot.SummonRowCount, Is.EqualTo(ExpectedSummonRowCount));
            Assert.That(snapshot.TotalRowCount, Is.EqualTo(ExpectedGeneratedPackageRowCount));
            Assert.That(snapshot.PackageContentHash, Is.EqualTo(output.Package.Manifest.ContentHash));
            Assert.That(snapshot.SourceTextHash, Is.EqualTo(output.SourceTextHash));
            Assert.That(snapshot.ManifestTextHash, Is.EqualTo(output.ManifestTextHash));
            Assert.That(snapshot.ExecutedStepCount, Is.EqualTo(3));
            Assert.That(snapshot.ExecutedExpectedStepCount, Is.True);
            Assert.That(snapshot.WrittenFileCount, Is.EqualTo(2));
            Assert.That(snapshot.RuntimeSourceByteCount, Is.GreaterThan(0));
            Assert.That(snapshot.ManifestByteCount, Is.GreaterThan(0));
            Assert.That(snapshot.ConfigSourceRootExists, Is.True);
            Assert.That(snapshot.ConfigSourceStaysUnderProjectRoot, Is.True);
            Assert.That(snapshot.WritesDefinitionPlaneOnly, Is.True);
            Assert.That(snapshot.RuntimeSourceFileHashMatches, Is.True);
            Assert.That(snapshot.ManifestFileHashMatches, Is.True);
            Assert.That(snapshot.FailureReason, Is.Empty);
            Assert.That(summary, Does.Contain("status=Ready"));
            Assert.That(summary, Does.Contain("rows=" + ExpectedGeneratedPackageRowCount.ToString(CultureInfo.InvariantCulture)));
            Assert.That(summary, Does.Contain("runtimeSource=" + output.RuntimeSourceRelativePath));
            Assert.That(summary, Does.Contain("manifest=" + output.ManifestRelativePath));

            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageRunsRealLubanProcessBeforeSourceGeneratorExport()
        {
            var projectRoot = Path.GetFullPath(".");
            var lubanCodeOutput = "Logs/AutoChess/T6-CHESS-V/LubanProcess/code";
            var lubanDataOutput = "Logs/AutoChess/T6-CHESS-V/LubanProcess/json";
            var sourceOutput = HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.Create(
                HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorPackage(),
                "Logs/AutoChess/T6-CHESS-V/GeneratedPackage/HeadlessAutoChessGeneratedDefinitionPackage.g.cs",
                "Logs/AutoChess/T6-CHESS-V/GeneratedPackage/HeadlessAutoChessGeneratedDefinitionPackage.manifest.json",
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeNamespace,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedRuntimeTypeName);
            var lubanProcessPlan = HeadlessAutoChessGeneratedDefinitionLubanProcessPlan.Create(
                projectRoot,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedConfigSourceRelativePath,
                lubanCodeOutput,
                lubanDataOutput);
            var toolchainPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                sourceOutput,
                projectRoot,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedConfigSourceRelativePath);
            var processToolchainPlan =
                HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan.Create(
                    lubanProcessPlan,
                    toolchainPlan);

            var validation = lubanProcessPlan.Validate();

            Assert.That(validation.Passed, Is.True, validation.FailureReason);
            Assert.That(validation.HasProjectRoot, Is.True);
            Assert.That(validation.HasDotNetExecutable, Is.True);
            Assert.That(validation.HasLubanDll, Is.True);
            Assert.That(validation.HasConfigSourceRoot, Is.True);
            Assert.That(validation.HasLubanConf, Is.True);
            Assert.That(validation.InputPathsStayUnderProjectRoot, Is.True);
            Assert.That(validation.HasOutputRoots, Is.True);
            Assert.That(validation.OutputPathsStayUnderProjectRoot, Is.True);
            Assert.That(validation.OutputPathsAreDistinct, Is.True);
            Assert.That(validation.UsesDefinitionGenerationTargets, Is.True);
            Assert.That(lubanProcessPlan.BuildArgumentString(), Does.Contain("Luban.dll"));
            Assert.That(lubanProcessPlan.OutputCodeAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(lubanProcessPlan.OutputDataAbsolutePath, Does.StartWith(projectRoot));
            Assert.That(toolchainPlan.Validate().Passed, Is.True);

            var result = processToolchainPlan.Execute();
            var snapshot = result.AuthoringSnapshot;

            Assert.That(result.Passed, Is.True, result.FailureReason);
            Assert.That(result.LubanProcessResultPassed, Is.True);
            Assert.That(result.LubanProcessResult.Passed, Is.True, result.LubanProcessResult.FailureReason);
            Assert.That(result.LubanProcessResult.PlanValidationPassed, Is.True);
            Assert.That(result.LubanProcessResult.ProcessStarted, Is.True);
            Assert.That(result.LubanProcessResult.ProcessExited, Is.True);
            Assert.That(result.LubanProcessResult.ExitCode, Is.EqualTo(0));
            Assert.That(result.LubanProcessResult.GeneratedCodeFileCount, Is.GreaterThan(0));
            Assert.That(result.LubanProcessResult.GeneratedDataFileCount, Is.GreaterThan(0));
            Assert.That(result.LubanProcessResult.GeneratedFileCount, Is.GreaterThan(0));
            Assert.That(result.LubanProcessResult.GeneratedByteCount, Is.GreaterThan(0));
            Assert.That(result.LubanProcessResult.GeneratedOutputHash, Is.Not.EqualTo(0));
            Assert.That(result.ToolchainResult.Passed, Is.True, result.ToolchainResult.FailureReason);
            Assert.That(result.ToolchainResult.ExecutedStepCount, Is.EqualTo(3));
            Assert.That(snapshot.Status, Is.EqualTo(HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.Ready));
            Assert.That(snapshot.ReadyForAuthoring, Is.True, snapshot.FormatSummary());
            Assert.That(snapshot.RuntimeSourceRelativePath, Is.EqualTo(sourceOutput.RuntimeSourceRelativePath));
            Assert.That(snapshot.ManifestRelativePath, Is.EqualTo(sourceOutput.ManifestRelativePath));
            Assert.That(snapshot.RuntimeSourceFileExists, Is.True);
            Assert.That(snapshot.ManifestFileExists, Is.True);
            Assert.That(snapshot.ArtifactHashesMatch, Is.True);
            Assert.That(File.Exists(snapshot.RuntimeSourceAbsolutePath), Is.True);
            Assert.That(File.Exists(snapshot.ManifestAbsolutePath), Is.True);

            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(sourceOutput.SourceText);
            AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(sourceOutput.ManifestText);
            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageBlocksSourceGeneratorWhenLubanProcessValidationFails()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-V",
                "BlockedLubanProcessProjectRoot"));
            var configSourceRelativePath =
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedConfigSourceRelativePath;
            var configRoot = Path.Combine(projectRoot, configSourceRelativePath);
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
            Directory.CreateDirectory(configRoot);
            File.WriteAllText(Path.Combine(configRoot, "luban.conf"), "{}", System.Text.Encoding.UTF8);

            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var lubanProcessPlan = HeadlessAutoChessGeneratedDefinitionLubanProcessPlan.Create(
                projectRoot,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedDotNetExecutablePath,
                "Missing/Luban.dll",
                configSourceRelativePath,
                "Logs/AutoChess/T6-CHESS-V/Blocked/code",
                "Logs/AutoChess/T6-CHESS-V/Blocked/json");
            var toolchainPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                configSourceRelativePath);
            var processToolchainPlan =
                HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan.Create(
                    lubanProcessPlan,
                    toolchainPlan);

            var validation = lubanProcessPlan.Validate();
            var result = processToolchainPlan.Execute();

            Assert.That(validation.Passed, Is.False);
            Assert.That(validation.HasProjectRoot, Is.True);
            Assert.That(validation.HasDotNetExecutable, Is.True);
            Assert.That(validation.HasLubanDll, Is.False);
            Assert.That(validation.HasConfigSourceRoot, Is.True);
            Assert.That(validation.HasLubanConf, Is.True);
            Assert.That(validation.FailureReason, Is.EqualTo("Luban process plan is missing Luban dll."));
            Assert.That(result.Passed, Is.False);
            Assert.That(result.LubanProcessResultPassed, Is.False);
            Assert.That(result.LubanProcessResult.PlanValidationPassed, Is.False);
            Assert.That(result.LubanProcessResult.ProcessStarted, Is.False);
            Assert.That(result.ToolchainResult.ExecutedStepCount, Is.EqualTo(0));
            Assert.That(result.AuthoringSnapshot.Status, Is.EqualTo(
                HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.PendingExecution));
            Assert.That(result.FailureReason, Is.EqualTo("Luban process plan is missing Luban dll."));
            Assert.That(File.Exists(toolchainPlan.ExportPlan.RuntimeSourceAbsolutePath), Is.False);
            Assert.That(File.Exists(toolchainPlan.ExportPlan.ManifestAbsolutePath), Is.False);

            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedPackageRejectsUnsafeSourceGeneratorToolchainInputs()
        {
            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-T",
                "UnsafeToolchainProjectRoot"));
            Directory.CreateDirectory(projectRoot);

            var missingConfigPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                "EX_GAS_Config/Missing");
            var missingConfigValidation = missingConfigPlan.Validate();

            Assert.That(missingConfigValidation.Passed, Is.False);
            Assert.That(missingConfigValidation.UsesLubanSourceGeneratorPackage, Is.True);
            Assert.That(missingConfigValidation.HasToolchainIdentity, Is.True);
            Assert.That(missingConfigValidation.HasConfigSourceRoot, Is.False);
            Assert.That(
                missingConfigValidation.FailureReason,
                Is.EqualTo("Source generator toolchain plan is missing config source root."));
            Assert.That(missingConfigPlan.Execute().ExecutedStepCount, Is.EqualTo(0));
            Assert.That(File.Exists(missingConfigPlan.ExportPlan.RuntimeSourceAbsolutePath), Is.False);

            var escapedConfigRelativePath = "../EscapedConfigRoot";
            var escapedConfigRoot = Path.GetFullPath(Path.Combine(projectRoot, escapedConfigRelativePath));
            Directory.CreateDirectory(escapedConfigRoot);
            var escapedConfigPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                escapedConfigRelativePath);
            var escapedConfigValidation = escapedConfigPlan.Validate();

            Assert.That(Directory.Exists(escapedConfigPlan.ConfigSourceAbsolutePath), Is.True);
            Assert.That(escapedConfigValidation.Passed, Is.False);
            Assert.That(escapedConfigValidation.HasConfigSourceRoot, Is.True);
            Assert.That(escapedConfigValidation.ConfigSourceStaysUnderProjectRoot, Is.False);
            Assert.That(
                escapedConfigValidation.FailureReason,
                Is.EqualTo("Source generator toolchain plan config source root must stay under project root."));
            Assert.That(escapedConfigPlan.Execute().ExecutedStepCount, Is.EqualTo(0));

            var absoluteConfigRoot = Path.GetFullPath(Path.Combine(projectRoot, "AbsoluteConfigRoot"));
            Directory.CreateDirectory(absoluteConfigRoot);
            var absoluteConfigPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                absoluteConfigRoot);
            var absoluteConfigValidation = absoluteConfigPlan.Validate();

            Assert.That(absoluteConfigValidation.Passed, Is.False);
            Assert.That(absoluteConfigValidation.HasConfigSourceRoot, Is.False);
            Assert.That(
                absoluteConfigValidation.FailureReason,
                Is.EqualTo("Source generator toolchain plan is missing config source root."));
            Assert.That(absoluteConfigPlan.Execute().ExecutedStepCount, Is.EqualTo(0));
        }

        [Test]
        public void GeneratedPackageAuthoringSnapshotReportsBlockedToolchainValidation()
        {
            var output = HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorOutput();
            var projectRoot = Path.GetFullPath(Path.Combine(
                "Logs",
                "AutoChess",
                "T6-CHESS-U",
                "BlockedAuthoringSnapshotProjectRoot"));
            Directory.CreateDirectory(projectRoot);

            var plan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan.Create(
                output,
                projectRoot,
                "EX_GAS_Config/Missing");
            var result = plan.Execute();
            var snapshot = HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot.Create(plan, result);

            Assert.That(
                snapshot.Status,
                Is.EqualTo(HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.BlockedByValidation));
            Assert.That(snapshot.ReadyForAuthoring, Is.False);
            Assert.That(snapshot.ValidationPassed, Is.False);
            Assert.That(snapshot.PlanValidationPassed, Is.False);
            Assert.That(snapshot.PackageValidationPassed, Is.True);
            Assert.That(snapshot.OutputValidationPassed, Is.True);
            Assert.That(snapshot.ExportPlanValidationPassed, Is.True);
            Assert.That(snapshot.ConfigSourceRootExists, Is.False);
            Assert.That(snapshot.ConfigSourceStaysUnderProjectRoot, Is.True);
            Assert.That(snapshot.WritesDefinitionPlaneOnly, Is.True);
            Assert.That(snapshot.ToolchainResultPassed, Is.False);
            Assert.That(snapshot.ExecutedStepCount, Is.EqualTo(0));
            Assert.That(snapshot.WrittenFileCount, Is.EqualTo(0));
            Assert.That(snapshot.RuntimeSourceFileExists, Is.False);
            Assert.That(snapshot.ManifestFileExists, Is.False);
            Assert.That(snapshot.TotalRowCount, Is.EqualTo(ExpectedGeneratedPackageRowCount));
            Assert.That(
                snapshot.FailureReason,
                Is.EqualTo("Source generator toolchain plan is missing config source root."));
            Assert.That(snapshot.FormatSummary(), Does.Contain("status=BlockedByValidation"));

            AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes();
        }

        [Test]
        public void GeneratedSourceCoversAutoChessDefinitionSurface()
        {
            var source = HeadlessAutoChessDefinitionSource.CreateGeneratedSource();

            var result = GASDefinitionGeneratedAdapter.Build(source);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.RegistryDiagnosticCount, Is.EqualTo(0));
            Assert.That(result.ValidationDiagnosticCount, Is.EqualTo(0));
            Assert.That(result.WarmupResult.AbilityConfigCount, Is.EqualTo(ExpectedAbilityCount));
            Assert.That(result.WarmupResult.GameplayEffectConfigCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(result.WarmupResult.TimelineConfigCount, Is.EqualTo(ExpectedTimelineCount));
            Assert.That(result.DefinitionTable.Abilities.Count, Is.EqualTo(ExpectedAbilityCount));
            Assert.That(result.DefinitionTable.GameplayEffects.Count, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(result.DefinitionTable.AttributeSets.Count, Is.EqualTo(ExpectedAttributeSetCount));
            Assert.That(result.DefinitionTable.Attributes.Count, Is.EqualTo(ExpectedAttributeCount));
            Assert.That(result.DefinitionTable.GameplayTags.Count, Is.EqualTo(ExpectedGameplayTagCount));
            Assert.That(result.DefinitionTable.GameplayCues.Count, Is.EqualTo(ExpectedGameplayCueCount));
            Assert.That(result.DefinitionTable.TotalDefinitionCount, Is.EqualTo(ExpectedDefinitionCount));

            Assert.That(
                result.DefinitionTable.TryGetAbility(
                    HeadlessAutoChessScenario.AbilityPlayerManaBurst,
                    out var manaBurst),
                Is.True);
            Assert.That(manaBurst.HasCost, Is.True);
            Assert.That(manaBurst.HasCooldown, Is.True);
            Assert.That(manaBurst.HasTimeline, Is.True);
            Assert.That(manaBurst.TimelineId, Is.EqualTo(HeadlessAutoChessScenario.TimelinePlayerManaBurst));

            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectArcaneStormDebuff,
                    out var debuff),
                Is.True);
            Assert.That(debuff.HasDuration, Is.True);
            Assert.That(debuff.HasPeriod, Is.True);
            Assert.That(debuff.PeriodEffectCount, Is.EqualTo(1));
            Assert.That(debuff.HasManagedCueTriggers, Is.True);

            Assert.That(
                result.DefinitionTable.TryGetAttribute(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeHealth,
                    out var health),
                Is.True);
            Assert.That(health.HasClamp, Is.True);
            Assert.That(health.MaxValue, Is.EqualTo(100f));
            Assert.That(
                result.DefinitionTable.TryGetAttribute(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeShield,
                    out var shield),
                Is.True);
            Assert.That(shield.HasClamp, Is.True);
            Assert.That(shield.MaxValue, Is.EqualTo(HeadlessAutoChessScenario.MaxShieldAmount));
            Assert.That(
                result.DefinitionTable.TryGetAttribute(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeArcaneResistance,
                    out var arcaneResistance),
                Is.True);
            Assert.That(arcaneResistance.HasClamp, Is.True);
            Assert.That(arcaneResistance.MaxValue, Is.EqualTo(HeadlessAutoChessScenario.MaxArcaneResistance));
            Assert.That(
                result.DefinitionTable.TryGetAttribute(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeLifeStealRatio,
                    out var lifeStealRatio),
                Is.True);
            Assert.That(lifeStealRatio.HasClamp, Is.True);
            Assert.That(lifeStealRatio.MaxValue, Is.EqualTo(HeadlessAutoChessScenario.MaxLifeStealRatio));
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagArcaneTeamBuff, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessStunned, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessShielded, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessSummoned, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagPlayerSummonCooldown, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagPlayerCleanseCooldown, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessCleanseRallied, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessLifeStealReady, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessPoisoned, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessExecutionReady, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessExecuted, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessDeathBurstReady, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayTag(HeadlessAutoChessScenario.TagAutoChessEnraged, out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanse,
                    out var cleanseEffect),
                Is.True);
            Assert.That(cleanseEffect.HasRemoveGameplayEffectsWithTags, Is.True);
            Assert.That(cleanseEffect.RemoveGameplayEffectsWithTags.Any.HasTag(HeadlessAutoChessScenario.TagAutoChessStunned), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanseRally,
                    out var cleanseRally),
                Is.True);
            Assert.That(cleanseRally.HasGrantedTags, Is.True);
            Assert.That(cleanseRally.GrantedTags.HasTag(HeadlessAutoChessScenario.TagAutoChessCleanseRallied), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerRallyComboDamage,
                    out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerLifeStealHeal,
                    out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonStack,
                    out var poisonStack),
                Is.True);
            Assert.That(poisonStack.HasDuration, Is.True);
            Assert.That(poisonStack.HasPeriod, Is.True);
            Assert.That(poisonStack.PeriodEffectCount, Is.EqualTo(1));
            Assert.That(poisonStack.HasGrantedTags, Is.True);
            Assert.That(poisonStack.GrantedTags.HasTag(HeadlessAutoChessScenario.TagAutoChessPoisoned), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonOverflowDamage,
                    out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonPeriodDamage,
                    out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerExecuteGear,
                    out var executeGear),
                Is.True);
            Assert.That(executeGear.HasGrantedTags, Is.True);
            Assert.That(executeGear.GrantedTags.HasTag(HeadlessAutoChessScenario.TagAutoChessExecutionReady), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerExecuteDamage,
                    out var executeDamage),
                Is.True);
            Assert.That(executeDamage.HasGrantedTags, Is.True);
            Assert.That(executeDamage.GrantedTags.HasTag(HeadlessAutoChessScenario.TagAutoChessExecuted), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstGear,
                    out var deathBurstGear),
                Is.True);
            Assert.That(deathBurstGear.HasGrantedTags, Is.True);
            Assert.That(deathBurstGear.GrantedTags.HasTag(HeadlessAutoChessScenario.TagAutoChessDeathBurstReady), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstDamage,
                    out _),
                Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayEffect(
                    HeadlessAutoChessScenario.GameplayEffectPlayerEnrage,
                    out var enrage),
                Is.True);
            Assert.That(enrage.HasGrantedTags, Is.True);
            Assert.That(enrage.GrantedTags.HasTag(HeadlessAutoChessScenario.TagAutoChessEnraged), Is.True);
            Assert.That(
                result.DefinitionTable.TryGetGameplayCue(
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    out var cue),
                Is.True);
            Assert.That(cue.UsesManagedPresentationFactory, Is.True);
        }

        [Test]
        public void GeneratedSourceRunsThroughBakePipelineAndRuntimeIntegrationPlan()
        {
            var buildResult = GASDefinitionGeneratedAdapter.Build(
                HeadlessAutoChessDefinitionSource.CreateGeneratedSource());
            var bakingPlan = GASGeneratedDefinitionBakingPlanner.Create(
                buildResult,
                GameplayEffectConfigRegistry.GetDefinitionCacheState());
            var contract = GASGeneratedDefinitionBakeContractPlanner.Create(bakingPlan);
            var bakeResult = GASGeneratedDefinitionBakePipeline.Create(contract);
            var integrationPlan = GASGeneratedDefinitionRuntimeIntegrationPlanner.CreateCurrent(bakeResult);

            Assert.That(buildResult.HasErrors, Is.False);
            Assert.That(bakingPlan.CanBake, Is.True);
            Assert.That(bakingPlan.HasGameplayEffectConfigProvider, Is.True);
            Assert.That(bakingPlan.EntryCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(bakingPlan.BakerInputCandidateCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(bakingPlan.StaticDefinitionBlobCandidateCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(bakingPlan.RuntimeLifecycleDeferredEntryCount, Is.EqualTo(ExpectedAbilityCount + ExpectedGameplayEffectCount));
            Assert.That(
                bakingPlan.ManagedPresentationDeferredEntryCount,
                Is.EqualTo(ExpectedManagedPresentationDeferredEntryCount));
            Assert.That(bakingPlan.RuntimeTimelineDeferredCount, Is.EqualTo(ExpectedTimelineCount * 2));

            Assert.That(contract.CanRunUnityBaker, Is.True);
            Assert.That(contract.UnityBakerInputWriteCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(contract.StaticDefinitionBlobCacheWriteCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(contract.ArchetypeTemplateCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(contract.DeferredBoundaryWriteCount, Is.EqualTo(ExpectedDeferredBoundaryCount));

            Assert.That(bakeResult.CanRunUnityBaker, Is.True);
            Assert.That(bakeResult.BakerInputCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(bakeResult.StaticBlobCacheRequestCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(bakeResult.RuntimeArchetypeCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(bakeResult.DeferredBoundaryCount, Is.EqualTo(ExpectedDeferredBoundaryCount));

            Assert.That(integrationPlan.CanRunUnityBaker, Is.True);
            Assert.That(integrationPlan.BlockedEntryCount, Is.EqualTo(0));
            Assert.That(integrationPlan.CanIntegrateRuntimeStaticArchetypes, Is.True);
            Assert.That(integrationPlan.UnityBakerInputEntryCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(integrationPlan.StaticBlobCacheEntryCount, Is.EqualTo(ExpectedGameplayEffectCount));
            Assert.That(integrationPlan.RuntimeArchetypeEntryCount, Is.EqualTo(ExpectedDefinitionCount));
            Assert.That(integrationPlan.DeferredBoundaryEntryCount, Is.EqualTo(ExpectedDeferredBoundaryCount));
            Assert.That(integrationPlan.ManagedPresentationBoundaryCount, Is.GreaterThan(0));
            Assert.That(integrationPlan.StructuralSemanticDecisionCount, Is.GreaterThan(0));
            Assert.That(integrationPlan.DirtyPipelineCandidateCount, Is.GreaterThan(0));

            Assert.That(
                integrationPlan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.Ability,
                    HeadlessAutoChessScenario.AbilityPlayerManaBurst,
                    out var manaBurst),
                Is.True);
            Assert.That(manaBurst.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary));
            Assert.That(manaBurst.HasSlot(GASGeneratedDefinitionArchetypeSlot.TimelineReferenceKey), Is.True);
            Assert.That(
                manaBurst.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.RuntimeTimelineDeferred),
                Is.True);

            Assert.That(
                integrationPlan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.GameplayEffect,
                    HeadlessAutoChessScenario.GameplayEffectArcaneStormDebuff,
                    out var debuff),
                Is.True);
            Assert.That(debuff.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ReadyWithDeferredRuntimeBoundary));
            Assert.That(debuff.HasSlot(GASGeneratedDefinitionArchetypeSlot.StaticDefinitionBlobReferenceSlot), Is.True);
            Assert.That(debuff.HasSlot(GASGeneratedDefinitionArchetypeSlot.PresentationCueKey), Is.True);
            Assert.That(
                debuff.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.GameplayEffectCacheLifecycleOwner),
                Is.True);
            Assert.That(
                debuff.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentationDeferred),
                Is.True);

            Assert.That(
                integrationPlan.TryFindEntry(
                    GASGeneratedDefinitionRuntimeIntegrationTarget.RuntimeStaticArchetype,
                    GASDefinitionKind.GameplayCue,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    out var cue),
                Is.True);
            Assert.That(cue.Status, Is.EqualTo(GASGeneratedDefinitionRuntimeIntegrationStatus.ManagedPresentationBoundary));
            Assert.That(
                cue.HasIntegrationBoundary(GASGeneratedDefinitionRuntimeIntegrationBoundary.ManagedPresentation),
                Is.True);
        }

        public static void RunGeneratedSourceValidationFromCommandLine()
        {
            var tests = new HeadlessAutoChessGeneratedDefinitionSourceTests();
            tests.SetUp();
            try
            {
                tests.GeneratedRowsBuildSourceGeneratorStyleSnapshotWithoutRuntimeState();
                tests.GeneratedPackageAddsLubanSourceGeneratorManifestAndValidation();
                tests.GeneratedPackageCreatesSourceGeneratorOutputBoundary();
                tests.GeneratedPackageWritesSourceGeneratorOutputArtifactsToProjectRoot();
                tests.GeneratedPackageRejectsUnsafeSourceGeneratorExportPaths();
                tests.GeneratedPackageExecutesSourceGeneratorToolchainPlan();
                tests.GeneratedPackageBuildsAuthoringSnapshotFromToolchainResult();
                tests.GeneratedPackageRunsRealLubanProcessBeforeSourceGeneratorExport();
                tests.GeneratedPackageBlocksSourceGeneratorWhenLubanProcessValidationFails();
                tests.GeneratedPackageRejectsUnsafeSourceGeneratorToolchainInputs();
                tests.GeneratedPackageAuthoringSnapshotReportsBlockedToolchainValidation();
                tests.GeneratedSourceCoversAutoChessDefinitionSurface();
                tests.GeneratedSourceRunsThroughBakePipelineAndRuntimeIntegrationPlan();
                UnityEngine.Debug.Log("HeadlessAutoChessGeneratedDefinitionSourceValidation passed.");
            }
            finally
            {
                tests.TearDown();
            }
        }

        private static void AssertGeneratedPackageContractDoesNotExposeRuntimeStateTypes()
        {
            var publicContractTypes = new[]
            {
                typeof(HeadlessAutoChessGeneratedDefinitionSourceKind),
                typeof(HeadlessAutoChessGeneratedDefinitionManifest),
                typeof(HeadlessAutoChessGeneratedDefinitionPackageValidation),
                typeof(HeadlessAutoChessGeneratedDefinitionPackage),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutputValidation),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlanValidation),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan),
                typeof(HeadlessAutoChessGeneratedDefinitionLubanProcessPlanValidation),
                typeof(HeadlessAutoChessGeneratedDefinitionLubanProcessResult),
                typeof(HeadlessAutoChessGeneratedDefinitionLubanProcessPlan),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainResult),
                typeof(HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan),
                typeof(HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus),
                typeof(HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot),
            };

            for (var i = 0; i < publicContractTypes.Length; i++)
                AssertTypeDoesNotExposeRuntimeState(publicContractTypes[i]);
        }

        private static void AssertGeneratedRowContractDoesNotExposeRuntimeStateTypes()
        {
            var publicContractTypes = new[]
            {
                typeof(HeadlessAutoChessGeneratedRegistrySnapshot),
                typeof(HeadlessAutoChessAbilityDefinitionRow),
                typeof(HeadlessAutoChessGameplayEffectDefinitionRow),
                typeof(HeadlessAutoChessAttributeSetDefinitionRow),
                typeof(HeadlessAutoChessAttributeDefinitionRow),
                typeof(HeadlessAutoChessGameplayTagDefinitionRow),
                typeof(HeadlessAutoChessGameplayCueDefinitionRow),
                typeof(HeadlessAutoChessTimelineDefinitionRow),
                typeof(HeadlessAutoChessSummonDefinitionRow),
                typeof(HeadlessAutoChessGeneratedDefinitionRows),
            };

            for (var i = 0; i < publicContractTypes.Length; i++)
                AssertTypeDoesNotExposeRuntimeState(publicContractTypes[i]);
        }

        private static void AssertTypeDoesNotExposeRuntimeState(Type type)
        {
            const BindingFlags memberFlags =
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly;

            var fields = type.GetFields(memberFlags);
            for (var i = 0; i < fields.Length; i++)
                AssertNoForbiddenRuntimeType(fields[i].FieldType, type.FullName + "." + fields[i].Name);

            var properties = type.GetProperties(memberFlags);
            for (var i = 0; i < properties.Length; i++)
                AssertNoForbiddenRuntimeType(properties[i].PropertyType, type.FullName + "." + properties[i].Name);
        }

        private static void AssertNoForbiddenRuntimeType(Type type, string memberName)
        {
            if (type == null)
                return;

            if (type.IsArray)
            {
                AssertNoForbiddenRuntimeType(type.GetElementType(), memberName);
                return;
            }

            Assert.That(type, Is.Not.EqualTo(typeof(Entity)), memberName);
            Assert.That(type, Is.Not.EqualTo(typeof(EntityManager)), memberName);
            Assert.That(type, Is.Not.EqualTo(typeof(EntityQuery)), memberName);
            Assert.That(type, Is.Not.EqualTo(typeof(EntityCommandBuffer)), memberName);

            var typeName = type.FullName ?? type.Name;
            Assert.That(typeName.Contains("BlobAssetReference"), Is.False, memberName);
            Assert.That(typeName.Contains("SimpleJSON"), Is.False, memberName);
            Assert.That(typeName.Contains("XLuban"), Is.False, memberName);
            Assert.That(typeName.Contains(".cfg."), Is.False, memberName);

            if (!type.IsGenericType)
                return;

            var arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
                AssertNoForbiddenRuntimeType(arguments[i], memberName);
        }

        private static void AssertGeneratedOutputTextDoesNotExposeForbiddenRuntimeTypes(string text)
        {
            Assert.That(text, Does.Not.Contain("Unity.Entities"));
            Assert.That(text, Does.Not.Contain("EntityManager"));
            Assert.That(text, Does.Not.Contain("EntityQuery"));
            Assert.That(text, Does.Not.Contain("EntityCommandBuffer"));
            Assert.That(text, Does.Not.Contain("BlobAssetReference"));
            Assert.That(text, Does.Not.Contain("SimpleJSON"));
            Assert.That(text, Does.Not.Contain("XLuban"));
            Assert.That(text, Does.Not.Contain(".cfg."));
            Assert.That(text, Does.Not.Contain("Sirenix"));
        }
    }
}
