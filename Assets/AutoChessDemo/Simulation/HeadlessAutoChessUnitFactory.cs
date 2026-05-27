using GAS.Runtime.Generated;
using Unity.Entities;

namespace GAS.Runtime
{
    internal static class HeadlessAutoChessUnitFactory
    {
        public static Entity CreateScenarioUnit(
            EntityManager em,
            int spawnIndex,
            in AutoChessScenarioSpawnEntry spawn)
        {
            var unitDefinition = AutoChessUnitConfigTable.TryGetDefinition(spawn.UnitCode, out var generatedUnit)
                ? generatedUnit
                : CreateFallbackUnitDefinition(in spawn);
            return CreateUnit(em, spawnIndex, in spawn, in unitDefinition);
        }

        public static Entity CreateSummonedUnit(
            EntityManager em,
            int summonSerial,
            Entity ownerAsc,
            Entity sourceAbility,
            Entity sourceGameplayEffect,
            int summonGameplayEffectCode,
            int frame,
            int turn,
            in CHeadlessAutoChessUnit owner,
            in CHeadlessAutoChessSummonRequest request)
        {
            var spawn = new AutoChessScenarioSpawnEntry
            {
                UnitCode = request.SummonedUnitCode,
                Team = owner.Team,
                BoardX = owner.BoardX + request.BoardXOffset,
                BoardY = owner.BoardY + request.BoardYOffset,
                TurnOrder = owner.TurnOrder + request.TurnOrderOffset + summonSerial,
                PrimaryAbilityCode = request.PrimaryAbilityCode,
                ManaAbilityCode = 0,
                ControlAbilityCode = 0,
                SupportAbilityCode = 0,
                SummonAbilityCode = 0,
                Health = request.Health,
                Mana = request.Mana,
                Shield = request.Shield,
                ArcaneResistance = request.ArcaneResistance,
            };
            var slot = owner.Slot + request.SlotOffset + summonSerial;
            var unitDefinition = CreateSummonedUnitDefinition(in request);
            var asc = CreateUnit(em, slot, in spawn, in unitDefinition);
            em.SetName(asc, $"AutoChessSummon_{request.SummonedUnitCode}_{owner.Team}_{summonSerial}");
            em.AddComponentData(asc, new CHeadlessAutoChessSummonedUnit
            {
                OwnerAsc = ownerAsc,
                SourceAbility = sourceAbility,
                SourceGameplayEffect = sourceGameplayEffect,
                SummonedUnitCode = request.SummonedUnitCode,
                SummonGameplayEffectCode = summonGameplayEffectCode,
                SummonSerial = summonSerial,
                SpawnFrame = frame,
                SpawnTurn = turn,
                ExpireTurn = request.LifetimeTurns > 0 ? turn + request.LifetimeTurns : 0,
            });
            return asc;
        }

        private static Entity CreateUnit(
            EntityManager em,
            int spawnIndex,
            in AutoChessScenarioSpawnEntry spawn,
            in AutoChessUnitDefinition unitDefinition)
        {
            var asc = AbilitySystemEntityFactory.Create(em);
            em.SetName(asc, $"AutoChessUnit_{spawn.UnitCode}_{spawn.Team}_{spawnIndex}");

            var health = PickSpawnValue(spawn.Health, unitDefinition.Health);
            var mana = PickSpawnValue(spawn.Mana, unitDefinition.Mana);
            var shield = PickSpawnValue(spawn.Shield, unitDefinition.Shield);
            var arcaneResistance = PickSpawnValue(spawn.ArcaneResistance, unitDefinition.ArcaneResistance);

            AddGeneratedAttributeComponents(em, asc, health, mana, shield, arcaneResistance);
            AddAutoChessUnitComponents(em, asc, spawnIndex, in spawn, in unitDefinition);
            CreateAscInitializeRequest(em, asc, in unitDefinition, health, mana, shield, arcaneResistance);
            return asc;
        }

