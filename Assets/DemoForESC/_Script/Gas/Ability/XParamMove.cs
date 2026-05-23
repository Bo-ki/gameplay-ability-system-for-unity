using System.Collections.Generic;
using GAS.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DemoForESC._Script.Gas.Ability
{
    public class XParamMove : XParam
    {
        private Vector3 _moveDirection;
        private Vector3 _viewPointForward;

        [ShowInInspector]
        [BeanField(nameof(SetRotationOffset), Name = "RotationOffset")]  
        public float RotationOffset { get; private set; } = 0.1f;

        public Vector3 ViewPointForward => _viewPointForward;
        public Vector3 MoveDirection => _moveDirection;
        
        public XParamMove()
        {
            _moveDirection = Vector3.zero;
            _viewPointForward = Vector3.forward;
            RotationOffset = 0.1f;
        }
        
        public void SetDirection(Vector3 moveDirection,Vector3 viewPointForward)
        {
            _moveDirection = moveDirection;
            _viewPointForward = viewPointForward;
        }
        
        public void SetRotationOffset(float offset)
        {
            RotationOffset = offset;
        }
        
#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null || paramData.Count == 0)
            {
                RotationOffset = 0.1f;
                return;
            }

            if (float.TryParse(paramData[0].ToString(), out var val))
                RotationOffset = val;
            else
                RotationOffset = 0.1f;
        }

        public List<object> EncodeExcelData()
        {
            var result = new List<object> { RotationOffset };
            return result;
        }
#endif
    }
}
