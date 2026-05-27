using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GAS.Runtime;

namespace GAS.Editor
{
    internal sealed class AutoChessAttributeComponentPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "AutoChessAttributeComponent";

        public override IReadOnlyList<string> OutputFileNames { get; } =
            new[] { "Runtime/AutoChessAttributeComponents.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            var attributeRow = AutoChessCodeGenData.FindRow(context, "Attribute");
            var attributes = AutoChessCodeGenData.BuildAttributeDefinitions(context, attributeRow);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("public struct AutoChessAttributeDefinition");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int AttributeSetCode;");
            writer.WriteLine("public int AttributeCode;");
            writer.WriteLine("public float InitialValue;");
            writer.WriteLine("public bool IsClampMin;");
            writer.WriteLine("public bool IsClampMax;");
            writer.WriteLine("public float MinValue;");
            writer.WriteLine("public float MaxValue;");
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var attribute in attributes)
                WriteAttributeComponent(writer, attribute);

            WriteAttributeCodes(writer, attributes);
            WriteAttributeAccessor(writer, attributes);

            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimeManifest(manifest, PhaseName, path);
        }

        private static void WriteAttributeComponent(IndentedWriter writer, AutoChessAttributeDefinition attribute)
        {
            writer.WriteLine("");
            writer.WriteLine($"public struct {attribute.ComponentTypeName} : IComponentData");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int AttributeSetCode = {attribute.AttributeSetCode};");
            writer.WriteLine($"public const int AttributeCode = {attribute.AttributeCode};");
            writer.WriteLine("");
            writer.WriteLine("public float BaseValue;");
            writer.WriteLine("public float CurrentValue;");
            writer.WriteLine("public float PreviousCurrentValue;");
            writer.WriteLine("public bool IsClampMin;");
            writer.WriteLine("public bool IsClampMax;");
            writer.WriteLine("public float MinValue;");
            writer.WriteLine("public float MaxValue;");
            writer.WriteLine("public bool Dirty;");
            writer.WriteLine("public bool CurrentValueChangePending;");
            writer.WriteLine("");
            writer.WriteLine($"public static {attribute.ComponentTypeName} Create(float initialValue = {AutoChessCodeGenData.FloatLiteral(attribute.InitialValue)})");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"var value = AutoChessAttributeAccessor.Clamp(AttributeCode, initialValue);");
            writer.WriteLine($"return new {attribute.ComponentTypeName}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("BaseValue = value,");
            writer.WriteLine("CurrentValue = value,");
            writer.WriteLine("PreviousCurrentValue = value,");
            writer.WriteLine($"IsClampMin = {AutoChessCodeGenData.BoolLiteral(attribute.IsClampMin)},");
            writer.WriteLine($"IsClampMax = {AutoChessCodeGenData.BoolLiteral(attribute.IsClampMax)},");
            writer.WriteLine($"MinValue = {AutoChessCodeGenData.FloatLiteral(attribute.MinValue)},");
            writer.WriteLine($"MaxValue = {AutoChessCodeGenData.FloatLiteral(attribute.MaxValue)},");
            writer.WriteLine("Dirty = true,");
            writer.WriteLine("CurrentValueChangePending = true,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteAttributeCodes(
            IndentedWriter writer,
            IReadOnlyList<AutoChessAttributeDefinition> attributes)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessAttributeCodes");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var attribute in attributes)
                writer.WriteLine($"public const int {attribute.SymbolName} = {attribute.AttributeCode};");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteAttributeAccessor(
            IndentedWriter writer,
            IReadOnlyList<AutoChessAttributeDefinition> attributes)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessAttributeAccessor");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int DefinitionCount = {attributes.Count};");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetDefinition(int attributeCode, out AutoChessAttributeDefinition definition)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (attributeCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var attribute in attributes)
            {
                writer.WriteLine($"case AutoChessAttributeCodes.{attribute.SymbolName}:");
                writer.Indent++;
                writer.WriteLine("definition = new AutoChessAttributeDefinition");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"AttributeSetCode = {attribute.AttributeSetCode},");
                writer.WriteLine($"AttributeCode = {attribute.AttributeCode},");
                writer.WriteLine($"InitialValue = {AutoChessCodeGenData.FloatLiteral(attribute.InitialValue)},");
                writer.WriteLine($"IsClampMin = {AutoChessCodeGenData.BoolLiteral(attribute.IsClampMin)},");
                writer.WriteLine($"IsClampMax = {AutoChessCodeGenData.BoolLiteral(attribute.IsClampMax)},");
                writer.WriteLine($"MinValue = {AutoChessCodeGenData.FloatLiteral(attribute.MinValue)},");
                writer.WriteLine($"MaxValue = {AutoChessCodeGenData.FloatLiteral(attribute.MaxValue)},");
                writer.Indent--;
                writer.WriteLine("};");
                writer.WriteLine("return true;");
                writer.Indent--;
            }

            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("definition = default;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static float Clamp(int attributeCode, float value)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!TryGetDefinition(attributeCode, out var definition)) return value;");
            writer.WriteLine("if (definition.IsClampMin && value < definition.MinValue) value = definition.MinValue;");
            writer.WriteLine("if (definition.IsClampMax && value > definition.MaxValue) value = definition.MaxValue;");
            writer.WriteLine("return value;");
            writer.Indent--;
            writer.WriteLine("}");
            WriteGetCurrentValue(writer, attributes);
            WriteSetCurrentValue(writer, attributes);
            WriteTryGetComponentType(writer, attributes);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteGetCurrentValue(
            IndentedWriter writer,
            IReadOnlyList<AutoChessAttributeDefinition> attributes)
        {
            writer.WriteLine("");
            var parameters = string.Join(
                ", ",
                attributes.Select(attribute => $"in {attribute.ComponentTypeName} {attribute.ParameterName}"));
            writer.WriteLine(string.IsNullOrEmpty(parameters)
                ? "public static float GetCurrentValue(int attributeCode)"
                : $"public static float GetCurrentValue(int attributeCode, {parameters})");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (attributeCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var attribute in attributes)
                writer.WriteLine($"case AutoChessAttributeCodes.{attribute.SymbolName}: return {attribute.ParameterName}.CurrentValue;");
            writer.WriteLine("default: return 0f;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteSetCurrentValue(
            IndentedWriter writer,
            IReadOnlyList<AutoChessAttributeDefinition> attributes)
        {
            writer.WriteLine("");
            var parameters = string.Join(
                ", ",
                attributes.Select(attribute => $"ref {attribute.ComponentTypeName} {attribute.ParameterName}"));
            writer.WriteLine(string.IsNullOrEmpty(parameters)
                ? "public static bool SetCurrentValue(int attributeCode, float value)"
                : $"public static bool SetCurrentValue(int attributeCode, {parameters}, float value)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (attributeCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var attribute in attributes)
            {
                writer.WriteLine($"case AutoChessAttributeCodes.{attribute.SymbolName}:");
                writer.Indent++;
                writer.WriteLine($"{attribute.ParameterName}.PreviousCurrentValue = {attribute.ParameterName}.CurrentValue;");
                writer.WriteLine($"{attribute.ParameterName}.CurrentValue = Clamp(attributeCode, value);");
                writer.WriteLine($"{attribute.ParameterName}.CurrentValueChangePending = {attribute.ParameterName}.CurrentValue != {attribute.ParameterName}.PreviousCurrentValue;");
                writer.WriteLine($"{attribute.ParameterName}.Dirty = true;");
                writer.WriteLine("return true;");
                writer.Indent--;
            }

            writer.WriteLine("default: return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteTryGetComponentType(
            IndentedWriter writer,
            IReadOnlyList<AutoChessAttributeDefinition> attributes)
        {
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetComponentType(int attributeCode, out ComponentType componentType)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (attributeCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var attribute in attributes)
            {
                writer.WriteLine($"case AutoChessAttributeCodes.{attribute.SymbolName}:");
                writer.Indent++;
                writer.WriteLine($"componentType = ComponentType.ReadWrite<{attribute.ComponentTypeName}>();");
                writer.WriteLine("return true;");
                writer.Indent--;
            }
            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("componentType = default;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    internal sealed class AutoChessTagMaskPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "AutoChessTagMask";

        public override IReadOnlyList<string> OutputFileNames { get; } =
            new[] { "Runtime/AutoChessTagMasks.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            var tagRow = AutoChessCodeGenData.FindRow(context, "GameplayTag");
            var tags = AutoChessCodeGenData.BuildTagDefinitions(context, tagRow);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            WriteTagCodes(writer, tags);
            WriteTagBits(writer, tags);
            WriteTagMaskTable(writer, tags);
            WriteTagChecks(writer, tags);

            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimeManifest(manifest, PhaseName, path);
        }

        private static void WriteTagCodes(IndentedWriter writer, IReadOnlyList<AutoChessTagDefinition> tags)
        {
            writer.WriteLine("public static class AutoChessTagCodes");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var tag in tags)
                writer.WriteLine($"public const int {tag.SymbolName} = {tag.TagCode};");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteTagBits(IndentedWriter writer, IReadOnlyList<AutoChessTagDefinition> tags)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessTagBits");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var tag in tags)
            {
                writer.WriteLine($"public const int {tag.SymbolName}Index = {tag.DenseIndex};");
                writer.WriteLine($"public const int {tag.SymbolName}Block = {tag.DenseIndex / 64};");
                writer.WriteLine($"public const ulong {tag.SymbolName}Mask = 1UL << {tag.DenseIndex & 63};");
            }
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteTagMaskTable(IndentedWriter writer, IReadOnlyList<AutoChessTagDefinition> tags)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessTagMaskTable");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int Count = {tags.Count};");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetDenseIndex(int tagCode, out int denseIndex)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (tagCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var tag in tags)
                writer.WriteLine($"case AutoChessTagCodes.{tag.SymbolName}: denseIndex = AutoChessTagBits.{tag.SymbolName}Index; return true;");
            writer.WriteLine("default: denseIndex = -1; return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryCreateMask(int tagCode, out CTagMask mask)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("mask = default;");
            writer.WriteLine("if (!TryGetDenseIndex(tagCode, out var denseIndex)) return false;");
            writer.WriteLine("mask.AddTag(denseIndex);");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteTagChecks(IndentedWriter writer, IReadOnlyList<AutoChessTagDefinition> tags)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessTagCheck");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static bool HasTag(in CTagMask mask, int tagCode)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return AutoChessTagMaskTable.TryGetDenseIndex(tagCode, out var denseIndex) && mask.HasTag(denseIndex);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static bool HasAnyTag(in CTagMask mask, in CTagMask other) => mask.HasAnyTag(other);");
            writer.WriteLine("public static bool HasAllTags(in CTagMask mask, in CTagMask other) => mask.HasAllTags(other);");
            foreach (var tag in tags)
                writer.WriteLine($"public static bool Has{tag.SymbolName}(in CTagMask mask) => mask.HasTag(AutoChessTagBits.{tag.SymbolName}Index);");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    internal sealed class AutoChessUnitConfigPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "AutoChessUnitConfig";

        public override IReadOnlyList<string> OutputFileNames { get; } =
            new[] { "Runtime/AutoChessUnitConfigs.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            var unitRow = AutoChessCodeGenData.FindRow(context, "Unit");
            var units = AutoChessCodeGenData.BuildUnitDefinitions(unitRow);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            WriteUnitTypes(writer);
            WriteUnitTable(writer, units);

            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimeManifest(manifest, PhaseName, path);
        }

        private static void WriteUnitTypes(IndentedWriter writer)
        {
            writer.WriteLine("public struct AutoChessUnitDefinition");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int UnitCode;");
            writer.WriteLine("public int FixedTagCode;");
            writer.WriteLine("public int PrimaryAbilityCode;");
            writer.WriteLine("public int ManaAbilityCode;");
            writer.WriteLine("public int ControlAbilityCode;");
            writer.WriteLine("public int SupportAbilityCode;");
            writer.WriteLine("public int SummonAbilityCode;");
            writer.WriteLine("public float Health;");
            writer.WriteLine("public float Mana;");
            writer.WriteLine("public float Shield;");
            writer.WriteLine("public float ArcaneResistance;");
            writer.WriteLine("public float MaxHealth;");
            writer.WriteLine("public float MaxMana;");
            writer.WriteLine("public float MaxShield;");
            writer.WriteLine("public float MaxArcaneResistance;");
            writer.WriteLine("public HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;");
            writer.WriteLine("public HeadlessAutoChessTargetPolicy ManaTargetPolicy;");
            writer.WriteLine("public HeadlessAutoChessTargetPolicy ControlTargetPolicy;");
            writer.WriteLine("public HeadlessAutoChessTargetPolicy SupportTargetPolicy;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteUnitTable(
            IndentedWriter writer,
            IReadOnlyList<AutoChessUnitDefinition> units)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessUnitConfigTable");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int DefinitionCount = {units.Count};");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetDefinition(int unitCode, out AutoChessUnitDefinition definition)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (unitCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var unit in units)
            {
                writer.WriteLine($"case {unit.UnitCode}:");
                writer.Indent++;
                WriteUnitDefinitionAssignment(writer, unit);
                writer.WriteLine("return true;");
                writer.Indent--;
            }

            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("definition = default;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteUnitDefinitionAssignment(
            IndentedWriter writer,
            AutoChessUnitDefinition unit)
        {
            writer.WriteLine("definition = new AutoChessUnitDefinition");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"UnitCode = {unit.UnitCode},");
            writer.WriteLine($"FixedTagCode = {unit.FixedTagCode},");
            writer.WriteLine($"PrimaryAbilityCode = {unit.PrimaryAbilityCode},");
            writer.WriteLine($"ManaAbilityCode = {unit.ManaAbilityCode},");
            writer.WriteLine($"ControlAbilityCode = {unit.ControlAbilityCode},");
            writer.WriteLine($"SupportAbilityCode = {unit.SupportAbilityCode},");
            writer.WriteLine($"SummonAbilityCode = {unit.SummonAbilityCode},");
            writer.WriteLine($"Health = {AutoChessCodeGenData.FloatLiteral(unit.Health)},");
            writer.WriteLine($"Mana = {AutoChessCodeGenData.FloatLiteral(unit.Mana)},");
            writer.WriteLine($"Shield = {AutoChessCodeGenData.FloatLiteral(unit.Shield)},");
            writer.WriteLine($"ArcaneResistance = {AutoChessCodeGenData.FloatLiteral(unit.ArcaneResistance)},");
            writer.WriteLine($"MaxHealth = {AutoChessCodeGenData.FloatLiteral(unit.MaxHealth)},");
            writer.WriteLine($"MaxMana = {AutoChessCodeGenData.FloatLiteral(unit.MaxMana)},");
            writer.WriteLine($"MaxShield = {AutoChessCodeGenData.FloatLiteral(unit.MaxShield)},");
            writer.WriteLine($"MaxArcaneResistance = {AutoChessCodeGenData.FloatLiteral(unit.MaxArcaneResistance)},");
            writer.WriteLine($"PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.{unit.PrimaryTargetPolicy},");
            writer.WriteLine($"ManaTargetPolicy = HeadlessAutoChessTargetPolicy.{unit.ManaTargetPolicy},");
            writer.WriteLine($"ControlTargetPolicy = HeadlessAutoChessTargetPolicy.{unit.ControlTargetPolicy},");
            writer.WriteLine($"SupportTargetPolicy = HeadlessAutoChessTargetPolicy.{unit.SupportTargetPolicy},");
            writer.Indent--;
            writer.WriteLine("};");
        }
    }

    internal sealed class AutoChessMmcEvaluatorPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "AutoChessMmcEvaluator";

        public override IReadOnlyList<string> OutputFileNames { get; } =
            new[] { "Runtime/AutoChessMmcEvaluator.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            var gameplayEffectRow = AutoChessCodeGenData.FindRow(context, "GameplayEffect");
            var magnitudes = AutoChessCodeGenData.BuildMagnitudeDefinitions(context, gameplayEffectRow);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            WriteMagnitudeTypes(writer);
            WriteMagnitudeTable(writer, magnitudes);

            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimeManifest(manifest, PhaseName, path);
        }

        private static void WriteMagnitudeTypes(IndentedWriter writer)
        {
            writer.WriteLine("public struct AutoChessMagnitudeDefinition");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int GameplayEffectCode;");
            writer.WriteLine("public int ModifierIndex;");
            writer.WriteLine("public int AttributeSetCode;");
            writer.WriteLine("public int AttributeCode;");
            writer.WriteLine("public EModifierOp Operation;");
            writer.WriteLine("public float BaseMagnitude;");
            writer.WriteLine("public EMagnitudeSource Source;");
            writer.WriteLine("public int Key;");
            writer.WriteLine("public int DamageTypeCode;");
            writer.WriteLine("public int ResistanceAttributeSetCode;");
            writer.WriteLine("public int ResistanceAttributeCode;");
            writer.WriteLine("public float ResistanceCap;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public struct AutoChessMagnitudeEvaluationContext");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public EMagnitudeSource Source;");
            writer.WriteLine("public float BaseMagnitude;");
            writer.WriteLine("public float SetByCallerMagnitude;");
            writer.WriteLine("public float SourceAttributeValue;");
            writer.WriteLine("public float TargetAttributeValue;");
            writer.WriteLine("public int StackCount;");
            writer.WriteLine("public float Coefficient;");
            writer.WriteLine("public float PreAdd;");
            writer.WriteLine("public float PostAdd;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteMagnitudeTable(
            IndentedWriter writer,
            IReadOnlyList<AutoChessMagnitudeDefinition> magnitudes)
        {
            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessMmcEvaluator");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int DefinitionCount = {magnitudes.Count};");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetDefinition(int gameplayEffectCode, int modifierIndex, out AutoChessMagnitudeDefinition definition)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (gameplayEffectCode)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var magnitude in magnitudes)
            {
                writer.WriteLine($"case {magnitude.GameplayEffectCode} when modifierIndex == {magnitude.ModifierIndex}:");
                writer.Indent++;
                writer.WriteLine("definition = new AutoChessMagnitudeDefinition");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"GameplayEffectCode = {magnitude.GameplayEffectCode},");
                writer.WriteLine($"ModifierIndex = {magnitude.ModifierIndex},");
                writer.WriteLine($"AttributeSetCode = {magnitude.AttributeSetCode},");
                writer.WriteLine($"AttributeCode = {magnitude.AttributeCode},");
                writer.WriteLine($"Operation = EModifierOp.{magnitude.Operation},");
                writer.WriteLine($"BaseMagnitude = {AutoChessCodeGenData.FloatLiteral(magnitude.BaseMagnitude)},");
                writer.WriteLine($"Source = EMagnitudeSource.{magnitude.Source},");
                writer.WriteLine($"Key = {magnitude.Key},");
                writer.WriteLine($"DamageTypeCode = {magnitude.DamageTypeCode},");
                writer.WriteLine($"ResistanceAttributeSetCode = {magnitude.ResistanceAttributeSetCode},");
                writer.WriteLine($"ResistanceAttributeCode = {magnitude.ResistanceAttributeCode},");
                writer.WriteLine($"ResistanceCap = {AutoChessCodeGenData.FloatLiteral(magnitude.ResistanceCap)},");
                writer.Indent--;
                writer.WriteLine("};");
                writer.WriteLine("return true;");
                writer.Indent--;
            }

            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("definition = default;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static float Evaluate(in AutoChessMagnitudeEvaluationContext context)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var magnitude = context.Source switch");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("EMagnitudeSource.SetByCaller => context.SetByCallerMagnitude,");
            writer.WriteLine("EMagnitudeSource.SourceAttribute => context.SourceAttributeValue,");
            writer.WriteLine("EMagnitudeSource.TargetAttribute => context.TargetAttributeValue,");
            writer.WriteLine("EMagnitudeSource.StackCount => context.StackCount,");
            writer.WriteLine("_ => context.BaseMagnitude,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("return ((magnitude + context.PreAdd) * context.Coefficient) + context.PostAdd;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryEvaluate(int gameplayEffectCode, int modifierIndex, in AutoChessMagnitudeEvaluationContext context, out float value)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!TryGetDefinition(gameplayEffectCode, modifierIndex, out var definition))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("value = 0f;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("var resolved = context;");
            writer.WriteLine("resolved.Source = definition.Source;");
            writer.WriteLine("resolved.BaseMagnitude = definition.BaseMagnitude;");
            writer.WriteLine("value = Evaluate(resolved);");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    internal sealed class AutoChessScenarioBuildPlanPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "AutoChessScenarioBuildPlan";

        public override IReadOnlyList<string> OutputFileNames { get; } =
            new[] { "Runtime/AutoChessScenarioBuildPlan.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            var summonRow = AutoChessCodeGenData.FindRow(context, "Summon");
            var unitRow = AutoChessCodeGenData.FindRow(context, "Unit");
            var scenarioSpawnRow = AutoChessCodeGenData.FindRow(context, "ScenarioSpawn");
            var summons = AutoChessCodeGenData.BuildSummonDefinitions(summonRow);
            var units = AutoChessCodeGenData.BuildUnitDefinitions(unitRow);
            var scenarioSpawns = AutoChessCodeGenData.BuildScenarioSpawnDefinitions(scenarioSpawnRow);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            WriteScenarioTypes(writer);
            WriteScenarioFactory(writer, context, summons, units, scenarioSpawns);

            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimeManifest(manifest, PhaseName, path);
        }

        private static void WriteScenarioTypes(IndentedWriter writer)
        {
            writer.WriteLine("public struct AutoChessScenarioSpawnEntry");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int UnitCode;");
            writer.WriteLine("public HeadlessAutoChessTeam Team;");
            writer.WriteLine("public int BoardX;");
            writer.WriteLine("public int BoardY;");
            writer.WriteLine("public int TurnOrder;");
            writer.WriteLine("public int PrimaryAbilityCode;");
            writer.WriteLine("public int ManaAbilityCode;");
            writer.WriteLine("public int ControlAbilityCode;");
            writer.WriteLine("public int SupportAbilityCode;");
            writer.WriteLine("public int SummonAbilityCode;");
            writer.WriteLine("public float Health;");
            writer.WriteLine("public float Mana;");
            writer.WriteLine("public float Shield;");
            writer.WriteLine("public float ArcaneResistance;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public struct AutoChessScenarioSummonEntry");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int SummonGameplayEffectCode;");
            writer.WriteLine("public int SummonedUnitCode;");
            writer.WriteLine("public int FixedTagCode;");
            writer.WriteLine("public int PrimaryAbilityCode;");
            writer.WriteLine("public int LifetimeTurns;");
            writer.WriteLine("public int SlotOffset;");
            writer.WriteLine("public int BoardXOffset;");
            writer.WriteLine("public int BoardYOffset;");
            writer.WriteLine("public int TurnOrderOffset;");
            writer.WriteLine("public float Health;");
            writer.WriteLine("public float Mana;");
            writer.WriteLine("public float Shield;");
            writer.WriteLine("public float MaxHealth;");
            writer.WriteLine("public float MaxMana;");
            writer.WriteLine("public float MaxShield;");
            writer.WriteLine("public HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public struct AutoChessScenarioBuildPlan");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int BoardWidth;");
            writer.WriteLine("public int BoardHeight;");
            writer.WriteLine("public int AbilityDefinitionCount;");
            writer.WriteLine("public int GameplayEffectDefinitionCount;");
            writer.WriteLine("public int AttributeDefinitionCount;");
            writer.WriteLine("public int GameplayTagDefinitionCount;");
            writer.WriteLine("public int UnitDefinitionCount;");
            writer.WriteLine("public int ScenarioSpawnDefinitionCount;");
            writer.WriteLine("public FixedList512Bytes<AutoChessScenarioSpawnEntry> SpawnEntries;");
            writer.WriteLine("public FixedList512Bytes<AutoChessScenarioSummonEntry> SummonEntries;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteScenarioFactory(
            IndentedWriter writer,
            GasCodeGenContext context,
            IReadOnlyList<AutoChessSummonDefinition> summons,
            IReadOnlyList<AutoChessUnitDefinition> units,
            IReadOnlyList<AutoChessScenarioSpawnDefinition> scenarioSpawns)
        {
            var abilityCount = AutoChessCodeGenData.FindRow(context, "Ability")?.RowValues?.Count ?? 0;
            var geCount = AutoChessCodeGenData.FindRow(context, "GameplayEffect")?.RowValues?.Count ?? 0;
            var attributeCount = AutoChessCodeGenData.FindRow(context, "Attribute")?.RowValues?.Count ?? 0;
            var tagCount = AutoChessCodeGenData.FindRow(context, "GameplayTag")?.RowValues?.Count ?? 0;
            var unitCount = AutoChessCodeGenData.FindRow(context, "Unit")?.RowValues?.Count ?? 0;
            var scenarioSpawnCount = AutoChessCodeGenData.FindRow(context, "ScenarioSpawn")?.RowValues?.Count ?? 0;
            var unitByCode = units.ToDictionary(unit => unit.UnitCode);

            writer.WriteLine("");
            writer.WriteLine("public static class AutoChessScenarioBuildPlanFactory");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static AutoChessScenarioBuildPlan CreateDefault()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var plan = new AutoChessScenarioBuildPlan");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("BoardWidth = HeadlessAutoChessScenario.BoardWidth,");
            writer.WriteLine("BoardHeight = HeadlessAutoChessScenario.BoardHeight,");
            writer.WriteLine($"AbilityDefinitionCount = {abilityCount},");
            writer.WriteLine($"GameplayEffectDefinitionCount = {geCount},");
            writer.WriteLine($"AttributeDefinitionCount = {attributeCount},");
            writer.WriteLine($"GameplayTagDefinitionCount = {tagCount},");
            writer.WriteLine($"UnitDefinitionCount = {unitCount},");
            writer.WriteLine($"ScenarioSpawnDefinitionCount = {scenarioSpawnCount},");
            writer.Indent--;
            writer.WriteLine("};");

            foreach (var spawn in scenarioSpawns)
            {
                if (!unitByCode.TryGetValue(spawn.UnitCode, out var unit))
                    continue;

                writer.WriteLine("plan.SpawnEntries.Add(new AutoChessScenarioSpawnEntry");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"UnitCode = {spawn.UnitCode},");
                writer.WriteLine($"Team = HeadlessAutoChessTeam.{spawn.Team},");
                writer.WriteLine($"BoardX = {spawn.BoardX},");
                writer.WriteLine($"BoardY = {spawn.BoardY},");
                writer.WriteLine($"TurnOrder = {spawn.TurnOrder},");
                writer.WriteLine($"PrimaryAbilityCode = {unit.PrimaryAbilityCode},");
                writer.WriteLine($"ManaAbilityCode = {unit.ManaAbilityCode},");
                writer.WriteLine($"ControlAbilityCode = {unit.ControlAbilityCode},");
                writer.WriteLine($"SupportAbilityCode = {unit.SupportAbilityCode},");
                writer.WriteLine($"SummonAbilityCode = {unit.SummonAbilityCode},");
                writer.WriteLine($"Health = {AutoChessCodeGenData.FloatLiteral(unit.Health)},");
                writer.WriteLine($"Mana = {AutoChessCodeGenData.FloatLiteral(unit.Mana)},");
                writer.WriteLine($"Shield = {AutoChessCodeGenData.FloatLiteral(unit.Shield)},");
                writer.WriteLine($"ArcaneResistance = {AutoChessCodeGenData.FloatLiteral(unit.ArcaneResistance)},");
                writer.Indent--;
                writer.WriteLine("});");
            }

            foreach (var summon in summons)
            {
                writer.WriteLine("plan.SummonEntries.Add(new AutoChessScenarioSummonEntry");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"SummonGameplayEffectCode = {summon.SummonGameplayEffectCode},");
                writer.WriteLine($"SummonedUnitCode = {summon.SummonedUnitCode},");
                writer.WriteLine($"FixedTagCode = {summon.FixedTagCode},");
                writer.WriteLine($"PrimaryAbilityCode = {summon.PrimaryAbilityCode},");
                writer.WriteLine($"LifetimeTurns = {summon.LifetimeTurns},");
                writer.WriteLine($"SlotOffset = {summon.SlotOffset},");
                writer.WriteLine($"BoardXOffset = {summon.BoardXOffset},");
                writer.WriteLine($"BoardYOffset = {summon.BoardYOffset},");
                writer.WriteLine($"TurnOrderOffset = {summon.TurnOrderOffset},");
                writer.WriteLine($"Health = {AutoChessCodeGenData.FloatLiteral(summon.Health)},");
                writer.WriteLine($"Mana = {AutoChessCodeGenData.FloatLiteral(summon.Mana)},");
                writer.WriteLine($"Shield = {AutoChessCodeGenData.FloatLiteral(summon.Shield)},");
                writer.WriteLine($"MaxHealth = {AutoChessCodeGenData.FloatLiteral(summon.MaxHealth)},");
                writer.WriteLine($"MaxMana = {AutoChessCodeGenData.FloatLiteral(summon.MaxMana)},");
                writer.WriteLine($"MaxShield = {AutoChessCodeGenData.FloatLiteral(summon.MaxShield)},");
                writer.WriteLine($"PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.{summon.PrimaryTargetPolicy},");
                writer.Indent--;
                writer.WriteLine("});");
            }

            writer.WriteLine("return plan;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    internal static class AutoChessCodeGenData
    {
        public static RowMetadata FindRow(GasCodeGenContext context, string domainName)
        {
            return context.Rows.FirstOrDefault(row => row.DomainName == domainName);
        }

        public static IReadOnlyList<AutoChessAttributeDefinition> BuildAttributeDefinitions(
            GasCodeGenContext context,
            RowMetadata row)
        {
            if (row?.RowValues == null)
                return Array.Empty<AutoChessAttributeDefinition>();

            var constants = BuildConstantNameMap(context, "Attribute");
            return row.RowValues
                .OrderBy(value => value.Code)
                .Select(value =>
                {
                    var symbol = ResolveSymbol(constants, value.Code, "Attribute", "Attribute");
                    return new AutoChessAttributeDefinition
                    {
                        SymbolName = symbol,
                        ComponentTypeName = symbol + "Attribute",
                        ParameterName = ToParameterName(symbol),
                        AttributeSetCode = GetInt(value.Row, "AttributeSetCode"),
                        AttributeCode = GetInt(value.Row, "AttributeCode"),
                        InitialValue = GetFloat(value.Row, "InitialValue"),
                        IsClampMin = GetBool(value.Row, "IsClampMin"),
                        IsClampMax = GetBool(value.Row, "IsClampMax"),
                        MinValue = GetFloat(value.Row, "MinValue"),
                        MaxValue = GetFloat(value.Row, "MaxValue"),
                    };
                })
                .ToArray();
        }

        public static IReadOnlyList<AutoChessTagDefinition> BuildTagDefinitions(
            GasCodeGenContext context,
            RowMetadata row)
        {
            if (row?.RowValues == null)
                return Array.Empty<AutoChessTagDefinition>();

            var constants = BuildConstantNameMap(context, "Tag");
            return row.RowValues
                .OrderBy(value => value.Code)
                .Select((value, index) => new AutoChessTagDefinition
                {
                    SymbolName = ResolveSymbol(constants, value.Code, "Tag", "Tag"),
                    TagCode = value.Code,
                    DenseIndex = index,
                })
                .ToArray();
        }

        public static IReadOnlyList<AutoChessMagnitudeDefinition> BuildMagnitudeDefinitions(
            GasCodeGenContext context,
            RowMetadata row)
        {
            if (row?.RowValues == null)
                return Array.Empty<AutoChessMagnitudeDefinition>();

            return row.RowValues
                .OrderBy(value => value.Code)
                .Where(value => GetInt(value.Row, "ModifierAttributeSetCode") > 0
                                && GetInt(value.Row, "ModifierAttributeCode") > 0)
                .Select(value => new AutoChessMagnitudeDefinition
                {
                    GameplayEffectCode = GetInt(value.Row, "GameplayEffectCode"),
                    ModifierIndex = 0,
                    AttributeSetCode = GetInt(value.Row, "ModifierAttributeSetCode"),
                    AttributeCode = GetInt(value.Row, "ModifierAttributeCode"),
                    Operation = GetEnumName<EModifierOp>(value.Row, "ModifierOperation"),
                    BaseMagnitude = GetFloat(value.Row, "ModifierMagnitude"),
                    Source = GetEnumName<EMagnitudeSource>(value.Row, "ModifierMagnitudeSource"),
                    Key = GetInt(value.Row, "ModifierMagnitudeKey"),
                    DamageTypeCode = GetInt(value.Row, "DamageTypeCode"),
                    ResistanceAttributeSetCode = GetInt(value.Row, "ResistanceAttributeSetCode"),
                    ResistanceAttributeCode = GetInt(value.Row, "ResistanceAttributeCode"),
                    ResistanceCap = GetFloat(value.Row, "ResistanceCap"),
                })
                .ToArray();
        }

        public static IReadOnlyList<AutoChessSummonDefinition> BuildSummonDefinitions(RowMetadata row)
        {
            if (row?.RowValues == null)
                return Array.Empty<AutoChessSummonDefinition>();

            return row.RowValues
                .OrderBy(value => value.Code)
                .Select(value => new AutoChessSummonDefinition
                {
                    SummonGameplayEffectCode = GetInt(value.Row, "SummonGameplayEffectCode"),
                    SummonedUnitCode = GetInt(value.Row, "SummonedUnitCode"),
                    FixedTagCode = GetInt(value.Row, "FixedTagCode"),
                    PrimaryAbilityCode = GetInt(value.Row, "PrimaryAbilityCode"),
                    LifetimeTurns = GetInt(value.Row, "LifetimeTurns"),
                    SlotOffset = GetInt(value.Row, "SlotOffset"),
                    BoardXOffset = GetInt(value.Row, "BoardXOffset"),
                    BoardYOffset = GetInt(value.Row, "BoardYOffset"),
                    TurnOrderOffset = GetInt(value.Row, "TurnOrderOffset"),
                    Health = GetFloat(value.Row, "Health"),
                    Mana = GetFloat(value.Row, "Mana"),
                    Shield = GetFloat(value.Row, "Shield"),
                    MaxHealth = GetFloat(value.Row, "MaxHealth"),
                    MaxMana = GetFloat(value.Row, "MaxMana"),
                    MaxShield = GetFloat(value.Row, "MaxShield"),
                    PrimaryTargetPolicy = GetEnumName<HeadlessAutoChessTargetPolicy>(value.Row, "PrimaryTargetPolicy"),
                })
                .ToArray();
        }

        public static IReadOnlyList<AutoChessUnitDefinition> BuildUnitDefinitions(RowMetadata row)
        {
            if (row?.RowValues == null)
                return Array.Empty<AutoChessUnitDefinition>();

            return row.RowValues
                .OrderBy(value => value.Code)
                .Select(value => new AutoChessUnitDefinition
                {
                    UnitCode = GetInt(value.Row, "UnitCode"),
                    FixedTagCode = GetInt(value.Row, "FixedTagCode"),
                    PrimaryAbilityCode = GetInt(value.Row, "PrimaryAbilityCode"),
                    ManaAbilityCode = GetInt(value.Row, "ManaAbilityCode"),
                    ControlAbilityCode = GetInt(value.Row, "ControlAbilityCode"),
                    SupportAbilityCode = GetInt(value.Row, "SupportAbilityCode"),
                    SummonAbilityCode = GetInt(value.Row, "SummonAbilityCode"),
                    Health = GetFloat(value.Row, "Health"),
                    Mana = GetFloat(value.Row, "Mana"),
                    Shield = GetFloat(value.Row, "Shield"),
                    ArcaneResistance = GetFloat(value.Row, "ArcaneResistance"),
                    MaxHealth = GetFloat(value.Row, "MaxHealth"),
                    MaxMana = GetFloat(value.Row, "MaxMana"),
                    MaxShield = GetFloat(value.Row, "MaxShield"),
                    MaxArcaneResistance = GetFloat(value.Row, "MaxArcaneResistance"),
                    PrimaryTargetPolicy = GetEnumName<HeadlessAutoChessTargetPolicy>(value.Row, "PrimaryTargetPolicy"),
                    ManaTargetPolicy = GetEnumName<HeadlessAutoChessTargetPolicy>(value.Row, "ManaTargetPolicy"),
                    ControlTargetPolicy = GetEnumName<HeadlessAutoChessTargetPolicy>(value.Row, "ControlTargetPolicy"),
                    SupportTargetPolicy = GetEnumName<HeadlessAutoChessTargetPolicy>(value.Row, "SupportTargetPolicy"),
                })
                .ToArray();
        }

        public static IReadOnlyList<AutoChessScenarioSpawnDefinition> BuildScenarioSpawnDefinitions(RowMetadata row)
        {
            if (row?.RowValues == null)
                return Array.Empty<AutoChessScenarioSpawnDefinition>();

            return row.RowValues
                .OrderBy(value => value.Code)
                .Select(value => new AutoChessScenarioSpawnDefinition
                {
                    ScenarioSpawnCode = GetInt(value.Row, "ScenarioSpawnCode"),
                    ScenarioId = GetInt(value.Row, "ScenarioId"),
                    UnitCode = GetInt(value.Row, "UnitCode"),
                    Team = GetEnumName<HeadlessAutoChessTeam>(value.Row, "Team"),
                    BoardX = GetInt(value.Row, "BoardX"),
                    BoardY = GetInt(value.Row, "BoardY"),
                    TurnOrder = GetInt(value.Row, "TurnOrder"),
                })
                .ToArray();
        }

        public static string FloatLiteral(float value)
        {
            if (float.IsNaN(value))
                return "float.NaN";
            if (float.IsPositiveInfinity(value))
                return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(value))
                return "float.NegativeInfinity";

            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        public static string BoolLiteral(bool value)
        {
            return value ? "true" : "false";
        }

        private static IReadOnlyDictionary<int, string> BuildConstantNameMap(
            GasCodeGenContext context,
            string prefix)
        {
            var result = new Dictionary<int, string>();
            var scenarioType = FindScenarioType(context);
            if (scenarioType == null)
                return result;

            foreach (var field in scenarioType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(int))
                    continue;

                if (!field.Name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (prefix == "Attribute" && field.Name.StartsWith("AttributeSet", StringComparison.Ordinal))
                    continue;

                result[(int)field.GetValue(null)] = field.Name;
            }

            return result;
        }

        private static Type FindScenarioType(GasCodeGenContext context)
        {
            foreach (var row in context.Rows)
            {
                var type = row.RowType.Assembly.GetType("GAS.Runtime.HeadlessAutoChessScenario");
                if (type != null)
                    return type;
            }

            return null;
        }

        private static string ResolveSymbol(
            IReadOnlyDictionary<int, string> constants,
            int code,
            string prefix,
            string fallbackPrefix)
        {
            if (!constants.TryGetValue(code, out var name))
                name = fallbackPrefix + code.ToString(CultureInfo.InvariantCulture);

            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
                name = name.Substring(prefix.Length);

            return SanitizeIdentifier(name);
        }

        private static string ToParameterName(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return "value";

            return char.ToLowerInvariant(symbol[0]) + symbol.Substring(1);
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Generated";

            var chars = value.Where(char.IsLetterOrDigit).ToArray();
            if (chars.Length == 0)
                return "Generated";

            var result = new string(chars);
            if (char.IsDigit(result[0]))
                result = "_" + result;

            return result;
        }

        private static int GetInt(object row, string memberName)
        {
            var value = GetValue(row, memberName);
            return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static float GetFloat(object row, string memberName)
        {
            var value = GetValue(row, memberName);
            return value == null ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static bool GetBool(object row, string memberName)
        {
            var value = GetValue(row, memberName);
            return value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static string GetEnumName<TEnum>(object row, string memberName)
        {
            var value = GetValue(row, memberName);
            if (value == null)
                return Enum.GetName(typeof(TEnum), 0);

            return Enum.GetName(typeof(TEnum), value) ?? value.ToString();
        }

        private static object GetValue(object row, string memberName)
        {
            if (row == null)
                return null;

            var type = row.GetType();
            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(row);

            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.CanRead ? property.GetValue(row) : null;
        }
    }

    internal sealed class AutoChessAttributeDefinition
    {
        public string SymbolName;
        public string ComponentTypeName;
        public string ParameterName;
        public int AttributeSetCode;
        public int AttributeCode;
        public float InitialValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
    }

    internal sealed class AutoChessTagDefinition
    {
        public string SymbolName;
        public int TagCode;
        public int DenseIndex;
    }

    internal sealed class AutoChessMagnitudeDefinition
    {
        public int GameplayEffectCode;
        public int ModifierIndex;
        public int AttributeSetCode;
        public int AttributeCode;
        public string Operation;
        public float BaseMagnitude;
        public string Source;
        public int Key;
        public int DamageTypeCode;
        public int ResistanceAttributeSetCode;
        public int ResistanceAttributeCode;
        public float ResistanceCap;
    }

    internal sealed class AutoChessSummonDefinition
    {
        public int SummonGameplayEffectCode;
        public int SummonedUnitCode;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int LifetimeTurns;
        public int SlotOffset;
        public int BoardXOffset;
        public int BoardYOffset;
        public int TurnOrderOffset;
        public float Health;
        public float Mana;
        public float Shield;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public string PrimaryTargetPolicy;
    }

    internal sealed class AutoChessUnitDefinition
    {
        public int UnitCode;
        public int FixedTagCode;
        public int PrimaryAbilityCode;
        public int ManaAbilityCode;
        public int ControlAbilityCode;
        public int SupportAbilityCode;
        public int SummonAbilityCode;
        public float Health;
        public float Mana;
        public float Shield;
        public float ArcaneResistance;
        public float MaxHealth;
        public float MaxMana;
        public float MaxShield;
        public float MaxArcaneResistance;
        public string PrimaryTargetPolicy;
        public string ManaTargetPolicy;
        public string ControlTargetPolicy;
        public string SupportTargetPolicy;
    }

    internal sealed class AutoChessScenarioSpawnDefinition
    {
        public int ScenarioSpawnCode;
        public int ScenarioId;
        public int UnitCode;
        public string Team;
        public int BoardX;
        public int BoardY;
        public int TurnOrder;
    }
}