        private static AutoChessUnitDefinition CreateFallbackUnitDefinition(in AutoChessScenarioSpawnEntry spawn)
        {
            return new AutoChessUnitDefinition
            {
                UnitCode = spawn.UnitCode,
                PrimaryAbilityCode = spawn.PrimaryAbilityCode,
                ManaAbilityCode = spawn.ManaAbilityCode,
                ControlAbilityCode = spawn.ControlAbilityCode,
                SupportAbilityCode = spawn.SupportAbilityCode,
                SummonAbilityCode = spawn.SummonAbilityCode,
                Health = spawn.Health,
                Mana = spawn.Mana,
                Shield = spawn.Shield,
                ArcaneResistance = spawn.ArcaneResistance,
                MaxHealth = GetGeneratedMaxValue(AutoChessAttributeCodes.Health),
                MaxMana = GetGeneratedMaxValue(AutoChessAttributeCodes.Mana),
                MaxShield = GetGeneratedMaxValue(AutoChessAttributeCodes.Shield),
                MaxArcaneResistance = GetGeneratedMaxValue(AutoChessAttributeCodes.ArcaneResistance),
                PrimaryTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                ManaTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                ControlTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                SupportTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
            };
        }

        private static AutoChessUnitDefinition CreateSummonedUnitDefinition(
            in CHeadlessAutoChessSummonRequest request)
        {
            var definition = AutoChessUnitConfigTable.TryGetDefinition(request.SummonedUnitCode, out var generated)
                ? generated
                : default;

            definition.UnitCode = request.SummonedUnitCode;
            definition.FixedTagCode = request.FixedTagCode > 0 ? request.FixedTagCode : definition.FixedTagCode;
            definition.PrimaryAbilityCode = PickCode(request.PrimaryAbilityCode, definition.PrimaryAbilityCode);
            definition.ManaAbilityCode = 0;
            definition.ControlAbilityCode = 0;
            definition.SupportAbilityCode = 0;
            definition.SummonAbilityCode = 0;
            definition.Health = PickSpawnValue(request.Health, definition.Health);
            definition.Mana = PickSpawnValue(request.Mana, definition.Mana);
            definition.Shield = PickSpawnValue(request.Shield, definition.Shield);
            definition.ArcaneResistance = PickSpawnValue(request.ArcaneResistance, definition.ArcaneResistance);
            definition.MaxHealth = PickSpawnValue(request.MaxHealth, definition.MaxHealth);
            definition.MaxMana = PickSpawnValue(request.MaxMana, definition.MaxMana);
            definition.MaxShield = PickSpawnValue(request.MaxShield, definition.MaxShield);
            definition.MaxArcaneResistance = PickSpawnValue(
                request.MaxArcaneResistance,
                definition.MaxArcaneResistance);
            definition.PrimaryTargetPolicy = request.PrimaryTargetPolicy;
            definition.ManaTargetPolicy = request.PrimaryTargetPolicy;
            definition.ControlTargetPolicy = request.PrimaryTargetPolicy;
            definition.SupportTargetPolicy = request.PrimaryTargetPolicy;
            return definition;
        }

        private static void AddGeneratedAttributeComponents(
            EntityManager em,
            Entity asc,
            float health,
            float mana,
            float shield,
            float arcaneResistance)
        {
            em.AddComponentData(asc, HealthAttribute.Create(health));
            em.AddComponentData(asc, ManaAttribute.Create(mana));
            em.AddComponentData(asc, ShieldAttribute.Create(shield));
            em.AddComponentData(asc, ArcaneResistanceAttribute.Create(arcaneResistance));
            em.AddComponentData(asc, CounterDamageAttribute.Create(0f));
            em.AddComponentData(asc, LifeStealRatioAttribute.Create(0f));
        }

