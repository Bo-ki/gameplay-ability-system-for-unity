using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests
{
    public sealed class TagRequirementEvaluatorTests
    {
        [Test]
        public void RequiredBlockedAndImmunityModesShareOneFailureContract()
        {
            var ownerTags = CreateMask(1, 4);

            var requiredPass = TagRequirementEvaluator.EvaluateRequired(
                ownerTags,
                new TagRequirementMask
                {
                    All = CreateMask(1),
                    Any = CreateMask(2, 4),
                    None = CreateMask(3),
                });
            Assert.That(requiredPass.Passed, Is.True);
            Assert.That(requiredPass.Failure, Is.EqualTo(ETagRequirementFailure.None));

            var requiredFail = TagRequirementEvaluator.EvaluateRequired(
                ownerTags,
                new TagRequirementMask { All = CreateMask(2) });
            Assert.That(requiredFail.Passed, Is.False);
            Assert.That(requiredFail.Failure, Is.EqualTo(ETagRequirementFailure.RequiredTagsNotMet));

            var blockedFail = TagRequirementEvaluator.EvaluateBlocked(
                ownerTags,
                new TagRequirementMask { None = CreateMask(4) });
            Assert.That(blockedFail.Passed, Is.False);
            Assert.That(blockedFail.Failure, Is.EqualTo(ETagRequirementFailure.BlockedTagsMatched));

            var immunityFail = TagRequirementEvaluator.EvaluateImmunity(
                ownerTags,
                new TagRequirementMask { All = CreateMask(1) });
            Assert.That(immunityFail.Passed, Is.False);
            Assert.That(immunityFail.Failure, Is.EqualTo(ETagRequirementFailure.ImmunityTagsMatched));
        }

        [Test]
        public void EntityEvaluationReportsMissingSubjectAndTagMask()
        {
            var world = new World(nameof(EntityEvaluationReportsMissingSubjectAndTagMask));
            var entityManager = world.EntityManager;

            try
            {
                var missingSubject = TagRequirementEvaluator.EvaluateRequired(
                    entityManager,
                    Entity.Null,
                    default);
                Assert.That(missingSubject.Passed, Is.False);
                Assert.That(missingSubject.Failure, Is.EqualTo(ETagRequirementFailure.MissingSubject));

                var subject = entityManager.CreateEntity();
                var missingMask = TagRequirementEvaluator.EvaluateRequired(
                    entityManager,
                    subject,
                    default);
                Assert.That(missingMask.Passed, Is.False);
                Assert.That(missingMask.Failure, Is.EqualTo(ETagRequirementFailure.MissingTagMask));

                entityManager.AddComponentData(subject, new CTagMask());
                var validSubject = TagRequirementEvaluator.EvaluateRequired(
                    entityManager,
                    subject,
                    default);
                Assert.That(validSubject.Passed, Is.True);
            }
            finally
            {
                world.Dispose();
            }
        }

        private static CTagMask CreateMask(params int[] tagIndices)
        {
            var mask = new CTagMask();
            for (var i = 0; i < tagIndices.Length; i++)
                mask.AddTag(tagIndices[i]);

            return mask;
        }
    }
}
