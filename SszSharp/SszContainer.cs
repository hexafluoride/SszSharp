namespace SszSharp;

public static class SszContainer
{
    // static convenience method
    private static Dictionary<(Type, SizePreset), ISszType> CachedContainers = new();

    private static ISszType CreateContainer(Type type, SizePreset? preset = null)
    {
        preset ??= SizePreset.DefaultPreset;
        return (ISszType) (Activator.CreateInstance(typeof(SszContainer<>).MakeGenericType(type), preset) ??
                           throw new Exception($"Could not construct container for {type}"));
    }
    public static SszContainer<T> GetContainer<T>(SizePreset? preset = null)
    {
        var cacheKey = (typeof(T), preset ?? SizePreset.DefaultPreset);
        if (!CachedContainers.ContainsKey(cacheKey))
            CachedContainers[cacheKey] = CreateContainer(typeof(T), preset);
        return (SszContainer<T>) CachedContainers[cacheKey];
    }

    public static (T, int) Deserialize<T>(ReadOnlySpan<byte> span, SizePreset? preset = null) =>
        GetContainer<T>(preset).Deserialize(span);
    public static int Serialize<T>(T t, Span<byte> span, SizePreset? preset = null) =>
        GetContainer<T>(preset).Serialize(t, span);

    public static byte[] Serialize<T>(T t, SizePreset? preset = null)
    {
        var container = GetContainer<T>();
        var length = container.Length(t);
        var buffer = new byte[length];
        var written = container.Serialize(t, buffer.AsSpan());

        if (written != length)
            throw new Exception($"Was expecting to serialize to {length} bytes, did {written} instead");
        
        return buffer;
    }
    public static byte[] HashTreeRoot<T>(T t, SizePreset? preset = null) =>
        Merkleizer.HashTreeRoot(GetContainer<T>(preset), t);
}

public class SszContainer<TReturn> : ISszType<TReturn>
{
    public Type RepresentativeType => typeof(TReturn);
    public readonly ISszContainerSchema<TReturn> Schema;

    public SszContainer(SizePreset? preset)
        : this(SszSchemaGenerator.GetSchema<TReturn>(preset))
    {
        
    }
    
    public SszContainer(ISszContainerSchema<TReturn> schema)
    {
        Schema = schema;
    }

    public byte[] HashTreeRoot(TReturn t) => Merkleizer.HashTreeRoot(this, t);

    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public (TReturn, int) Deserialize(ReadOnlySpan<byte> span)
    {
        var ret = Schema.Default();
        int lastOffset = -1;
        int nextExpectedOffset = -1;
        int fixedPartIndex = 0;
        int totalConsumed = 0;
        var contentsStart = -1;

        var variableOffsets = new int[Schema.Fields.Length];
        int lastVariableOffsetIndex = -1;
        
        for (int i = 0; i < Schema.Fields.Length; i++)
        {
            var field = Schema.Fields[i];
            var fieldType = field.FieldType;
            if (fieldType.IsVariableLength())
            {
                variableOffsets[i] = (int)BitConverter.ToUInt32(span.Slice(fixedPartIndex));
                fixedPartIndex += 4;
                totalConsumed += 4;
                lastVariableOffsetIndex = i;
            }
            else
            {
                if (lastOffset == -1)
                    lastOffset = 0;

                (var deserialized, int consumedBytes) = fieldType.DeserializeUntyped(span.Slice(fixedPartIndex, fieldType.LengthUntyped(default!)));
                totalConsumed += consumedBytes;
                fixedPartIndex += consumedBytes;
                Schema.Set(ret, i, deserialized);
            }
        }

        for (int i = 0; i < Schema.Fields.Length; i++)
        {
            var field = Schema.Fields[i];
            var fieldType = field.FieldType;
            if (fieldType.IsVariableLength())
            {
                var currentOffset = variableOffsets[i];
                if (contentsStart == -1)
                {
                    contentsStart = (int)currentOffset;
                }
                
                var nextOffsetLimit = i == lastVariableOffsetIndex
                    ? span.Length
                    : variableOffsets.Skip(i + 1).First(k => k > 0);

                if (currentOffset > span.Length)
                {
                    throw new Exception("Offset too large");
                }

                if (lastOffset != -1 && (currentOffset < lastOffset))
                {
                    throw new Exception("Next offset is less than last offset");
                }

                if (nextExpectedOffset != -1 && nextExpectedOffset != currentOffset)
                {
                    throw new Exception("Gap in variable parts");
                }

                (var deserialized, int consumedBytes) = fieldType.DeserializeUntyped(span.Slice(currentOffset, nextOffsetLimit - currentOffset));
                totalConsumed += consumedBytes;
                lastOffset = (int)currentOffset;
                nextExpectedOffset = (int)currentOffset + consumedBytes;

                Schema.Set(ret, i, deserialized);
            }
        }

        return (ret, totalConsumed);
    }

    public int SerializeUntyped(object obj, Span<byte> span) => Serialize((TReturn)obj, span);
    public int Serialize(TReturn t, Span<byte> span)
    {
        var enumerated = Schema.Fields;
        
        var fixedParts = new int[enumerated.Length];
        int consumed = 0;

        // First pass, write fixed parts and placeholders for variable offsets
        for (int i = 0; i < enumerated.Length; i++)
        {
            var field = enumerated[i];
            var fieldType = field.FieldType;
            if (fieldType.IsVariableLength())
            {
                fixedParts[i] = consumed;
                consumed += 4;
            }
            else
            {
                fixedParts[i] = consumed;
                var serializedLength = fieldType.SerializeUntyped(Schema.Get(t, i), span.Slice(consumed));
                consumed += serializedLength;
            }
        }

        int fixedLength = consumed;
        //int variableLength = fixedLength;
        
        // Second pass, serialize and write variable parts
        for (int i = 0; i < enumerated.Length; i++)
        {
            var field = enumerated[i];
            var fieldType = field.FieldType;
            if (fieldType.IsVariableLength())
            {
                var variablePartSpan = span.Slice(consumed);
                var serializedLength = fieldType.SerializeUntyped(Schema.Get(t, i), variablePartSpan);
                BitConverter.GetBytes((uint)(consumed)).CopyTo(span.Slice(fixedParts[i], 4));
                consumed += serializedLength;
            }
        }

        return consumed;
    }
    
    public int Length(TReturn t) => Schema.Fields.Select((field, i) => field.FieldType.IsVariableLength() ? 4 + field.FieldType.LengthUntyped(Schema.Get(t, i)) : field.FieldType.LengthUntyped(default!)).Sum();
    public bool IsVariableLength() => Schema.Fields.Any(field => field.FieldType.IsVariableLength());
    public long ChunkCount(TReturn t) => Schema.Fields.Length;
    public int LengthUntyped(object t) => Length((TReturn)t);
    public long ChunkCountUntyped(object t) => ChunkCount((TReturn)t);
}