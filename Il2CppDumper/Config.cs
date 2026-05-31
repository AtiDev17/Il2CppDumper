using System.Collections.Generic;

namespace Il2CppDumper
{
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Microsoft.Performance", "CA1812")]
    internal sealed class ReplaceHashName
    {
        public string TargetName { get; set; }
        public string ReplaceToName { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Microsoft.Performance", "CA1812")]
    internal sealed class Config
    {
        public bool DumpMethod { get; set; } = true;
        public bool DumpField { get; set; } = true;
        public bool DumpProperty { get; set; } = true;
        public bool DumpAttribute { get; set; } = true;
        public bool DumpFieldOffset { get; set; } = true;
        public bool DumpMethodOffset { get; set; } = true;
        public bool DumpTypeDefIndex { get; set; } = true;
        public bool GenerateDummyDll { get; set; } = true;
        public bool GenerateStruct { get; set; } = true;
        public bool DummyDllAddToken { get; set; }
        public bool DummyDllAddOffset { get; set; }
        public bool ForceIl2CppVersion { get; set; }
        public double ForceVersion { get; set; } = 29;
        public bool ForceDump { get; set; }
        public bool NoRedirectedPointer { get; set; }
        public bool DisablePlusSearch { get; set; }
        public List<ReplaceHashName> ReplaceHashNames { get; set; }
        public Dictionary<string, string> ReplaceHashNameMap { get; set; }
    }
}
