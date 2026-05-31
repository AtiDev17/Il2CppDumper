using System;

namespace Il2CppDumper
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ArrayLengthAttribute : Attribute
    {
        public int Length { get; set; }
    }
}
