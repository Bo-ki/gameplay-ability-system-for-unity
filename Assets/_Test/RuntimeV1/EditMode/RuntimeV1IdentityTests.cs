using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 强类型身份的值语义与冻结校验顺序。
    /// </summary>
    [TestFixture]
    public class RuntimeV1IdentityTests
    {
        private const ulong Epoch = 71;
        private static readonly OwnerAscHandle OwnerAsc = new OwnerAscHandle(1201, 4);

        /// <summary>
        /// 验证各领域句柄只在完整身份相同时值相等，且默认值保持无效。
        /// </summary>
        [Test]
        public void 领域句柄_实现完整值相等与默认无效()
        {
            Assert.That(new GrantedAbilityHandle(Epoch, OwnerAsc, 2, 8),
                Is.EqualTo(new GrantedAbilityHandle(Epoch, OwnerAsc, 2, 8)));
            Assert.That(new AbilityActivationHandle(Epoch, OwnerAsc, 3, 9),
                Is.Not.EqualTo(new AbilityActivationHandle(Epoch, OwnerAsc, 3, 10)));
            Assert.That(new AbilityContinuationHandle(Epoch, OwnerAsc, 4, 11).IsValid, Is.True);
            Assert.That(new AbilitySubscriptionHandle(Epoch, OwnerAsc, 5, 12).IsValid, Is.True);
            Assert.That(new CooldownGateHandle(Epoch, OwnerAsc, 6, 13).IsValid, Is.True);
            Assert.That(new ActiveEffectHandle(Epoch, OwnerAsc, 7, 14).IsValid, Is.True);
            Assert.That(default(GrantedAbilityHandle).IsValid, Is.False);
            Assert.That(default(ActiveEffectHandle).IsValid, Is.False);
        }

        /// <summary>
        /// 验证诊断载体严格按 Epoch、Kind、Owner、Index、Live、Generation 返回首个失败。
        /// </summary>
        [Test]
        public void 槽句柄校验_严格返回冻结顺序中的首个失败()
        {
            var wrongEpochAndRest = CreateCarrier(Epoch + 1, new OwnerAscHandle(99, 2), 20, 30, HandleKind.ActiveEffect);
            AssertValidation(wrongEpochAndRest, Epoch, OwnerAsc, 8, false, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.EpochMismatch);

            var wrongKindAndRest = CreateCarrier(Epoch, new OwnerAscHandle(99, 2), 20, 30, HandleKind.ActiveEffect);
            AssertValidation(wrongKindAndRest, Epoch, OwnerAsc, 8, false, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.KindMismatch);

            var wrongOwnerAndRest = CreateCarrier(Epoch, new OwnerAscHandle(99, 2), 20, 30, HandleKind.GrantedAbility);
            AssertValidation(wrongOwnerAndRest, Epoch, OwnerAsc, 8, false, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.OwnerMismatch);

            var wrongIndexAndRest = CreateCarrier(Epoch, OwnerAsc, 8, 30, HandleKind.GrantedAbility);
            AssertValidation(wrongIndexAndRest, Epoch, OwnerAsc, 8, false, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.SlotIndexOutOfRange);

            var deadAllocationAndRest = CreateCarrier(Epoch, OwnerAsc, 3, 30, HandleKind.GrantedAbility);
            AssertValidation(deadAllocationAndRest, Epoch, OwnerAsc, 8, false, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.AllocationNotLive);

            var wrongGeneration = CreateCarrier(Epoch, OwnerAsc, 3, 30, HandleKind.GrantedAbility);
            AssertValidation(wrongGeneration, Epoch, OwnerAsc, 8, true, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.GenerationMismatch);
        }

        /// <summary>
        /// 验证合法句柄通过纯校验且诊断载体保留领域种类。
        /// </summary>
        [Test]
        public void 槽句柄校验_完整身份匹配时通过()
        {
            var handle = new GrantedAbilityHandle(Epoch, OwnerAsc, 3, 7);
            var carrier = handle.ToDiagnosticCarrier();

            Assert.That(carrier.Kind, Is.EqualTo(HandleKind.GrantedAbility));
            AssertValidation(carrier, Epoch, OwnerAsc, 8, true, 7, HandleKind.GrantedAbility,
                HandleValidationFailure.None);
        }

        /// <summary>
        /// 验证双方默认或越界值即使相等也不能伪装成合法稳定槽身份。
        /// </summary>
        [Test]
        public void 槽句柄校验_拒绝双方相等的无效身份()
        {
            AssertValidation(default, 0, default, 1, true, 0, HandleKind.None,
                HandleValidationFailure.EpochMismatch);
            AssertValidation(CreateCarrier(Epoch, OwnerAsc, 0, 1, HandleKind.None),
                Epoch, OwnerAsc, 1, true, 1, HandleKind.None,
                HandleValidationFailure.KindMismatch);
            var invalidKind = (HandleKind)byte.MaxValue;
            AssertValidation(CreateCarrier(Epoch, OwnerAsc, 0, 1, invalidKind),
                Epoch, OwnerAsc, 1, true, 1, invalidKind,
                HandleValidationFailure.KindMismatch);
            AssertValidation(CreateCarrier(Epoch, default, 0, 1, HandleKind.GrantedAbility),
                Epoch, default, 1, true, 1, HandleKind.GrantedAbility,
                HandleValidationFailure.OwnerMismatch);
            AssertValidation(CreateCarrier(Epoch, OwnerAsc, 0, 0, HandleKind.GrantedAbility),
                Epoch, OwnerAsc, 1, true, 0, HandleKind.GrantedAbility,
                HandleValidationFailure.GenerationMismatch);
        }

        /// <summary>
        /// 验证新 World 或 Session 的 Epoch 会先于相同 owner 与 slot 拒绝旧句柄。
        /// </summary>
        [Test]
        public void World或Session重建_旧槽句柄因Epoch失效()
        {
            var oldHandle = new ActiveEffectHandle(Epoch, OwnerAsc, 1, 5);

            AssertValidation(oldHandle.ToDiagnosticCarrier(), Epoch + 1, OwnerAsc, 4, true, 5,
                HandleKind.ActiveEffect, HandleValidationFailure.EpochMismatch);
        }

        /// <summary>
        /// 验证 BattleInstance 句柄分别拒绝跨 Session Epoch 与复用后的旧代际。
        /// </summary>
        [Test]
        public void BattleInstance句柄_拒绝Epoch与Generation不匹配()
        {
            var oldBattle = new BattleInstanceHandle(Epoch, 501, 2);

            Assert.That(
                BattleInstanceHandleValidator.Validate(in oldBattle, Epoch + 1, 501, 2),
                Is.EqualTo(BattleHandleValidationFailure.EpochMismatch));
            Assert.That(
                BattleInstanceHandleValidator.Validate(in oldBattle, Epoch, 501, 3),
                Is.EqualTo(BattleHandleValidationFailure.GenerationMismatch));
            Assert.That(new BattleInstanceHandle(Epoch, 501, 2), Is.EqualTo(oldBattle));
        }

        /// <summary>
        /// 验证 Battle 双方默认字段相等时仍按 Epoch、StableId、Generation 顺序拒绝。
        /// </summary>
        [Test]
        public void BattleInstance句柄_拒绝双方相等的默认身份()
        {
            var defaultBattle = default(BattleInstanceHandle);
            Assert.That(BattleInstanceHandleValidator.Validate(in defaultBattle, 0, 0, 0),
                Is.EqualTo(BattleHandleValidationFailure.EpochMismatch));
            var missingStableId = new BattleInstanceHandle(Epoch, 0, 1);
            Assert.That(BattleInstanceHandleValidator.Validate(in missingStableId, Epoch, 0, 1),
                Is.EqualTo(BattleHandleValidationFailure.StableIdMismatch));
            var missingGeneration = new BattleInstanceHandle(Epoch, 501, 0);
            Assert.That(BattleInstanceHandleValidator.Validate(in missingGeneration, Epoch, 501, 0),
                Is.EqualTo(BattleHandleValidationFailure.GenerationMismatch));
        }

        /// <summary>
        /// 验证 payload range 的用途参与值身份并使默认 range 保持无效。
        /// </summary>
        [Test]
        public void PayloadRange句柄_Kind参与值身份()
        {
            var capture = new PayloadRangeHandle(Epoch, OwnerAsc, 10, 4, 3, PayloadKind.Capture);
            var payload = new PayloadRangeHandle(Epoch, OwnerAsc, 10, 4, 3, PayloadKind.ActiveEffectPayload);

            Assert.That(capture.IsValid, Is.True);
            Assert.That(capture, Is.Not.EqualTo(payload));
            Assert.That(default(PayloadRangeHandle).IsValid, Is.False);
            Assert.That(new PayloadRangeHandle(
                Epoch, OwnerAsc, 0, 1, 1, (PayloadKind)byte.MaxValue).IsValid, Is.False);
        }

        /// <summary>
        /// 创建指定字段的无类型诊断载体以验证校验优先级。
        /// </summary>
        private static StableHandleDiagnosticCarrier CreateCarrier(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration,
            HandleKind kind)
        {
            return new StableHandleDiagnosticCarrier(
                simulationEpoch,
                ownerAsc,
                slotIndex,
                slotGeneration,
                kind);
        }

        /// <summary>
        /// 断言纯校验器返回预期的首个失败阶段。
        /// </summary>
        private static void AssertValidation(
            StableHandleDiagnosticCarrier handle,
            ulong expectedSimulationEpoch,
            OwnerAscHandle expectedOwnerAsc,
            int slotCount,
            bool allocationIsLive,
            uint allocationGeneration,
            HandleKind expectedKind,
            HandleValidationFailure expectedFailure)
        {
            var failure = StableHandleValidator.Validate(
                in handle,
                expectedSimulationEpoch,
                in expectedOwnerAsc,
                slotCount,
                allocationIsLive,
                allocationGeneration,
                expectedKind);

            Assert.That(failure, Is.EqualTo(expectedFailure));
        }
    }
}
