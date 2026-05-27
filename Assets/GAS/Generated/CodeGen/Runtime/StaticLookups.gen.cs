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
    /// Burst-readable lookup for AbilityDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct AbilityDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<AbilityDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public AbilityDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<AbilityDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<AbilityDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for AttributeDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct AttributeDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<AttributeDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public AttributeDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<AttributeDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<AttributeDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for AttributeSetDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct AttributeSetDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<AttributeSetDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public AttributeSetDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<AttributeSetDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<AttributeSetDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for GameplayCueDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct GameplayCueDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<GameplayCueDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public GameplayCueDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<GameplayCueDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<GameplayCueDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for GameplayEffectDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct GameplayEffectDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<GameplayEffectDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public GameplayEffectDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<GameplayEffectDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<GameplayEffectDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for GameplayTagDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct GameplayTagDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<GameplayTagDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public GameplayTagDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<GameplayTagDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<GameplayTagDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for ScenarioSpawnDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct ScenarioSpawnDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<ScenarioSpawnDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public ScenarioSpawnDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<ScenarioSpawnDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<ScenarioSpawnDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for SummonDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct SummonDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<SummonDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public SummonDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<SummonDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<SummonDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for TimelineDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct TimelineDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<TimelineDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public TimelineDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<TimelineDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<TimelineDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }

    /// <summary>
    /// Burst-readable lookup for UnitDefinitionBlob. Runtime owns only sorted codes and blob references.
    /// </summary>
    public struct UnitDefinitionLookup : IDisposable
    {
        private NativeArray<int> _sortedCodes;
        private NativeArray<BlobAssetReference<UnitDefinitionBlob>> _entries;
        private byte _ownsMemory;

        public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;
        public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;
        public bool OwnsMemory => _ownsMemory != 0;

        public UnitDefinitionLookup(
            NativeArray<int> sortedCodes,
            NativeArray<BlobAssetReference<UnitDefinitionBlob>> entries,
            bool ownsMemory = true)
        {
            if (sortedCodes.IsCreated != entries.IsCreated)
                throw new ArgumentException("Lookup code and entry arrays must have the same ownership state.");
            if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)
                throw new ArgumentException("Lookup code and entry arrays must have the same length.");
            _sortedCodes = sortedCodes;
            _entries = entries;
            _ownsMemory = ownsMemory ? (byte)1 : (byte)0;
        }

        public bool TryGet(int code, out BlobAssetReference<UnitDefinitionBlob> blob)
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
            if (!OwnsMemory)
            {
                _sortedCodes = default;
                _entries = default;
                _ownsMemory = 0;
                return;
            }
            if (_entries.IsCreated)
            {
                for (var i = 0; i < _entries.Length; i++)
                    if (_entries[i].IsCreated) _entries[i].Dispose();
                _entries.Dispose();
            }
            if (_sortedCodes.IsCreated) _sortedCodes.Dispose();
            _sortedCodes = default;
            _entries = default;
            _ownsMemory = 0;
        }
    }
}
