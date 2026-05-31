using System;
using System.Collections.Generic;
using System.Linq;

namespace Il2CppDumper
{
    internal sealed class SectionHelper(Il2Cpp il2Cpp, int methodCount, int typeDefinitionsCount, long metadataUsagesCount, int imageCount)
    {
        private bool pointerInExec;

        public List<SearchSection> Exec { get; set; }
        public List<SearchSection> Data { get; set; }
        public List<SearchSection> Bss { get; set; }

        public void SetSection(SearchSectionType type, Elf32_Phdr[] sections)
        {
            var secs = new List<SearchSection>();
            foreach (var section in sections)
            {
                if (section != null)
                {
                    secs.Add(new SearchSection
                    {
                        offset = section.p_offset,
                        offsetEnd = section.p_offset + section.p_filesz,
                        address = section.p_vaddr,
                        addressEnd = section.p_vaddr + section.p_memsz
                    });
                }
            }
            SetSection(type, secs);
        }

        public void SetSection(SearchSectionType type, Elf64_Phdr[] sections)
        {
            var secs = new List<SearchSection>();
            foreach (var section in sections)
            {
                if (section != null)
                {
                    secs.Add(new SearchSection
                    {
                        offset = section.p_offset,
                        offsetEnd = section.p_offset + section.p_filesz,
                        address = section.p_vaddr,
                        addressEnd = section.p_vaddr + section.p_memsz
                    });
                }
            }
            SetSection(type, secs);
        }

        public void SetSection(SearchSectionType type, MachoSection[] sections)
        {
            var secs = new List<SearchSection>();
            foreach (var section in sections)
            {
                if (section != null)
                {
                    secs.Add(new SearchSection
                    {
                        offset = section.offset,
                        offsetEnd = section.offset + section.size,
                        address = section.addr,
                        addressEnd = section.addr + section.size
                    });
                }
            }
            SetSection(type, secs);
        }

        public void SetSection(SearchSectionType type, MachoSection64Bit[] sections)
        {
            var secs = new List<SearchSection>();
            foreach (var section in sections)
            {
                if (section != null)
                {
                    secs.Add(new SearchSection
                    {
                        offset = section.offset,
                        offsetEnd = section.offset + section.size,
                        address = section.addr,
                        addressEnd = section.addr + section.size
                    });
                }
            }
            SetSection(type, secs);
        }

        public void SetSection(SearchSectionType type, ulong imageBase, SectionHeader[] sections)
        {
            var secs = new List<SearchSection>();
            foreach (var section in sections)
            {
                if (section != null)
                {
                    secs.Add(new SearchSection
                    {
                        offset = section.PointerToRawData,
                        offsetEnd = section.PointerToRawData + section.SizeOfRawData,
                        address = section.VirtualAddress + imageBase,
                        addressEnd = section.VirtualAddress + section.VirtualSize + imageBase
                    });
                }
            }
            SetSection(type, secs);
        }

        public void SetSection(SearchSectionType type, params NSOSegmentHeader[] sections)
        {
            var secs = new List<SearchSection>();
            foreach (var section in sections)
            {
                if (section != null)
                {
                    secs.Add(new SearchSection
                    {
                        offset = section.FileOffset,
                        offsetEnd = section.FileOffset + section.DecompressedSize,
                        address = section.MemoryOffset,
                        addressEnd = section.MemoryOffset + section.DecompressedSize
                    });
                }
            }
            SetSection(type, secs);
        }

        public void SetSection(SearchSectionType type, params SearchSection[] secs) => SetSection(type, secs.ToList());

        private void SetSection(SearchSectionType type, List<SearchSection> secs)
        {
            switch (type)
            {
                case SearchSectionType.Exec:
                    Exec = secs;
                    break;
                case SearchSectionType.Data:
                    Data = secs;
                    break;
                case SearchSectionType.Bss:
                    Bss = secs;
                    break;
                default:
                    break;
            }
        }

        public ulong FindCodeRegistration()
        {
            if (il2Cpp.Version >= 24.2)
            {
                ulong codeRegistration;
                if (il2Cpp is ElfBase)
                {
                    codeRegistration = FindCodeRegistrationExec();
                    if (codeRegistration == 0)
                    {
                        codeRegistration = FindCodeRegistrationData();
                    }
                    else
                    {
                        pointerInExec = true;
                    }
                }
                else
                {
                    codeRegistration = FindCodeRegistrationData();
                    if (codeRegistration == 0)
                    {
                        codeRegistration = FindCodeRegistrationExec();
                        pointerInExec = true;
                    }
                }
                return codeRegistration;
            }
            return FindCodeRegistrationOld();
        }

        public ulong FindMetadataRegistration()
        {
            if (il2Cpp.Version < 19)
            {
                return 0;
            }
            return il2Cpp.Version >= 27 ? FindMetadataRegistrationV21() : FindMetadataRegistrationOld();
        }

