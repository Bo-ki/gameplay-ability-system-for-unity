using System;
using System.Collections.Generic;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 以排序 key 数组和二分查找提供固定容量映射，消除可控 hash collision 的 CPU 退化面。
    /// </summary>
    internal sealed class CanonicalDefinitionLookup<TValue>
    {
        private readonly CanonicalDefinitionKey[] _keys;
        private readonly TValue[] _values;
        private readonly bool[] _assigned;

        /// <summary>
        /// 冻结全部允许 key，并在分配 value 表前拒绝 null 或重复 identity。
        /// </summary>
        public CanonicalDefinitionLookup(IReadOnlyList<CanonicalDefinitionKey> keys)
        {
            _keys = CanonicalSemanticResourceBudget.SnapshotList(
                keys,
                "canonical definition lookup keys",
                false);
            Array.Sort(_keys, CompareKeys);
            ValidateKeys(_keys);
            _values = new TValue[_keys.Length];
            _assigned = new bool[_keys.Length];
        }

        public int Count => _keys.Length;

        /// <summary>
        /// 读取已赋值 key；缺失或尚未赋值均以 CFG1501 失败。
        /// </summary>
        public TValue this[CanonicalDefinitionKey key]
        {
            get
            {
                var index = FindIndex(key);
                if (index < 0 || !_assigned[index])
                    Fail("Canonical definition lookup key is missing or unassigned.");
                return _values[index];
            }
        }

        /// <summary>
        /// 为一个预声明 key 赋值一次，禁止覆盖形成双事实源。
        /// </summary>
        public void Add(CanonicalDefinitionKey key, TValue value)
        {
            var index = FindIndex(key);
            if (index < 0 || _assigned[index])
                Fail("Canonical definition lookup key is missing or assigned twice.");
            _values[index] = value;
            _assigned[index] = true;
        }

        /// <summary>
        /// 判断 key 是否属于固定 key 集，与 value 是否赋值无关。
        /// </summary>
        public bool ContainsKey(CanonicalDefinitionKey key)
        {
            return FindIndex(key) >= 0;
        }

        /// <summary>
        /// 尝试读取已赋值 value，未声明或未赋值时返回 false。
        /// </summary>
        public bool TryGetValue(CanonicalDefinitionKey key, out TValue value)
        {
            var index = FindIndex(key);
            if (index >= 0 && _assigned[index])
            {
                value = _values[index];
                return true;
            }
            value = default(TValue);
            return false;
        }

        /// <summary>
        /// 按稳定 key 顺序读取第 index 个 key，供确定性冻结循环使用。
        /// </summary>
        public CanonicalDefinitionKey GetKeyAt(int index)
        {
            return _keys[index];
        }

        /// <summary>
        /// 按稳定 key 顺序读取第 index 个已赋值 value。
        /// </summary>
        public TValue GetValueAt(int index)
        {
            if (!_assigned[index])
                Fail("Canonical definition lookup value is unassigned.");
            return _values[index];
        }

        /// <summary>
        /// 创建共享同一稳定 key 集但不共享 value 的新映射。
        /// </summary>
        public CanonicalDefinitionLookup<TNext> CreateSibling<TNext>()
        {
            return new CanonicalDefinitionLookup<TNext>(_keys);
        }

        /// <summary>
        /// 在排序 key 数组中执行确定性二分查找。
        /// </summary>
        private int FindIndex(CanonicalDefinitionKey key)
        {
            if (key == null)
                return -1;
            var low = 0;
            var high = _keys.Length - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var comparison = _keys[middle].CompareTo(key);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        /// <summary>
        /// 验证排序后 key 集没有 null 或重复 identity。
        /// </summary>
        private static void ValidateKeys(CanonicalDefinitionKey[] keys)
        {
            for (var index = 0; index < keys.Length; index++)
            {
                if (keys[index] == null)
                    Fail("Canonical definition lookup key cannot be null.");
                if (index > 0 && keys[index - 1].CompareTo(keys[index]) == 0)
                    Fail("Canonical definition lookup key cannot be duplicated.");
            }
        }

        /// <summary>
        /// 比较两个非空 canonical key 的稳定 identity。
        /// </summary>
        private static int CompareKeys(CanonicalDefinitionKey left, CanonicalDefinitionKey right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            return right == null ? 1 : left.CompareTo(right);
        }

        /// <summary>
        /// 以稳定 CFG1501 拒绝固定映射的结构错误。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                message);
        }
    }
}
