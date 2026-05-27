using GAS.Runtime.Generated;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateBefore(typeof(SAscInitializeRequest))]
    public partial struct SHeadlessAutoChessScenarioBootstrap : ISystem
    {
        private EntityQuery _requestQuery;

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessScenarioBootstrapRequest>()
                .Build();
            state.RequireForUpdate(_requestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var requests = _requestQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                if (!em.Exists(requestEntity) ||
                    !em.HasComponent<CHeadlessAutoChessScenarioBootstrapRequest>(requestEntity))
                {
                    continue;
                }

                var request = em.GetComponentData<CHeadlessAutoChessScenarioBootstrapRequest>(requestEntity);
                var plan = AutoChessScenarioBuildPlanFactory.CreateDefault();
                CreateScenario(em, in request, in plan);
                em.DestroyEntity(requestEntity);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void CreateScenario(
            EntityManager em,
            in CHeadlessAutoChessScenarioBootstrapRequest request,
            in AutoChessScenarioBuildPlan plan)
        {
            var driver = em.CreateEntity();
            em.SetName(driver, $"HeadlessAutoChessDriver_{request.ScenarioCode}");
            em.AddComponentData(driver, new CHeadlessAutoChessDriver
            {
                Enabled = true,
                ScenarioCode = request.ScenarioCode,
                Variant = request.Variant,
                DeterministicSeed = request.DeterministicSeed,
                SpawnedUnitCount = plan.SpawnEntries.Length,
                BoardWidth = plan.BoardWidth,
                BoardHeight = plan.BoardHeight,
                Round = 1,
                LastDecisionFrame = -1,
                Winner = HeadlessAutoChessTeam.None,
            });
            AddScenarioFactStores(em, driver);

            for (var i = 0; i < plan.SpawnEntries.Length; i++)
            {
                var spawn = plan.SpawnEntries[i];
                HeadlessAutoChessUnitFactory.CreateScenarioUnit(em, i, in spawn);
            }
        }

        private static void AddScenarioFactStores(EntityManager em, Entity driver)
        {
            em.AddComponentData(driver, new CHeadlessAutoChessBattleFacts());
            em.AddBuffer<BHeadlessAutoChessUnitDefeatedFact>(driver).EnsureCapacity(8);
            em.AddComponentData(driver, new CHeadlessAutoChessGameplayEffectFacts());
            em.AddBuffer<BHeadlessAutoChessGameplayEffectAppliedFact>(driver).EnsureCapacity(32);
            em.AddComponentData(driver, new CHeadlessAutoChessPassiveReactionFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessSynergyFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessSummonFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessCounterFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessCleanseFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessRallyComboFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessLifeStealFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessPoisonFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessExecuteFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessDeathBurstFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessEnrageFacts());
            em.AddComponentData(driver, new CHeadlessAutoChessPresentationCueMarkerFacts());
        }

    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAbilityCommandRequest))]
    [UpdateBefore(typeof(STryActivateAbility))]
    public partial struct SHeadlessAutoChessAbilitySlotLink : ISystem
    {
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BGrantedAbility>()
                .Build();
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var units = _unitQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                if (!em.Exists(asc))
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
                var grantedAbilities = em.GetBuffer<BGrantedAbility>(asc);
                var slots = em.HasComponent<CHeadlessAutoChessAbilitySlots>(asc)
                    ? em.GetComponentData<CHeadlessAutoChessAbilitySlots>(asc)
                    : default;

                LinkAbilitySlots(em, grantedAbilities, in unit, ref slots);

                if (em.HasComponent<CHeadlessAutoChessAbilitySlots>(asc))
                    em.SetComponentData(asc, slots);
                else
                    em.AddComponentData(asc, slots);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void LinkAbilitySlots(
            EntityManager em,
            DynamicBuffer<BGrantedAbility> grantedAbilities,
            in CHeadlessAutoChessUnit unit,
            ref CHeadlessAutoChessAbilitySlots slots)
        {
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (!TryGetAbilityCode(em, ability, out var abilityCode))
                    continue;

                if (abilityCode == unit.PrimaryAbilityCode)
                    slots.PrimaryAbility = ability;
                else if (abilityCode == unit.ManaAbilityCode)
                    slots.ManaAbility = ability;
                else if (abilityCode == unit.ControlAbilityCode)
                    slots.ControlAbility = ability;
                else if (abilityCode == unit.SupportAbilityCode)
                    slots.SupportAbility = ability;
                else if (abilityCode == unit.SummonAbilityCode)
                    slots.SummonAbility = ability;
            }
        }

        private static bool TryGetAbilityCode(EntityManager em, Entity ability, out int abilityCode)
        {
            abilityCode = 0;
            if (ability == Entity.Null || !em.Exists(ability))
                return false;

            if (em.HasComponent<CAbilityBaseInfo>(ability))
            {
                abilityCode = em.GetComponentData<CAbilityBaseInfo>(ability).Code;
                return abilityCode > 0;
            }

            if (!em.HasComponent<CAbilityConfig>(ability))
                return false;

            var config = em.GetComponentData<CAbilityConfig>(ability).Config;
            if (!config.IsCreated)
                return false;

            abilityCode = config.Value.Code;
            return abilityCode > 0;
        }
    }
}
