using System.Collections.Generic;

namespace GAS.Editor
{
    public interface IGasCodeGenPhase
    {
        string PhaseName { get; }

        IReadOnlyList<string> OutputFileNames { get; }

        void Execute(GasCodeGenContext context, GasCodeGenManifest manifest);
    }
}
