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
            IReadOnlyList<GAS.Editor.AbilityDefinitionRow> rows,
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
            IReadOnlyList<GAS.Editor.AttributeDefinitionRow> rows,
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
            IReadOnlyList<GAS.Editor.AttributeSetDefinitionRow> rows,
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
            IReadOnlyList<GAS.Editor.GameplayCueDefinitionRow> rows,
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
            IReadOnlyList<GAS.Editor.GameplayEffectDefinitionRow> rows,
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
            IReadOnlyList<GAS.Editor.GameplayTagDefinitionRow> rows,
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
    }
}
#endif
