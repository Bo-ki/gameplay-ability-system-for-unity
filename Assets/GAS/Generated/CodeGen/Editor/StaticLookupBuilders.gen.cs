///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedDefinitionLookupBuilder
    {

        public static AbilityDefinitionLookup BuildAbilityDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].AbilityCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<AbilityDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildAbilityDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new AbilityDefinitionLookup(sortedCodes, entries);
        }

        public static AttributeDefinitionLookup BuildAttributeDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].AttributeCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<AttributeDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildAttributeDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new AttributeDefinitionLookup(sortedCodes, entries);
        }

        public static AttributeSetDefinitionLookup BuildAttributeSetDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].AttributeSetCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<AttributeSetDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildAttributeSetDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new AttributeSetDefinitionLookup(sortedCodes, entries);
        }

        public static GameplayCueDefinitionLookup BuildGameplayCueDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].GameplayCueCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<GameplayCueDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildGameplayCueDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new GameplayCueDefinitionLookup(sortedCodes, entries);
        }

        public static GameplayEffectDefinitionLookup BuildGameplayEffectDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].GameplayEffectCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<GameplayEffectDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildGameplayEffectDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new GameplayEffectDefinitionLookup(sortedCodes, entries);
        }

        public static GameplayTagDefinitionLookup BuildGameplayTagDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].GameplayTagCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<GameplayTagDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildGameplayTagDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new GameplayTagDefinitionLookup(sortedCodes, entries);
        }

        public static ScenarioSpawnDefinitionLookup BuildScenarioSpawnDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].ScenarioSpawnCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<ScenarioSpawnDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildScenarioSpawnDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new ScenarioSpawnDefinitionLookup(sortedCodes, entries);
        }

        public static SummonDefinitionLookup BuildSummonDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessSummonDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].SummonGameplayEffectCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<SummonDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildSummonDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new SummonDefinitionLookup(sortedCodes, entries);
        }

        public static UnitDefinitionLookup BuildUnitDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessUnitDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].UnitCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<UnitDefinitionBlob>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = GASGeneratedDefinitionBlobBuilder.BuildUnitDefinitionBlob(rows[sorted[i].Index], allocator);
            }
            return new UnitDefinitionLookup(sortedCodes, entries);
        }
    }
}
#endif
