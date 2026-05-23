using DemoForESC._Script.UI.View;
using EXUI;
using GAS.Runtime;

namespace DemoForESC._Script.Gas.Cue
{
    public class CueDodgeCooldownUI : GameplayCueBase<XParamNone>
    {
        public override void OnAdd(float time)
        {
            base.OnAdd(time);

            // 通知 VM：冷却开始  
            var w = XUI.M.Windows<MainWindow>();
            w?.VM.SetDodgeVisible(true);
        }

        public override void OnTick(float time)
        {
            base.OnTick(time);

            var effect = GetSourceEffectEntity();
            if (effect == Unity.Entities.Entity.Null
                || !EntityManager.Exists(effect)
                || !EntityManager.HasComponent<CDurationRuntime>(effect))
                return;

            var durationComponent = EntityManager.GetComponentData<CDurationRuntime>(effect);
            var duration = durationComponent.ResolvedDuration;
            var startTime = durationComponent.ActiveTime;
            var currentGasTime = GASManager.CurrentFrame;
            
            var w = XUI.M.Windows<MainWindow>();
            w?.VM.UpdateDodgeCd(currentGasTime-startTime,duration);
        }

        public override void OnRemove(float time)
        {
            base.OnRemove(time);
            var w = XUI.M.Windows<MainWindow>();
            w?.VM.SetDodgeVisible(false);
        }
        
        public override void OnDeactivate(float time)
        {
            var w = XUI.M.Windows<MainWindow>();
            w?.VM.SetDodgeVisible(false);
        }
    }
}
