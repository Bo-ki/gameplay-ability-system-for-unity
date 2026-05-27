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
    public static class BlobDefinitionLookupBuilder
    {

        public static BlobAbilityDefinitionLookup BuildBlobAbilityDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessAbilityDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].AbilityCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobAbilityDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobAbilityDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobAbilityDefinitionLookup(sortedCodes, entries);
        }

        public static BlobAttributeDefinitionLookup BuildBlobAttributeDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessAttributeDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].AttributeCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobAttributeDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobAttributeDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobAttributeDefinitionLookup(sortedCodes, entries);
        }

        public static BlobAttributeSetDefinitionLookup BuildBlobAttributeSetDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessAttributeSetDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].AttributeSetCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobAttributeSetDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobAttributeSetDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobAttributeSetDefinitionLookup(sortedCodes, entries);
        }

        public static BlobGameplayCueDefinitionLookup BuildBlobGameplayCueDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessGameplayCueDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].GameplayCueCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobGameplayCueDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobGameplayCueDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobGameplayCueDefinitionLookup(sortedCodes, entries);
        }

        public static BlobGameplayEffectDefinitionLookup BuildBlobGameplayEffectDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessGameplayEffectDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].GameplayEffectCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobGameplayEffectDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobGameplayEffectDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobGameplayEffectDefinitionLookup(sortedCodes, entries);
        }

        public static BlobGameplayTagDefinitionLookup BuildBlobGameplayTagDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessGameplayTagDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].GameplayTagCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobGameplayTagDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobGameplayTagDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobGameplayTagDefinitionLookup(sortedCodes, entries);
        }

        public static BlobScenarioSpawnDefinitionLookup BuildBlobScenarioSpawnDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessScenarioSpawnDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].ScenarioSpawnCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobScenarioSpawnDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobScenarioSpawnDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobScenarioSpawnDefinitionLookup(sortedCodes, entries);
        }

        public static BlobSummonDefinitionLookup BuildBlobSummonDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessSummonDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].SummonGameplayEffectCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobSummonDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobSummonDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobSummonDefinitionLookup(sortedCodes, entries);
        }

        public static BlobTimelineDefinitionLookup BuildBlobTimelineDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessTimelineDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].TimelineId, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobTimelineDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobTimelineDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobTimelineDefinitionLookup(sortedCodes, entries);
        }

        public static BlobUnitDefinitionLookup BuildBlobUnitDefinitionLookupFromRows(
            IReadOnlyList<GAS.Runtime.HeadlessAutoChessUnitDefinitionRow> rows,
            Allocator allocator = Allocator.Persistent)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var sorted = new (int Code, int Index)[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                sorted[i] = (rows[i].UnitCode, i);
            Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));
            var sortedCodes = new NativeArray<int>(rows.Count, allocator);
            var entries = new NativeArray<BlobAssetReference<BlobUnitDefinition>>(rows.Count, allocator);
            for (var i = 0; i < sorted.Length; i++)
            {
                sortedCodes[i] = sorted[i].Code;
                entries[i] = BlobDefinitionBuilder.BakeBlobUnitDefinition(rows[sorted[i].Index], allocator);
            }
            return new BlobUnitDefinitionLookup(sortedCodes, entries);
        }
    }
}
#endif
