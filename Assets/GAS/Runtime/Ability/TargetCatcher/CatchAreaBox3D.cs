using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存生成期 Area 目标规则参数；物理命中只允许由宿主的 TargetResolve adapter 转换为稳定 target token。
    /// </summary>
    public sealed class CatchAreaBox3D : TargetCatcherBase<XParamCatchAreaBox3D>
    {
    }

    /// <summary>
    /// 保存 Box3D 目标规则的 authoring 参数，供 normalized row 解码和 TargetResolve bake 使用。
    /// </summary>
    public class XParamCatchAreaBox3D : XParam
    {
        [BeanField(nameof(SetIsWorldSpace),Order = 1)]
        public bool isWorldSpace;

        [BeanField(nameof(SetOffset),Order = 2)]
        public Vector3 offset;

        [BeanField(nameof(SetSize),Order = 3)]
        public Vector3 size;

        [BeanField(nameof(SetRotation),Order = 4)]
        public Vector3 rotation;

        [BeanField(nameof(SetLayer), LubanType = "int",Order = 5)]
        public LayerMask layer;


        public void SetIsWorldSpace(bool isWorld)
        {
            isWorldSpace = isWorld;
        }

        public void SetOffset(Vector3 offset)
        {
            this.offset = offset;
        }

        public void SetSize(Vector3 size)
        {
            this.size = size;
        }

        public void SetRotation(Vector3 rotation)
        {
            this.rotation = rotation;
        }

        public void SetLayer(int layer)
        {
            this.layer.value = layer;
        }
#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null)
                return;

            if (TryGetValue(paramData, 0, out var worldSpace)
                && bool.TryParse(worldSpace, out var parsedWorldSpace))
                isWorldSpace = parsedWorldSpace;
            if (TryParseVector3(paramData, 1, out var parsedOffset))
                offset = parsedOffset;
            if (TryParseVector3(paramData, 2, out var parsedSize))
                size = parsedSize;
            if (TryParseVector3(paramData, 3, out var parsedRotation))
                rotation = parsedRotation;
            if (TryGetValue(paramData, 4, out var layerValue)
                && int.TryParse(layerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLayer))
                layer = parsedLayer;
        }

        public List<object> EncodeExcelData()
        {
            var data = new List<object>
            {
                isWorldSpace.ToString(),
                FormatVector3(offset),
                FormatVector3(size),
                FormatVector3(rotation),
                layer.value.ToString(CultureInfo.InvariantCulture)
            };
            return data;
        }

        private static bool TryGetValue(List<object> values, int index, out string value)
        {
            value = null;
            if (index < 0 || index >= values.Count || values[index] == null)
                return false;

            value = System.Convert.ToString(values[index], CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryParseVector3(List<object> values, int index, out Vector3 result)
        {
            result = default;
            if (!TryGetValue(values, index, out var encoded))
                return false;

            var parts = encoded.Split(',');
            if (parts.Length != 3
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;

            result = new Vector3(x, y, z);
            return true;
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Join(",",
                value.x.ToString("G9", CultureInfo.InvariantCulture),
                value.y.ToString("G9", CultureInfo.InvariantCulture),
                value.z.ToString("G9", CultureInfo.InvariantCulture));
        }
#endif
    }
}
