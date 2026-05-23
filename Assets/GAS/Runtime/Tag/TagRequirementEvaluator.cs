using Unity.Entities;

namespace GAS.Runtime
{
    public enum ETagRequirementEvaluationMode : byte
    {
        Required = 0,
        Blocked = 1,
        Immunity = 2,
    }

    public enum ETagRequirementFailure : byte
    {
        None = 0,
        MissingSubject = 1,
        MissingTagMask = 2,
        MissingDefinition = 3,
        RequiredTagsNotMet = 4,
        BlockedTagsMatched = 5,
        ImmunityTagsMatched = 6,
    }

    public readonly struct TagRequirementEvaluationResult
    {
        public readonly bool Passed;
        public readonly ETagRequirementEvaluationMode Mode;
        public readonly ETagRequirementFailure Failure;
        public readonly Entity Subject;

        public TagRequirementEvaluationResult(
            bool passed,
            ETagRequirementEvaluationMode mode,
            ETagRequirementFailure failure,
            Entity subject = default)
        {
            Passed = passed;
            Mode = mode;
            Failure = failure;
            Subject = subject;
        }
    }

    public static class TagRequirementEvaluator
    {
        public static TagRequirementEvaluationResult Pass(
            ETagRequirementEvaluationMode mode,
            Entity subject = default)
        {
            return new TagRequirementEvaluationResult(true, mode, ETagRequirementFailure.None, subject);
        }

        public static TagRequirementEvaluationResult Fail(
            ETagRequirementEvaluationMode mode,
            ETagRequirementFailure failure,
            Entity subject = default)
        {
            return new TagRequirementEvaluationResult(false, mode, failure, subject);
        }

        public static TagRequirementEvaluationResult EvaluateRequired(
            EntityManager entityManager,
            Entity subject,
            in TagRequirementMask requirement)
        {
            return EvaluateSubject(entityManager, subject, requirement, ETagRequirementEvaluationMode.Required);
        }

        public static TagRequirementEvaluationResult EvaluateBlocked(
            EntityManager entityManager,
            Entity subject,
            in TagRequirementMask requirement)
        {
            return EvaluateSubject(entityManager, subject, requirement, ETagRequirementEvaluationMode.Blocked);
        }

        public static TagRequirementEvaluationResult EvaluateImmunity(
            EntityManager entityManager,
            Entity subject,
            in TagRequirementMask requirement)
        {
            return EvaluateSubject(entityManager, subject, requirement, ETagRequirementEvaluationMode.Immunity);
        }

        public static TagRequirementEvaluationResult EvaluateRequired(
            in CTagMask tags,
            in TagRequirementMask requirement,
            Entity subject = default)
        {
            return EvaluateMask(tags, requirement, ETagRequirementEvaluationMode.Required, subject);
        }

        public static TagRequirementEvaluationResult EvaluateBlocked(
            in CTagMask tags,
            in TagRequirementMask requirement,
            Entity subject = default)
        {
            return EvaluateMask(tags, requirement, ETagRequirementEvaluationMode.Blocked, subject);
        }

        public static TagRequirementEvaluationResult EvaluateImmunity(
            in CTagMask tags,
            in TagRequirementMask requirement,
            Entity subject = default)
        {
            return EvaluateMask(tags, requirement, ETagRequirementEvaluationMode.Immunity, subject);
        }

        private static TagRequirementEvaluationResult EvaluateSubject(
            EntityManager entityManager,
            Entity subject,
            in TagRequirementMask requirement,
            ETagRequirementEvaluationMode mode)
        {
            if (subject == Entity.Null || !entityManager.Exists(subject))
                return Fail(mode, ETagRequirementFailure.MissingSubject, subject);

            if (!entityManager.HasComponent<CTagMask>(subject))
                return Fail(mode, ETagRequirementFailure.MissingTagMask, subject);

            return EvaluateMask(entityManager.GetComponentData<CTagMask>(subject), requirement, mode, subject);
        }

        private static TagRequirementEvaluationResult EvaluateMask(
            in CTagMask tags,
            in TagRequirementMask requirement,
            ETagRequirementEvaluationMode mode,
            Entity subject)
        {
            return mode switch
            {
                ETagRequirementEvaluationMode.Required => requirement.Evaluate(tags)
                    ? Pass(mode, subject)
                    : Fail(mode, ETagRequirementFailure.RequiredTagsNotMet, subject),
                ETagRequirementEvaluationMode.Blocked => requirement.Evaluate(tags)
                    ? Pass(mode, subject)
                    : Fail(mode, ETagRequirementFailure.BlockedTagsMatched, subject),
                ETagRequirementEvaluationMode.Immunity => requirement.Evaluate(tags)
                    ? Fail(mode, ETagRequirementFailure.ImmunityTagsMatched, subject)
                    : Pass(mode, subject),
                _ => Fail(mode, ETagRequirementFailure.RequiredTagsNotMet, subject),
            };
        }
    }
}
