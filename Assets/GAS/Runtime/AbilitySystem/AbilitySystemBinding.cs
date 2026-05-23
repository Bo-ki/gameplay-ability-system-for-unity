using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// GameObject 到 ASC Entity 的生命周期绑定。GAS 运行时状态只存在于 ECS Entity 中。
    /// </summary>
    public class AbilitySystemBinding : MonoBehaviour
    {
        private AbilitySystemFacade _abilitySystem;

        private void Awake()
        {
            _abilitySystem = AbilitySystemFacade.Create();
        }

        private void OnDestroy()
        {
            _abilitySystem.Dispose();
        }

        private void OnEnable()
        {
            EntityHelper.BindGameObjectToEntity(_abilitySystem.Entity, gameObject);
        }

        private void OnDisable()
        {
            EntityHelper.UnbindGameObjectToEntity(_abilitySystem.Entity);
        }

        public void Init(AbilitySystemConfig config)
        {
            _abilitySystem.Init(config);
        }

        public AbilitySystemFacade Facade => _abilitySystem;

        public Entity Entity => _abilitySystem.Entity;
    }
}
