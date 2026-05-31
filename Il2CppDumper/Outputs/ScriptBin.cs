using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Il2CppDumper
{
    internal sealed class CBinaryWriter(Stream stream, Encoding encoding) : BinaryWriter(stream, encoding)
    {

        public void WriteString0(string str)
        {
            if (str == null)
            {
                Write(0);
                return;
            }
            if (str.Any(x => x is > (char)byte.MaxValue or '\0'))
            {
                var lastIndex = 0;
                var builder = new StringBuilder();
                for (var index = 0; index < str.Length; index++)
                {
                    var ch = str[index];
                    if (ch == 0)
                    {
                        lastIndex = index + 1;
                    }
                    else if (ch > byte.MaxValue)
                    {
                        var len = index - lastIndex;
                        if (len > 0)
                        {
                            _ = builder.Append(str.AsSpan(lastIndex, len));
                        }
                        _ = builder.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:X}", (int)ch);
                        lastIndex = index + 1;
                    }
                }
                str = builder.ToString();
            }
            if (str.Length == 0)
            {
                Write(0);
                return;
            }
            var buffer = Encoding.ASCII.GetBytes(str);
            if (buffer.Any(v => v == 0))
            {
                Console.WriteLine($"bad string: {str}");
                Write(0);
                return;
            }
            Write(buffer.Length);
            Write(buffer);
        }

        public void Write<T>(IList<T> list, Action<CBinaryWriter, T> write)
        {
            Write(list.Count);
            foreach (var item in list)
            {
                write(this, item);
                Write(BaseStream.Position);
            }
        }
    }

    internal static class ScriptBin
    {
        public static void WriteFile(string filename, ScriptJson json)
        {
            using var file = File.Create(filename);
            using var binary = new CBinaryWriter(file, Encoding.ASCII);
            binary.Write(json.ScriptMethod, Write);
            binary.Write(json.ScriptString, Write);
            binary.Write(json.ScriptMetadata, Write);
            binary.Write(json.ScriptMetadataMethod, Write);
            binary.Write(json.Addresses, Write);
        }

        private static void Write(CBinaryWriter writer, ScriptMethod o)
        {
            writer.Write(o.Address);
            writer.WriteString0(o.Name);
            writer.WriteString0(o.Signature);
            writer.WriteString0(o.TypeSignature);
        }

        private static void Write(CBinaryWriter writer, ScriptString o)
        {
            writer.Write(o.Address);
            writer.WriteString0(o.Value);
        }

        private static void Write(CBinaryWriter writer, ScriptMetadata o)
        {
            writer.Write(o.Address);
            writer.WriteString0(o.Name);
            writer.WriteString0(o.Signature);
        }

        private static void Write(CBinaryWriter writer, ScriptMetadataMethod o)
        {
            writer.Write(o.Address);
            writer.WriteString0(o.Name);
            writer.Write(o.MethodAddress);
        }

        private static void Write(CBinaryWriter writer, ulong o) => writer.Write(o);
    }
}
