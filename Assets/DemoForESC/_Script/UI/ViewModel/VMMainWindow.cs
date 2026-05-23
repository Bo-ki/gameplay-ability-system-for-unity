using GAS.Runtime;
using Loxodon.Framework.Extension;
using UnityEngine;

namespace DemoForESC._Script.UI.ViewModel
{
    public class VMMainWindow : ViewModelCommon
    {
        public ObservableVariable<string> LabelPlayer { get; } = new();
        public ObservableVariable<string> HpText { get; } = new();
        public ObservableVariable<string> MpText { get; } = new();
        public ObservableVariable<string> SpText { get; } = new();
        public ObservableVariable<float> Hp { get; } = new();
        public ObservableVariable<float> Mp { get; } = new();
        public ObservableVariable<float> Sp { get; } = new();
        public ObservableVariable<float> DodgeCd { get; } = new();
        public ObservableVariable<string> DodgeName { get; } = new();
        public ObservableVariable<string> DodgeCdText { get; } = new();
        public ObservableVariable<int> DodgeLayer { get; } = new();

        private AbilitySystemBinding PlayerAbilitySystem
        {
            get
            {
                var player = DemoPlayer.Player();
                return player.AbilitySystem;
            }
        }

        public override void OnShow()
        {
            base.OnShow();
            LabelPlayer.Value = "[玩家]";
            DodgeName.Value = "闪避";
            RefreshState();
        }

        public override void Update_f()
        {
            RefreshState();
        }

        private void RefreshState()
        {
            var asc = PlayerAbilitySystem;
            var hp = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Hp);
            var hpMax = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.HpMax);
            HpText.Value = $"{hp}/{hpMax}";
            Hp.Value = hp / hpMax;

            var mp = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Mp);
            var mpMax = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.MpMax);
            MpText.Value = $"{mp}/{mpMax}";
            Mp.Value = mp / mpMax;

            var sp = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Sp);
            var spMax = asc.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.SpMax);
            SpText.Value = $"{sp}/{spMax}";
            Sp.Value = sp / spMax;
        }

        public void SetDodgeVisible(bool visible)
        {
            DodgeLayer.Value = LayerMask.NameToLayer(visible?"UI":"Hide");
        }
        
        public void UpdateDodgeCd(int currentTime,int duration)
        {
            DodgeCd.Value = (float)currentTime / duration;
            DodgeCdText.Value = $"{(duration-(float)currentTime)/60}s";
        }
    }
}
