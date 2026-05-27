///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using System;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{

    /// <summary>
    /// Burst-readable lookup for BlobAbilityDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobAbilityDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobAbilityDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobAbilityDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobAbilityDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobAbilityDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobAttributeDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobAttributeDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobAttributeDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobAttributeDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobAttributeDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobAttributeDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobAttributeSetDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobAttributeSetDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobAttributeSetDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobAttributeSetDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobAttributeSetDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobAttributeSetDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobGameplayCueDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobGameplayCueDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobGameplayCueDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobGameplayCueDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobGameplayCueDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobGameplayCueDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobGameplayEffectDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobGameplayEffectDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobGameplayEffectDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobGameplayEffectDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobGameplayEffectDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobGameplayEffectDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobGameplayTagDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobGameplayTagDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobGameplayTagDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobGameplayTagDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobGameplayTagDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobGameplayTagDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobScenarioSpawnDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobScenarioSpawnDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobScenarioSpawnDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobScenarioSpawnDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobScenarioSpawnDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobScenarioSpawnDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobSummonDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobSummonDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobSummonDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobSummonDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobSummonDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobSummonDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobTimelineDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobTimelineDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobTimelineDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobTimelineDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobTimelineDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobTimelineDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }

    /// <summary>
    /// Burst-readable lookup for BlobUnitDefinition. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct BlobUnitDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<BlobUnitDefinition>> _entries;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;

        public BlobUnitDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<BlobUnitDefinition>> entries)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
        }

        public bool TryGet(int code, out BlobAssetReference<BlobUnitDefinition> blob)
        {
            blob = default;
            if (!IsCreated) return false;
            var lo = 0;
            var hi = _sortedCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = _sortedCodes[mid];
                if (midCode == code)
                {
                    blob = _entries[mid];
                    return true;
                }
                if (midCode < code) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
        }
    }
}
