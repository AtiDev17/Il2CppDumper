using System;
using System.Text;

namespace Il2CppDumper
{
    internal static class HexExtensions
    {
        public static string HexToBin(this byte b) => Convert.ToString(b, 2).PadLeft(8, '0');

        public static string HexToBin(this byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 8);
            foreach (var b in bytes)
            {
                _ = result.Insert(0, b.HexToBin());
            }
            return result.ToString();
        }
    }
}
