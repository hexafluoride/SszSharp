namespace SszSharp;

public class SszContainer<TReturn> : ISszType<TReturn>
{
    public readonly ISszContainerSchema<TReturn> Schema;

    public SszContainer(ISszContainerSchema<TReturn> schema)
    {
        Schema = schema;
    }

    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public (TReturn, int) Deserialize(ReadOnlySpan<byte> span)
    {
        var ret = Schema.Default();
        int lastOffset = -1;
        int nextExpectedOffset = -1;
        int fixedPartIndex = 0;
        int totalConsumed = 0;
        var contentsStart = -1;

        var variableOffsets = new int[Schema.FieldTypes.Length];
        int lastVariableOffsetIndex = -1;
        
        for (int i = 0; i < Schema.FieldTypes.Length; i++)
        {
            var type = Schema.FieldTypes[i];
            if (type.IsVariableLength())
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

                (var deserialized, int consumedBytes) = type.DeserializeUntyped(span.Slice(fixedPartIndex, type.LengthUntyped(default)));
                totalConsumed += consumedBytes;
                //lastOffset += consumedBytes;
                fixedPartIndex += consumedBytes;
                Schema.Set(ret, i, deserialized);
            }
        }

        for (int i = 0; i < Schema.FieldTypes.Length; i++)
        {
            var type = Schema.FieldTypes[i];
            if (type.IsVariableLength())
            {
                var currentOffset = variableOffsets[i];
                if (contentsStart == -1)
                {
                    contentsStart = (int)currentOffset;
                }
                
                var nextOffsetLimit = i == lastVariableOffsetIndex
                    ? span.Length
                    : variableOffsets.Skip(i + 1).First(k => k > 0);

                if (currentOffset > int.MaxValue || currentOffset > span.Length)
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

                (var deserialized, int consumedBytes) = type.DeserializeUntyped(span.Slice((int)currentOffset, nextOffsetLimit - (int)currentOffset));
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
        var enumerated = Schema.FieldTypes;
        
        var fixedParts = new int[enumerated.Length];
        int consumed = 0;

        // First pass, write fixed parts and placeholders for variable offsets
        for (int i = 0; i < enumerated.Length; i++)
        {
            var type = enumerated[i];
            if (type.IsVariableLength())
            {
                fixedParts[i] = consumed;
                consumed += 4;
            }
            else
            {
                fixedParts[i] = consumed;
                var serializedLength = type.SerializeUntyped(Schema.Get(t, i), span.Slice(consumed));
                consumed += serializedLength;
            }
        }

        int fixedLength = consumed;
        //int variableLength = fixedLength;
        
        // Second pass, serialize and write variable parts
        for (int i = 0; i < enumerated.Length; i++)
        {
            var type = enumerated[i];
            if (type.IsVariableLength())
            {
                var variablePartSpan = span.Slice(consumed);
                var serializedLength = type.SerializeUntyped(Schema.Get(t, i), variablePartSpan);
                BitConverter.GetBytes((uint)(consumed)).CopyTo(span.Slice(fixedParts[i], 4));
                consumed += serializedLength;
            }
        }

        return consumed;
    }
    
    public int Length(TReturn t) => Schema.FieldTypes.Select((fieldType, i) => fieldType.IsVariableLength() ? fieldType.LengthUntyped(Schema.Get(t, i)) : fieldType.LengthUntyped(default)).Sum();
    public bool IsVariableLength() => Schema.FieldTypes.Any(fieldType => fieldType.IsVariableLength());
    public long ChunkCount(TReturn t) => Schema.FieldTypes.Length;
    public int LengthUntyped(object t) => Length((TReturn)t);
    public long ChunkCountUntyped(object t) => ChunkCount((TReturn)t);
}