        private ulong FindCodeRegistrationOld()
        {
            foreach (var section in Data)
            {
                il2Cpp.Position = section.offset;
                while (il2Cpp.Position < section.offsetEnd)
                {
                    var addr = il2Cpp.Position;
                    if (il2Cpp.ReadIntPtr() == methodCount)
                    {
                        try
                        {
                            var pointer = il2Cpp.MapVATR(il2Cpp.ReadUIntPtr());
                            if (CheckPointerRangeDataRa(pointer))
                            {
                                var pointers = il2Cpp.ReadClassArray<ulong>(pointer, methodCount);
                                if (CheckPointerRangeExecVa(pointers))
                                {
                                    return addr - section.offset + section.address;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"FindCodeRegistrationOld error: {ex.Message}");
                        }
                    }
                    il2Cpp.Position = addr + il2Cpp.PointerSize;
                }
            }

            return 0ul;
        }

        private ulong FindMetadataRegistrationOld()
        {
            foreach (var section in Data)
            {
                il2Cpp.Position = section.offset;
                var end = Math.Min(section.offsetEnd, il2Cpp.Length) - il2Cpp.PointerSize;
                while (il2Cpp.Position < end)
                {
                    var addr = il2Cpp.Position;
                    if (il2Cpp.ReadIntPtr() == typeDefinitionsCount)
                    {
                        try
                        {
                            il2Cpp.Position += il2Cpp.PointerSize * 2;
                            var pointer = il2Cpp.MapVATR(il2Cpp.ReadUIntPtr());
                            if (CheckPointerRangeDataRa(pointer))
                            {
                                var pointers = il2Cpp.ReadClassArray<ulong>(pointer, metadataUsagesCount);
                                if (CheckPointerRangeBssVa(pointers))
                                {
                                    return addr - (il2Cpp.PointerSize * 12) - section.offset + section.address;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"FindMetadataRegistrationOld error: {ex.Message}");
                        }
                    }
                    il2Cpp.Position = addr + il2Cpp.PointerSize;
                }
            }

            return 0ul;
        }

        private ulong FindMetadataRegistrationV21()
        {
            foreach (var section in Data)
            {
                il2Cpp.Position = section.offset;
                var end = Math.Min(section.offsetEnd, il2Cpp.Length) - il2Cpp.PointerSize;
                while (il2Cpp.Position < end)
                {
                    var addr = il2Cpp.Position;
                    if (il2Cpp.ReadIntPtr() == typeDefinitionsCount)
                    {
                        il2Cpp.Position += il2Cpp.PointerSize;
                        if (il2Cpp.ReadIntPtr() == typeDefinitionsCount)
                        {
                            try
                            {
                                var pointer = il2Cpp.MapVATR(il2Cpp.ReadUIntPtr());
                                if (CheckPointerRangeDataRa(pointer))
                                {
                                    var pointers = il2Cpp.ReadClassArray<ulong>(pointer, typeDefinitionsCount);
                                    var flag = pointerInExec ? CheckPointerRangeExecVa(pointers) : CheckPointerRangeDataVa(pointers);
                                    if (flag)
                                    {
                                        return addr - (il2Cpp.PointerSize * 10) - section.offset + section.address;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"FindMetadataRegistrationV21 error: {ex.Message}");
                            }
                        }
                    }
                    il2Cpp.Position = addr + il2Cpp.PointerSize;
                }
            }

            return 0ul;
        }

        private bool CheckPointerRangeDataRa(ulong pointer) => Data.Any(x => pointer >= x.offset && pointer <= x.offsetEnd);

        private bool CheckPointerRangeExecVa(ulong[] pointers) => pointers.All(x => Exec.Any(y => x >= y.address && x <= y.addressEnd));

        private bool CheckPointerRangeDataVa(ulong[] pointers) => pointers.All(x => Data.Any(y => x >= y.address && x <= y.addressEnd));

        private bool CheckPointerRangeBssVa(ulong[] pointers) => pointers.All(x => Bss.Any(y => x >= y.address && x <= y.addressEnd));

        private static readonly byte[] featureBytes = [0x6D, 0x73, 0x63, 0x6F, 0x72, 0x6C, 0x69, 0x62, 0x2E, 0x64, 0x6C, 0x6C, 0x00]; //mscorlib.dll

        private ulong FindCodeRegistrationData() => FindCodeRegistration2019(Data);

        private ulong FindCodeRegistrationExec() => FindCodeRegistration2019(Exec);

        private ulong FindCodeRegistration2019(List<SearchSection> secs)
        {
            foreach (var sec in secs)
            {
                il2Cpp.Position = sec.offset;
                var buff = il2Cpp.ReadBytes((int)(sec.offsetEnd - sec.offset));
                foreach (var index in buff.Search(featureBytes))
                {
                    var dllva = (ulong)index + sec.address;
                    foreach (var refva in FindReference(dllva))
                    {
                        foreach (var refva2 in FindReference(refva))
                        {
                            if (il2Cpp.Version >= 27)
                            {
                                for (int i = imageCount - 1; i >= 0; i--)
                                {
                                    foreach (var refva3 in FindReference(refva2 - ((ulong)i * il2Cpp.PointerSize)))
                                    {
                                        il2Cpp.Position = il2Cpp.MapVATR(refva3 - il2Cpp.PointerSize);
                                        if (il2Cpp.ReadIntPtr() == imageCount)
                                        {
                                            if (il2Cpp.Version >= 35)
                                            {
                                                return refva3 - (il2Cpp.PointerSize * 16);
                                            }
                                            return il2Cpp.Version >= 29 ? refva3 - (il2Cpp.PointerSize * 14) : refva3 - (il2Cpp.PointerSize * 13);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i < imageCount; i++)
                                {
                                    foreach (var refva3 in FindReference(refva2 - ((ulong)i * il2Cpp.PointerSize)))
                                    {
                                        return refva3 - (il2Cpp.PointerSize * 13);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return 0ul;
        }

        private IEnumerable<ulong> FindReference(ulong addr)
        {
            foreach (var dataSec in Data)
            {
                var position = dataSec.offset;
                var end = Math.Min(dataSec.offsetEnd, il2Cpp.Length) - il2Cpp.PointerSize;
                while (position < end)
                {
                    il2Cpp.Position = position;
                    if (il2Cpp.ReadUIntPtr() == addr)
                    {
                        yield return position - dataSec.offset + dataSec.address;
                    }
                    position += il2Cpp.PointerSize;
                }
            }
        }
    }
}
