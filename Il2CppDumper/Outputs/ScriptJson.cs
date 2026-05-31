using System.Collections.Generic;

namespace Il2CppDumper
{
    internal sealed class ScriptJson
    {
        public List<ScriptMethod> ScriptMethod = [];
        public List<ScriptString> ScriptString = [];
        public List<ScriptMetadata> ScriptMetadata = [];
        public List<ScriptMetadataMethod> ScriptMetadataMethod = [];
        public ulong[] Addresses;
    }

    internal sealed class ScriptMethod
    {
        public ulong Address;
        public string Name;
        public string Signature;
        public string TypeSignature;
    }

    internal sealed class ScriptString
    {
        public ulong Address;
        public string Value;
    }

    internal sealed class ScriptMetadata
    {
        public ulong Address;
        public string Name;
        public string Signature;
    }

    internal sealed class ScriptMetadataMethod
    {
        public ulong Address;
        public string Name;
        public ulong MethodAddress;
    }
}
