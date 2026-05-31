using dnlib.DotNet;

namespace Il2CppDumper
{
    public class MyAssemblyResolver : AssemblyResolver
    {
        public void Register(AssemblyDef assembly)
        {
            AddToCache(assembly);
        }
    }
}