        private static void AddAutoChessUnitComponents(
            EntityManager em,
            Entity asc,
            int spawnIndex,
            in AutoChessScenarioSpawnEntry spawn,
            in AutoChessUnitDefinition unitDefinition)
        {
            em.AddComponentData(asc, new CHeadlessAutoChessUnit
            {
                Team = spawn.Team,
                Slot = spawnIndex,
                BoardX = spawn.BoardX,
                BoardY = spawn.BoardY,
                TurnOrder = spawn.TurnOrder,
                PrimaryAbilityCode = PickCode(spawn.PrimaryAbilityCode, unitDefinition.PrimaryAbilityCode),
                ManaAbilityCode = PickCode(spawn.ManaAbilityCode, unitDefinition.ManaAbilityCode),
                ControlAbilityCode = PickCode(spawn.ControlAbilityCode, unitDefinition.ControlAbilityCode),
                SupportAbilityCode = PickCode(spawn.SupportAbilityCode, unitDefinition.SupportAbilityCode),
                SummonAbilityCode = PickCode(spawn.SummonAbilityCode, unitDefinition.SummonAbilityCode),
                HealthAttrSetCode = HealthAttribute.AttributeSetCode,
                HealthAttrCode = HealthAttribute.AttributeCode,
                ManaAttrSetCode = ManaAttribute.AttributeSetCode,
                ManaAttrCode = ManaAttribute.AttributeCode,
                ShieldAttrSetCode = ShieldAttribute.AttributeSetCode,
                ShieldAttrCode = ShieldAttribute.AttributeCode,
                ArcaneResistanceAttrSetCode = ArcaneResistanceAttribute.AttributeSetCode,
                ArcaneResistanceAttrCode = ArcaneResistanceAttribute.AttributeCode,
                CounterDamageAttrSetCode = CounterDamageAttribute.AttributeSetCode,
                CounterDamageAttrCode = CounterDamageAttribute.AttributeCode,
                LifeStealRatioAttrSetCode = LifeStealRatioAttribute.AttributeSetCode,
                LifeStealRatioAttrCode = LifeStealRatioAttribute.AttributeCode,
                PrimaryCooldownTagIndex = GetCooldownTagIndex(PickCode(
                    spawn.PrimaryAbilityCode,
                    unitDefinition.PrimaryAbilityCode)),
                ManaCooldownTagIndex = GetCooldownTagIndex(PickCode(
                    spawn.ManaAbilityCode,
                    unitDefinition.ManaAbilityCode)),
                ControlCooldownTagIndex = GetCooldownTagIndex(PickCode(
                    spawn.ControlAbilityCode,
                    unitDefinition.ControlAbilityCode)),
                SupportCooldownTagIndex = GetCooldownTagIndex(PickCode(
                    spawn.SupportAbilityCode,
                    unitDefinition.SupportAbilityCode)),
                SummonCooldownTagIndex = GetCooldownTagIndex(PickCode(
                    spawn.SummonAbilityCode,
                    unitDefinition.SummonAbilityCode)),
                CrowdControlTagIndex = AutoChessTagBits.AutoChessStunnedIndex,
                ManaAbilityThreshold = unitDefinition.MaxMana,
                MaxActiveSummons = HeadlessAutoChessScenario.PlayerSummonMaxActiveCount,
                PrimaryTargetPolicy = unitDefinition.PrimaryTargetPolicy,
                ManaTargetPolicy = unitDefinition.ManaTargetPolicy,
                ControlTargetPolicy = unitDefinition.ControlTargetPolicy,
                SupportTargetPolicy = unitDefinition.SupportTargetPolicy,
            });
            em.AddComponentData(asc, new CHeadlessAutoChessAbilitySlots());
            em.AddComponentData(asc, new CHeadlessAutoChessDamageState());
            em.AddComponentData(asc, new CHeadlessAutoChessDeathState());
            em.AddComponentData(asc, new CHeadlessAutoChessPassiveState());
            em.AddComponentData(asc, new CHeadlessAutoChessSynergyState());
        }

