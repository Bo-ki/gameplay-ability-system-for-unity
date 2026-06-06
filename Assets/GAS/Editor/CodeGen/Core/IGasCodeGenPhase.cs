using System.Collections.Generic;

namespace GAS.Editor
{
    public interface IGasCodeGenPhase
    {
        string PhaseName { get; }

        IReadOnlyList<string> OutputFileNames { get; }

        bool RequiresRows { get; }

        void Execute(GasCodeGenContext context, GasCodeGenManifest manifest);
    }
}
