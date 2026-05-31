using System.Text;

namespace Il2CppDumper
{
    internal static class StringExtensions
    {
        public static string ToEscapedString(this string s)
        {
            var re = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                _ = c switch
                {
                    '\'' => re.Append(@"\'"),
                    '"' => re.Append(@"\"""),
                    '\\' => re.Append(@"\\"),
                    '\0' => re.Append(@"\0"),
                    '\a' => re.Append(@"\a"),
                    '\b' => re.Append(@"\b"),
                    '\f' => re.Append(@"\f"),
                    '\n' => re.Append(@"\n"),
                    '\r' => re.Append(@"\r"),
                    '\t' => re.Append(@"\t"),
                    '\v' => re.Append(@"\v"),
                    '\u0085' => re.Append(@"\u0085"),
                    '\u2028' => re.Append(@"\u2028"),
                    '\u2029' => re.Append(@"\u2029"),
                    _ => re.Append(c),
                };
            }
            return re.ToString();
        }
    }
}
