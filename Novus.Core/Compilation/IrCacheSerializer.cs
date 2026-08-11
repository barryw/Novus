using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Novus.IR;

namespace Novus.Compilation;

internal sealed class PersistedIrModule
{
    public IrModule Module { get; set; } = null!;
    public List<IrStringLiteral> StringLiterals { get; set; } = new();
    public List<string> ImportedModules { get; set; } = new();
}

/// <summary>
/// Versioned, reference-preserving serializer for compiler-owned IR graphs.
/// Cache files are untrusted: only Novus.Core and a small set of framework types can be created.
/// </summary>
internal static class IrCacheSerializer
{
    private const uint Magic = 0x5249564E; // NVIR
    private const int FormatVersion = 3;
    private static readonly Assembly CompilerAssembly = typeof(IrModule).Assembly;
    private static readonly Assembly CoreAssembly = typeof(object).Assembly;
    private static readonly Dictionary<object, string> SingletonNames = FindSingletons();
    private static readonly object[] SingletonValues = SingletonNames.OrderBy(x => x.Value, StringComparer.Ordinal).Select(x => x.Key).ToArray();
    private static readonly Dictionary<object, int> SingletonIds = SingletonValues.Select((value, index) => (value, index))
        .ToDictionary(x => x.value, x => x.index, ReferenceEqualityComparer.Instance);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]> Fields = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, string> TypeSchemas = new();
    private static readonly string SingletonSchemaHash = ComputeSingletonSchemaHash();

    public static byte[] Serialize(PersistedIrModule module)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(SingletonSchemaHash);
        new GraphWriter(writer).Write(module);
        writer.Flush();
        return stream.ToArray();
    }

    public static PersistedIrModule Deserialize(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != FormatVersion || reader.ReadString() != SingletonSchemaHash)
        {
            throw new InvalidDataException("Incompatible Novus IR cache entry");
        }

        return (PersistedIrModule)(new GraphReader(reader).Read() ??
            throw new InvalidDataException("Empty Novus IR cache entry"));
    }

    private enum EntryKind : byte
    {
        Null,
        Reference,
        Singleton,
        Value,
        Object
    }

    private enum PayloadKind : byte
    {
        Primitive,
        Enum,
        Array,
        List,
        Dictionary,
        Set,
        Fields
    }

    private sealed class GraphWriter(BinaryWriter writer)
    {
        private readonly Dictionary<object, int> _references = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Type, int> _types = new();

        public void Write(object? value)
        {
            if (value == null)
            {
                writer.Write((byte)EntryKind.Null);
                return;
            }

            if (SingletonIds.TryGetValue(value, out var singletonId))
            {
                writer.Write((byte)EntryKind.Singleton);
                writer.Write(singletonId);
                return;
            }

            var type = value.GetType();
            EnsureAllowed(type);
            if (!type.IsValueType && type != typeof(string))
            {
                if (_references.TryGetValue(value, out var existingId))
                {
                    writer.Write((byte)EntryKind.Reference);
                    writer.Write(existingId);
                    return;
                }

                writer.Write((byte)EntryKind.Object);
                writer.Write(_references.Count);
                _references[value] = _references.Count;
            }
            else
            {
                writer.Write((byte)EntryKind.Value);
            }

            WriteType(type);
            WritePayload(type, value);
        }

        private void WriteType(Type type)
        {
            if (_types.TryGetValue(type, out var id))
            {
                writer.Write(id);
                return;
            }

            writer.Write(~_types.Count);
            _types[type] = _types.Count;
            writer.Write(type.AssemblyQualifiedName!);
            writer.Write(GetTypeSchema(type));
        }

        private void WritePayload(Type type, object value)
        {
            if (IsPrimitive(type))
            {
                writer.Write((byte)PayloadKind.Primitive);
                WritePrimitive(type, value);
            }
            else if (type.IsEnum)
            {
                writer.Write((byte)PayloadKind.Enum);
                Write(Convert.ChangeType(value, Enum.GetUnderlyingType(type)));
            }
            else if (type.IsArray)
            {
                var array = (Array)value;
                if (array.Rank != 1)
                {
                    throw new NotSupportedException("Only one-dimensional arrays can be cached");
                }

                writer.Write((byte)PayloadKind.Array);
                writer.Write(array.Length);
                foreach (var item in array)
                {
                    Write(item);
                }
            }
            else if (IsGeneric(type, typeof(List<>)))
            {
                writer.Write((byte)PayloadKind.List);
                WriteItems((IEnumerable)value, ((ICollection)value).Count);
            }
            else if (IsGeneric(type, typeof(Dictionary<,>)))
            {
                writer.Write((byte)PayloadKind.Dictionary);
                var dictionary = (IDictionary)value;
                writer.Write(dictionary.Count);
                foreach (DictionaryEntry item in dictionary)
                {
                    Write(item.Key);
                    Write(item.Value);
                }
            }
            else if (IsGeneric(type, typeof(HashSet<>)))
            {
                writer.Write((byte)PayloadKind.Set);
                var count = (int)type.GetProperty("Count")!.GetValue(value)!;
                WriteItems((IEnumerable)value, count);
            }
            else
            {
                writer.Write((byte)PayloadKind.Fields);
                var fields = GetFields(type);
                foreach (var field in fields)
                {
                    Write(field.GetValue(value));
                }
            }
        }

        private void WriteItems(IEnumerable items, int count)
        {
            writer.Write(count);
            foreach (var item in items)
            {
                Write(item);
            }
        }

        private void WritePrimitive(Type type, object value)
        {
            if (type == typeof(string)) writer.Write((string)value);
            else if (type == typeof(bool)) writer.Write((bool)value);
            else if (type == typeof(byte)) writer.Write((byte)value);
            else if (type == typeof(sbyte)) writer.Write((sbyte)value);
            else if (type == typeof(short)) writer.Write((short)value);
            else if (type == typeof(ushort)) writer.Write((ushort)value);
            else if (type == typeof(int)) writer.Write((int)value);
            else if (type == typeof(uint)) writer.Write((uint)value);
            else if (type == typeof(long)) writer.Write((long)value);
            else if (type == typeof(ulong)) writer.Write((ulong)value);
            else if (type == typeof(char)) writer.Write((char)value);
            else if (type == typeof(float)) writer.Write((float)value);
            else if (type == typeof(double)) writer.Write((double)value);
            else if (type == typeof(decimal)) writer.Write((decimal)value);
            else if (type == typeof(Guid)) writer.Write(((Guid)value).ToByteArray());
            else if (type == typeof(DateTime)) writer.Write(((DateTime)value).ToBinary());
            else if (type == typeof(TimeSpan)) writer.Write(((TimeSpan)value).Ticks);
            else throw new NotSupportedException($"Unsupported primitive type {type}");
        }
    }

    private sealed class GraphReader(BinaryReader reader)
    {
        private readonly List<object> _references = new();
        private readonly List<Type> _types = new();

        public object? Read()
        {
            var entryKind = (EntryKind)reader.ReadByte();
            if (entryKind == EntryKind.Null)
            {
                return null;
            }

            if (entryKind == EntryKind.Reference)
            {
                return _references[reader.ReadInt32()];
            }

            if (entryKind == EntryKind.Singleton)
            {
                return SingletonValues[reader.ReadInt32()];
            }

            var referenceId = entryKind == EntryKind.Object ? reader.ReadInt32() : -1;
            var type = ReadType();
            var payloadKind = (PayloadKind)reader.ReadByte();
            if (entryKind == EntryKind.Value)
            {
                return ReadValue(type, payloadKind);
            }

            return ReadObject(type, payloadKind, referenceId);
        }

        private object ReadValue(Type type, PayloadKind kind)
        {
            if (kind == PayloadKind.Primitive)
            {
                return ReadPrimitive(type);
            }

            if (kind == PayloadKind.Enum)
            {
                return Enum.ToObject(type, Read()!);
            }

            if (kind != PayloadKind.Fields)
            {
                throw new InvalidDataException($"Invalid value payload {kind}");
            }

            var boxed = RuntimeHelpers.GetUninitializedObject(type);
            ReadFields(type, boxed);
            return boxed;
        }

        private object ReadObject(Type type, PayloadKind kind, int referenceId)
        {
            object value;
            switch (kind)
            {
                case PayloadKind.Array:
                    value = Array.CreateInstance(type.GetElementType()!, reader.ReadInt32());
                    Register(referenceId, value);
                    var array = (Array)value;
                    for (var i = 0; i < array.Length; i++) array.SetValue(Read(), i);
                    return value;
                case PayloadKind.List:
                    value = Activator.CreateInstance(type)!;
                    Register(referenceId, value);
                    ReadItems(reader.ReadInt32(), item => ((IList)value).Add(item));
                    return value;
                case PayloadKind.Dictionary:
                    value = Activator.CreateInstance(type)!;
                    Register(referenceId, value);
                    var dictionary = (IDictionary)value;
                    var count = reader.ReadInt32();
                    for (var i = 0; i < count; i++) dictionary.Add(Read()!, Read());
                    return value;
                case PayloadKind.Set:
                    value = Activator.CreateInstance(type)!;
                    Register(referenceId, value);
                    var add = type.GetMethod("Add")!;
                    ReadItems(reader.ReadInt32(), item => add.Invoke(value, [item]));
                    return value;
                case PayloadKind.Fields:
                    value = RuntimeHelpers.GetUninitializedObject(type);
                    Register(referenceId, value);
                    ReadFields(type, value);
                    return value;
                default:
                    throw new InvalidDataException($"Invalid object payload {kind}");
            }
        }

        private void ReadFields(Type type, object value)
        {
            foreach (var field in GetFields(type))
            {
                field.SetValue(value, Read());
            }
        }

        private Type ReadType()
        {
            var id = reader.ReadInt32();
            if (id >= 0)
            {
                return _types[id];
            }

            id = ~id;
            if (id != _types.Count)
            {
                throw new InvalidDataException("Invalid Novus IR type reference");
            }

            var type = ResolveType(reader.ReadString());
            if (reader.ReadString() != GetTypeSchema(type))
            {
                throw new InvalidDataException($"Incompatible cached type {type.FullName}");
            }
            _types.Add(type);
            return type;
        }

        private void Register(int id, object value)
        {
            if (id != _references.Count)
            {
                throw new InvalidDataException("Invalid Novus IR object reference");
            }

            _references.Add(value);
        }

        private void ReadItems(int count, Action<object?> add)
        {
            for (var i = 0; i < count; i++) add(Read());
        }

        private object ReadPrimitive(Type type)
        {
            if (type == typeof(string)) return reader.ReadString();
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(char)) return reader.ReadChar();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(decimal)) return reader.ReadDecimal();
            if (type == typeof(Guid)) return new Guid(reader.ReadBytes(16));
            if (type == typeof(DateTime)) return DateTime.FromBinary(reader.ReadInt64());
            if (type == typeof(TimeSpan)) return TimeSpan.FromTicks(reader.ReadInt64());
            throw new InvalidDataException($"Unsupported primitive type {type}");
        }
    }

    private static Type ResolveType(string name)
    {
        var type = Type.GetType(name, assemblyName =>
        {
            if (assemblyName.Name == CompilerAssembly.GetName().Name) return CompilerAssembly;
            if (assemblyName.Name == CoreAssembly.GetName().Name) return CoreAssembly;
            return null;
        }, null, throwOnError: false) ?? throw new InvalidDataException($"Unknown cached type {name}");
        EnsureAllowed(type);
        return type;
    }

    private static void EnsureAllowed(Type type)
    {
        if (type.Assembly == CompilerAssembly || type == typeof(object) || IsPrimitive(type) ||
            type.Assembly == CoreAssembly && type.IsEnum ||
            type.IsArray && IsAllowed(type.GetElementType()!) ||
            type.IsGenericType && type.GetGenericArguments().All(IsAllowed) &&
            type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(List<>) || definition == typeof(Dictionary<,>) || definition == typeof(HashSet<>) ||
             definition == typeof(KeyValuePair<,>) || definition == typeof(Nullable<>) ||
             definition.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidDataException($"Type is not permitted in Novus IR cache: {type}");
    }

    private static bool IsAllowed(Type type)
    {
        try
        {
            EnsureAllowed(type);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsPrimitive(Type type) =>
        type == typeof(string) || type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
        type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong) || type == typeof(char) || type == typeof(float) ||
        type == typeof(double) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) ||
        type == typeof(TimeSpan);

    private static bool IsGeneric(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition;

    private static FieldInfo[] GetFields(Type type) => Fields.GetOrAdd(type, static current =>
        Enumerable.Range(0, GetInheritanceDepth(current))
            .SelectMany(depth => GetBaseType(current, depth).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .OrderBy(FieldKey, StringComparer.Ordinal)
            .ToArray());

    private static int GetInheritanceDepth(Type type)
    {
        var depth = 0;
        for (var current = type; current != null; current = current.BaseType) depth++;
        return depth;
    }

    private static Type GetBaseType(Type type, int depth)
    {
        for (var i = 0; i < depth; i++) type = type.BaseType!;
        return type;
    }

    private static string FieldKey(FieldInfo field) => $"{field.DeclaringType!.FullName}:{field.Name}";

    private static Dictionary<object, string> FindSingletons()
    {
        var result = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        foreach (var type in CompilerAssembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (typeof(IrType).IsAssignableFrom(field.FieldType) && field.GetValue(null) is { } value)
                {
                    result.TryAdd(value, $"{type.FullName}:{field.Name}");
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.GetIndexParameters().Length == 0 && typeof(IrType).IsAssignableFrom(property.PropertyType) &&
                    property.GetValue(null) is { } value)
                {
                    result.TryAdd(value, $"{type.FullName}:{property.Name}");
                }
            }
        }

        return result;
    }

    private static string GetTypeSchema(Type type) => TypeSchemas.GetOrAdd(type, static current =>
    {
        var schema = $"{current.FullName}|{string.Join(',', GetFields(current).Select(field => $"{FieldKey(field)}={field.FieldType.FullName}"))}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema)))[..16];
    });

    private static string ComputeSingletonSchemaHash()
    {
        var schema = SingletonNames.Values.Order(StringComparer.Ordinal).Select(name => $"singleton:{name}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', schema))))[..16];
    }
}
