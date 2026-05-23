///////////////////////////////////
//// This is a generated file. ////
////   Modify BindData() only.  ////
///////////////////////////////////

using DemoForESC._Script;
using EXUI;
using GAS.Runtime;
using Loxodon.Framework.Extension;

namespace UI.ViewModel
{
    public class VMBossWindow : ViewModelCommon
    {
        public ObservableVariable<string> LabelBoss { get; } = new();
        public ObservableVariable<float> Value { get; } = new();
        public ObservableVariable<string> LabelValue { get; } = new();
        public ObservableVariable<string> LabelName { get; } = new();

        private BaseUnit _unit;
        
        public override void OnShow()
        {
            base.OnShow();
            LabelBoss.Value = "【训练假人】";
        }

        public override void OnHide()
        {
            base.OnHide();
        }

        public override void Update_f()
        {
            if (_unit != null)
                RefreshState();
        }

        private void RefreshState()
        {
            var asc = _unit.AbilitySystem;
            var hp = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Hp);
            var hpMax = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.HpMax);
            LabelValue.Value = $"{hp}/{hpMax}";
            Value.Value = hp / hpMax;
        }
        
        public void BindTargetHp(BaseUnit unit)
        {
            _unit = unit;
            RefreshState();
        }
    }
}
