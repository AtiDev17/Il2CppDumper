using dnlib.DotNet;

namespace Il2CppDumper
{
    internal sealed class MyAssemblyResolver : AssemblyResolver
    {
        public void Register(AssemblyDef assembly) => AddToCache(assembly);
    }
}
