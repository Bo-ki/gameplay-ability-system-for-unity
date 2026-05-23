using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime.Tests.Effect
{
    public sealed class GameplayEffectRequestWriterTests
    {
        private EntityManager _em;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

        [SetUp]
        public void SetUp()
        {
            _em = GASManager.EntityManager;
        }

        [Test]
        public void CreateSelfRequestWritesHeaderTargetAndSetByCaller()
        {
            var source = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = 10001,
                        Level = 2,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.Self,
                    });

                GameplayEffectRequestWriter.AddTarget(_em, request, source);
                GameplayEffectRequestWriter.AddSetByCallerValues(
                    _em,
                    request,
                    new[]
                    {
                        new BSetByCallerValue { Key = 3001, Value = 12f },
                        new BSetByCallerValue { Key = 3002, Value = 34f },
                    });

                Assert.That(_em.HasComponent<CApplyGameplayEffectRequest>(request), Is.True);
                Assert.That(_em.HasComponent<CTargetDataHeader>(request), Is.True);

                var header = _em.GetComponentData<CTargetDataHeader>(request);
                Assert.That(header.SourceAsc, Is.EqualTo(source));
                Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.Self));

                var targets = _em.GetBuffer<BTargetEntity>(request);
                Assert.That(targets.Length, Is.EqualTo(1));
                Assert.That(targets[0].TargetAsc, Is.EqualTo(source));

                var values = _em.GetBuffer<BSetByCallerValue>(request);
                Assert.That(values.Length, Is.EqualTo(2));
                Assert.That(values[0].Key, Is.EqualTo(3001));
                Assert.That(values[0].Value, Is.EqualTo(12f));
                Assert.That(values[1].Key, Is.EqualTo(3002));
                Assert.That(values[1].Value, Is.EqualTo(34f));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void AddTargetAllowsEntityListRequests()
        {
            var source = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        GameplayEffectCode = 10002,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.EntityList,
                    });

                GameplayEffectRequestWriter.AddTarget(_em, request, targetA);
                GameplayEffectRequestWriter.AddTarget(_em, request, targetB);

                var header = _em.GetComponentData<CTargetDataHeader>(request);
                Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.EntityList));

                var targets = _em.GetBuffer<BTargetEntity>(request);
                Assert.That(targets.Length, Is.EqualTo(2));
                Assert.That(targets[0].TargetAsc, Is.EqualTo(targetA));
                Assert.That(targets[1].TargetAsc, Is.EqualTo(targetB));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(source);
                DestroyIfExists(targetA);
                DestroyIfExists(targetB);
            }
        }

        [Test]
        public void RequestEntityCanCarryOptionalPointDirectionAndHitBuffers()
        {
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var request = Entity.Null;

            try
            {
                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        GameplayEffectCode = 10003,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.Entity,
                    });

                GameplayEffectRequestWriter.AddTarget(_em, request, target);
                _em.AddBuffer<BTargetPoint>(request).Add(new BTargetPoint
                {
                    Position = new float3(1f, 2f, 3f),
                });
                _em.AddBuffer<BTargetDirection>(request).Add(new BTargetDirection
                {
                    Direction = new float3(0f, 0f, 1f),
                });
                _em.AddBuffer<BTargetHit>(request).Add(new BTargetHit
                {
                    HitEntity = target,
                    Position = new float3(4f, 5f, 6f),
                    Normal = new float3(0f, 1f, 0f),
                    SurfaceCode = 7,
                });

                Assert.That(_em.GetBuffer<BTargetPoint>(request)[0].Position, Is.EqualTo(new float3(1f, 2f, 3f)));
                Assert.That(_em.GetBuffer<BTargetDirection>(request)[0].Direction, Is.EqualTo(new float3(0f, 0f, 1f)));

                var hit = _em.GetBuffer<BTargetHit>(request)[0];
                Assert.That(hit.HitEntity, Is.EqualTo(target));
                Assert.That(hit.Position, Is.EqualTo(new float3(4f, 5f, 6f)));
                Assert.That(hit.Normal, Is.EqualTo(new float3(0f, 1f, 0f)));
                Assert.That(hit.SurfaceCode, Is.EqualTo(7));
            }
            finally
            {
                DestroyIfExists(request);
                DestroyIfExists(source);
                DestroyIfExists(target);
            }
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }
    }
}