        private static void CreateAscInitializeRequest(
            EntityManager em,
            Entity asc,
            in AutoChessUnitDefinition unitDefinition,
            float health,
            float mana,
            float shield,
            float arcaneResistance)
        {
            var request = em.CreateEntity();
            em.SetName(request, $"AutoChessAscInitialize_{unitDefinition.UnitCode}_{asc.Index}");
            em.AddComponentData(request, new CAscInitializeRequest
            {
                ASC = asc,
                Level = 1,
            });

            if (unitDefinition.FixedTagCode > 0)
            {
                var fixedTags = em.AddBuffer<BAscInitFixedTag>(request);
                fixedTags.Add(new BAscInitFixedTag
                {
                    TagCode = unitDefinition.FixedTagCode,
                });
            }

            var attributes = em.AddBuffer<BAscInitAttribute>(request);
            AddAttribute(attributes, AutoChessAttributeCodes.Health, health, 0f, unitDefinition.MaxHealth);
            AddAttribute(attributes, AutoChessAttributeCodes.Mana, mana, 0f, unitDefinition.MaxMana);
            AddAttribute(attributes, AutoChessAttributeCodes.Shield, shield, 0f, unitDefinition.MaxShield);
            AddAttribute(
                attributes,
                AutoChessAttributeCodes.ArcaneResistance,
                arcaneResistance,
                0f,
                unitDefinition.MaxArcaneResistance);
            AddAttribute(
                attributes,
                AutoChessAttributeCodes.CounterDamage,
                0f,
                0f,
                GetGeneratedMaxValue(AutoChessAttributeCodes.CounterDamage));
            AddAttribute(
                attributes,
                AutoChessAttributeCodes.LifeStealRatio,
                0f,
                0f,
                GetGeneratedMaxValue(AutoChessAttributeCodes.LifeStealRatio));

            var abilities = em.AddBuffer<BAscInitAbility>(request);
            AddAbility(abilities, unitDefinition.PrimaryAbilityCode);
            AddAbility(abilities, unitDefinition.ManaAbilityCode);
            AddAbility(abilities, unitDefinition.ControlAbilityCode);
            AddAbility(abilities, unitDefinition.SupportAbilityCode);
            AddAbility(abilities, unitDefinition.SummonAbilityCode);
        }

        private static void AddAttribute(
            DynamicBuffer<BAscInitAttribute> attributes,
            int attributeCode,
            float value,
            float minValue,
            float maxValue)
        {
            attributes.Add(new BAscInitAttribute
            {
                AttrSetCode = HealthAttribute.AttributeSetCode,
                AttributeCode = attributeCode,
                BaseValue = AutoChessAttributeAccessor.Clamp(attributeCode, value),
                IsClampMin = true,
                IsClampMax = true,
                MinValue = minValue,
                MaxValue = maxValue,
            });
        }

        private static void AddAbility(DynamicBuffer<BAscInitAbility> abilities, int abilityCode)
        {
            if (abilityCode <= 0 || ContainsAbility(abilities, abilityCode))
                return;

            abilities.Add(new BAscInitAbility
            {
                AbilityCode = abilityCode,
            });
        }

        private static bool ContainsAbility(DynamicBuffer<BAscInitAbility> abilities, int abilityCode)
        {
            for (var i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].AbilityCode == abilityCode)
                    return true;
            }

            return false;
        }

        private static int PickCode(int spawnCode, int unitCode)
        {
            return spawnCode > 0 ? spawnCode : unitCode;
        }

        private static float PickSpawnValue(float spawnValue, float unitValue)
        {
            return spawnValue > 0f ? spawnValue : unitValue;
        }

        private static float GetGeneratedMaxValue(int attributeCode)
        {
            return AutoChessAttributeAccessor.TryGetDefinition(attributeCode, out var definition)
                ? definition.MaxValue
                : 0f;
        }

        private static int GetCooldownTagIndex(int abilityCode)
        {
            return abilityCode switch
            {
                HeadlessAutoChessScenario.AbilityPlayerManaBurst => AutoChessTagBits.ManaBurstCooldownIndex,
                HeadlessAutoChessScenario.AbilityPlayerControlStun => AutoChessTagBits.PlayerStunCooldownIndex,
                HeadlessAutoChessScenario.AbilityPlayerBarrier => AutoChessTagBits.PlayerBarrierCooldownIndex,
                HeadlessAutoChessScenario.AbilityPlayerSummon => AutoChessTagBits.PlayerSummonCooldownIndex,
                HeadlessAutoChessScenario.AbilityPlayerCleanse => AutoChessTagBits.PlayerCleanseCooldownIndex,
                _ => -1,
            };
        }
    }
}
