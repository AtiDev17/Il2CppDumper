using System.Collections.Generic;

namespace Il2CppDumper
{
    public class ReplaceHashName
    {
        public string TargetName { get; set; }
        public string ReplaceToName { get; set; }
    }

    public class Config
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
        public bool DummyDllAddToken { get; set; } = false;
        public bool DummyDllAddOffset { get; set; } = false;
        public bool ForceIl2CppVersion { get; set; } = false;
        public double ForceVersion { get; set; } = 29;
        public bool ForceDump { get; set; } = false;
        public bool NoRedirectedPointer { get; set; } = false;
        public bool DisablePlusSearch { get; set; } = false;
        public List<ReplaceHashName> ReplaceHashNames { get; set; }
        public Dictionary<string, string> ReplaceHashNameMap { get; set; }
    }
}
