using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Il2CppDumper
{
    internal class BinaryStream : IDisposable
    {
        public double Version;
        public bool Is32Bit;
        public ulong ImageBase;
        private readonly Stream stream;
        private readonly MethodInfo readClass;
        private readonly MethodInfo readClassArray;
        private readonly Dictionary<Type, MethodInfo> genericMethodCache;
        private readonly Dictionary<FieldInfo, VersionAttribute[]> attributeCache;
        private readonly Dictionary<Type, FieldInfo[]> fieldCache;

        public BinaryStream(Stream input)
        {
            stream = input;
            Reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (stream.CanWrite)
            {
                Writer = new BinaryWriter(stream, Encoding.UTF8, true);
            }
            readClass = GetType().GetMethod("ReadClass", []);
            readClassArray = GetType().GetMethod("ReadClassArray", [typeof(long)]);
            genericMethodCache = [];
            attributeCache = [];
            fieldCache = [];
        }

        public bool ReadBoolean() => Reader.ReadBoolean();

        public byte ReadByte() => Reader.ReadByte();

        public byte[] ReadBytes(int count) => Reader.ReadBytes(count);

        public sbyte ReadSByte() => Reader.ReadSByte();

        public short ReadInt16() => Reader.ReadInt16();

        public ushort ReadUInt16() => Reader.ReadUInt16();

        public int ReadInt32() => Reader.ReadInt32();

        public uint ReadUInt32() => Reader.ReadUInt32();

        public long ReadInt64() => Reader.ReadInt64();

        public ulong ReadUInt64() => Reader.ReadUInt64();

        public float ReadSingle() => Reader.ReadSingle();

        public double ReadDouble() => Reader.ReadDouble();

        public uint ReadCompressedUInt32() => Reader.ReadCompressedUInt32();

        public int ReadCompressedInt32() => Reader.ReadCompressedInt32();

        public uint ReadULeb128() => Reader.ReadULeb128();

        public void Write(bool value) => Writer.Write(value);

        public void Write(byte value) => Writer.Write(value);

        public void Write(sbyte value) => Writer.Write(value);

        public void Write(short value) => Writer.Write(value);

        public void Write(ushort value) => Writer.Write(value);

        public void Write(int value) => Writer.Write(value);

        public void Write(uint value) => Writer.Write(value);

        public void Write(long value) => Writer.Write(value);

        public void Write(ulong value) => Writer.Write(value);

        public void Write(float value) => Writer.Write(value);

        public void Write(double value) => Writer.Write(value);

        public ulong Position
        {
            get => (ulong)stream.Position;
            set => stream.Position = (long)value;
        }

        public ulong Length => (ulong)stream.Length;

        private object ReadPrimitive(Type type)
        {
            return type.Name switch
            {
                "Int32" => ReadInt32(),
                "UInt32" => ReadUInt32(),
                "Int16" => ReadInt16(),
                "UInt16" => ReadUInt16(),
                "Byte" => ReadByte(),
                "Int64" => ReadIntPtr(),
                "UInt64" => ReadUIntPtr(),
                _ => throw new NotSupportedException()
            };
        }

        public T ReadClass<T>(ulong addr) where T : new()
        {
            Position = addr;
            return ReadClass<T>();
        }

        public T ReadClass<T>() where T : new()
        {
            var type = typeof(T);
            if (type.IsPrimitive)
            {
                return (T)ReadPrimitive(type);
            }
            else
            {
                var t = new T();
                if (!fieldCache.TryGetValue(type, out var fields))
                {
                    fields = type.GetFields();
                    fieldCache.Add(type, fields);
                }
                foreach (var i in fields)
                {
                    if (!attributeCache.TryGetValue(i, out var versionAttributes))
                    {
                        if (Attribute.IsDefined(i, typeof(VersionAttribute)))
                        {
                            versionAttributes = [.. i.GetCustomAttributes<VersionAttribute>()];
                            attributeCache.Add(i, versionAttributes);
                        }
                    }
                    if (versionAttributes?.Length > 0)
                    {
                        var read = false;
                        foreach (var versionAttribute in versionAttributes)
                        {
                            if (Version >= versionAttribute.Min && Version <= versionAttribute.Max)
                            {
                                read = true;
                                break;
                            }
                        }
                        if (!read)
                        {
                            continue;
                        }
                    }
                    var fieldType = i.FieldType;
                    if (fieldType.IsPrimitive)
                    {
                        i.SetValue(t, ReadPrimitive(fieldType));
                    }
                    else if (fieldType.IsEnum)
                    {
                        var e = fieldType.GetField("value__").FieldType;
                        i.SetValue(t, ReadPrimitive(e));
                    }
                    else if (fieldType.IsArray)
                    {
                        var arrayLengthAttribute = i.GetCustomAttribute<ArrayLengthAttribute>();
                        if (!genericMethodCache.TryGetValue(fieldType, out var methodInfo))
                        {
                            methodInfo = readClassArray.MakeGenericMethod(fieldType.GetElementType());
                            genericMethodCache.Add(fieldType, methodInfo);
                        }
                        i.SetValue(t, methodInfo.Invoke(this, [arrayLengthAttribute.Length]));
                    }
                    else if (fieldType == typeof(TypeIndex))
                    {
                        i.SetValue(t, ReadTypeIndex());
                    }
                    else if (fieldType == typeof(TypeDefinitionIndex))
                    {
                        i.SetValue(t, ReadTypeDefinitionIndex());
                    }
                    else if (fieldType == typeof(GenericContainerIndex))
                    {
                        i.SetValue(t, ReadGenericContainerIndex());
                    }
                    else if (fieldType == typeof(ParameterIndex))
                    {
                        i.SetValue(t, ReadParameterIndex());
                    }
                    else if (fieldType == typeof(Il2CppSectionMetadata))
                    {
                        i.SetValue(t, ReadClass<Il2CppSectionMetadata>());
                    }
                    else
                    {
                        if (!genericMethodCache.TryGetValue(fieldType, out var methodInfo))
                        {
                            methodInfo = readClass.MakeGenericMethod(fieldType);
                            genericMethodCache.Add(fieldType, methodInfo);
                        }
                        i.SetValue(t, methodInfo.Invoke(this, null));
                    }
                }
                return t;
            }
        }

        public TypeIndex ReadTypeIndex()
        {
            if (Version < 35)
            {
                int value = ReadInt32();
                return new TypeIndex(value);
            }
            else
            {
                switch (Metadata.typeIndexSize)
                {
                    case 1:
                        {
                            uint value = ReadByte();
                            return value == Byte.MaxValue ? new TypeIndex(-1) : new TypeIndex((int)value);
                        }
                    case 2:
                        {
                            uint value = ReadUInt16();
                            return value == UInt16.MaxValue ? new TypeIndex(-1) : new TypeIndex((int)value);
                        }
                    case 4:
                    default:
                        {
                            uint value = ReadUInt32();
                            return value == UInt32.MaxValue ? new TypeIndex(-1) : new TypeIndex((int)value);
                        }
                }
            }
        }

        public TypeDefinitionIndex ReadTypeDefinitionIndex()
        {
            if (Version < 35)
            {
                int value = ReadInt32();
                return new TypeDefinitionIndex(value);
            }
            else
            {
                switch (Metadata.typeDefinitionIndexSize)
                {
                    case 1:
                        {
                            uint value = ReadByte();
                            return value == Byte.MaxValue ? new TypeDefinitionIndex(-1) : new TypeDefinitionIndex((int)value);
                        }
                    case 2:
                        {
                            uint value = ReadUInt16();
                            return value == UInt16.MaxValue ? new TypeDefinitionIndex(-1) : new TypeDefinitionIndex((int)value);
                        }
                    case 4:
                    default:
                        {
                            uint value = ReadUInt32();
                            return value == UInt32.MaxValue ? new TypeDefinitionIndex(-1) : new TypeDefinitionIndex((int)value);
                        }
                }
            }
        }

        public GenericContainerIndex ReadGenericContainerIndex()
        {
            if (Version < 35)
            {
                int value = ReadInt32();
                return new GenericContainerIndex(value);
            }
            else
            {
                switch (Metadata.genericContainerIndexSize)
                {
                    case 1:
                        {
                            uint value = ReadByte();
                            return value == Byte.MaxValue ? new GenericContainerIndex(-1) : new GenericContainerIndex((int)value);
                        }
                    case 2:
                        {
                            uint value = ReadUInt16();
                            return value == UInt16.MaxValue ? new GenericContainerIndex(-1) : new GenericContainerIndex((int)value);
                        }
                    case 4:
                    default:
                        {
                            uint value = ReadUInt32();
                            return value == UInt32.MaxValue ? new GenericContainerIndex(-1) : new GenericContainerIndex((int)value);
                        }
                }
            }
        }

        public ParameterIndex ReadParameterIndex()
        {
            if (Version < 39)
            {
                int value = ReadInt32();
                return new ParameterIndex(value);
            }
            else
            {
                switch (Metadata.parameterIndexSize)
                {
                    case 1:
                        {
                            uint value = ReadByte();
                            return value == Byte.MaxValue ? new ParameterIndex(-1) : new ParameterIndex((int)value);
                        }
                    case 2:
                        {
                            uint value = ReadUInt16();
                            return value == UInt16.MaxValue ? new ParameterIndex(-1) : new ParameterIndex((int)value);
                        }
                    case 4:
                    default:
                        {
                            uint value = ReadUInt32();
                            return value == UInt32.MaxValue ? new ParameterIndex(-1) : new ParameterIndex((int)value);
                        }
                }
            }
        }

        public T[] ReadClassArray<T>(long count) where T : new()
        {
            var t = new T[count];
            for (var i = 0; i < count; i++)
            {
                t[i] = ReadClass<T>();
            }
            return t;
        }

        public T[] ReadClassArray<T>(ulong addr, ulong count) where T : new() => ReadClassArray<T>(addr, (long)count);

        public T[] ReadClassArray<T>(ulong addr, long count) where T : new()
        {
            Position = addr;
            return ReadClassArray<T>(count);
        }

        public string ReadStringToNull(ulong addr)
        {
            Position = addr;
            var bytes = new List<byte>();
            byte b;
            while ((b = ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString([.. bytes]);
        }

        public long ReadIntPtr() => Is32Bit ? ReadInt32() : ReadInt64();

        public virtual ulong ReadUIntPtr() => Is32Bit ? ReadUInt32() : ReadUInt64();

        public ulong PointerSize => Is32Bit ? 4ul : 8ul;

        public BinaryReader Reader { get; }

        public BinaryWriter Writer { get; }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Reader.Dispose();
                Writer?.Dispose();
                stream.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
