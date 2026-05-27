///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;

namespace GAS.Runtime.Generated
{
    public struct AutoChessMagnitudeDefinition
    {
        public int GameplayEffectCode;
        public int ModifierIndex;
        public int AttributeSetCode;
        public int AttributeCode;
        public EModifierOp Operation;
        public float BaseMagnitude;
        public EMagnitudeSource Source;
        public int Key;
        public int DamageTypeCode;
        public int ResistanceAttributeSetCode;
        public int ResistanceAttributeCode;
        public float ResistanceCap;
    }

    public struct AutoChessMagnitudeEvaluationContext
    {
        public EMagnitudeSource Source;
        public float BaseMagnitude;
        public float SetByCallerMagnitude;
        public float SourceAttributeValue;
        public float TargetAttributeValue;
        public int StackCount;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    public static class AutoChessMmcEvaluator
    {
        public const int DefinitionCount = 20;

        public static bool TryGetDefinition(int gameplayEffectCode, int modifierIndex, out AutoChessMagnitudeDefinition definition)
        {
            switch (gameplayEffectCode)
            {
                case 9621 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9621,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 9f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9662,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9622 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9622,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 5f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9662,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9623 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9623,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 24f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9663,
                        ResistanceAttributeSetCode = 9601,
                        ResistanceAttributeCode = 4,
                        ResistanceCap = 0.75f,
                    };
                    return true;
                case 9624 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9624,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 2,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 3f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9626 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9626,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 2,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 2f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9627 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9627,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 18f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9628 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9628,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 2,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 1f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9630 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9630,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 3f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9663,
                        ResistanceAttributeSetCode = 9601,
                        ResistanceAttributeCode = 4,
                        ResistanceCap = 0.75f,
                    };
                    return true;
                case 9637 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9637,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 3,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 10f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9645 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9645,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 5,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 4f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9646 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9646,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 4f,
                        Source = EMagnitudeSource.SetByCaller,
                        Key = 9680,
                        DamageTypeCode = 9662,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9664 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9664,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 2,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 2f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9665 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9665,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 6f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9662,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9666 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9666,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 6,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 0.35f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9667 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9667,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 0f,
                        Source = EMagnitudeSource.SetByCaller,
                        Key = 9654,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9669 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9669,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 2f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9671,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9672 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9672,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 1f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9671,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9674 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9674,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 5f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9675,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9677 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9677,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 1,
                        Operation = EModifierOp.Subtract,
                        BaseMagnitude = 2f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 9678,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                case 9679 when modifierIndex == 0:
                    definition = new AutoChessMagnitudeDefinition
                    {
                        GameplayEffectCode = 9679,
                        ModifierIndex = 0,
                        AttributeSetCode = 9601,
                        AttributeCode = 5,
                        Operation = EModifierOp.Add,
                        BaseMagnitude = 2f,
                        Source = EMagnitudeSource.Constant,
                        Key = 0,
                        DamageTypeCode = 0,
                        ResistanceAttributeSetCode = 0,
                        ResistanceAttributeCode = 0,
                        ResistanceCap = 0f,
                    };
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }

        public static float Evaluate(in AutoChessMagnitudeEvaluationContext context)
        {
            var magnitude = context.Source switch
            {
                EMagnitudeSource.SetByCaller => context.SetByCallerMagnitude,
                EMagnitudeSource.SourceAttribute => context.SourceAttributeValue,
                EMagnitudeSource.TargetAttribute => context.TargetAttributeValue,
                EMagnitudeSource.StackCount => context.StackCount,
                _ => context.BaseMagnitude,
            };
            return ((magnitude + context.PreAdd) * context.Coefficient) + context.PostAdd;
        }

        public static bool TryEvaluate(int gameplayEffectCode, int modifierIndex, in AutoChessMagnitudeEvaluationContext context, out float value)
        {
            if (!TryGetDefinition(gameplayEffectCode, modifierIndex, out var definition))
            {
                value = 0f;
                return false;
            }

            var resolved = context;
            resolved.Source = definition.Source;
            resolved.BaseMagnitude = definition.BaseMagnitude;
            value = Evaluate(resolved);
            return true;
        }
    }
}
