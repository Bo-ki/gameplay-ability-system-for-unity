namespace GAS.Runtime
{
    public static class GASDefinitionCatalogLookup
    {
        public static bool TryGetAbilityIndex(
            ref GASDefinitionCatalogBlob catalog,
            int abilityCode,
            out int index)
        {
            index = -1;
            var lo = 0;
            var hi = catalog.AbilityCodes.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var midCode = catalog.AbilityCodes[mid];
                if (midCode == abilityCode)
                {
                    index = mid;
                    return true;
                }

                if (midCode < abilityCode)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return false;
        }

        public static ref readonly GASCatalogAbilityDefinitionBlob GetAbility(
            ref GASDefinitionCatalogBlob catalog,
            int index)
        {
            return ref catalog.Abilities[index];
        }
    }
}
