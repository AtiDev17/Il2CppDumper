namespace Il2CppDumper
{
    internal enum SearchSectionType
    {
        Exec,
        Data,
        Bss
    }

    internal sealed class SearchSection
    {
        public ulong offset;
        public ulong offsetEnd;
        public ulong address;
        public ulong addressEnd;
    }
}
