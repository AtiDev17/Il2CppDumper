using System.IO;

namespace Il2CppDumper
{
    internal abstract class ElfBase(Stream stream) : Il2Cpp(stream)
    {
        protected abstract void Load();
        protected abstract bool CheckSection();

        public override bool CheckDump() => !CheckSection();

        public void Reload() => Load();
    }
}
