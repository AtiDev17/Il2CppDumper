using System;
using System.Collections.Generic;

namespace Il2CppDumper
{
    internal sealed class StructInfo
    {
        public string TypeName;
        public bool IsValueType;
        public string Parent;
        public List<StructFieldInfo> Fields = [];
        public List<StructFieldInfo> StaticFields = [];
        public StructVTableMethodInfo[] VTableMethod = [];
        public List<StructRGCTXInfo> RGCTXs = [];
    }

    internal sealed class StructFieldInfo
    {
        public string FieldTypeName;
        public string FieldName;
        public bool IsValueType;
        public bool IsCustomType;
    }

    internal sealed class StructVTableMethodInfo
    {
        public string MethodName;
    }

    internal sealed class StructRGCTXInfo
    {
        public Il2CppRGCTXDataType Type;
        public string TypeName;
        public string ClassName;
        public string MethodName;
    }
}
