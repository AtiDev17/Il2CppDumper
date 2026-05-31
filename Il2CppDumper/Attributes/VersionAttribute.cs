using System;

namespace Il2CppDumper
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    internal sealed class VersionAttribute : Attribute
    {
        public double Min { get; set; }
        public double Max { get; set; } = double.MaxValue;
    }
}
