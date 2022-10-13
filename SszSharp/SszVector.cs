namespace SszSharp;

public class SszVector<TReturn, TDescriptor> : ISszType<IEnumerable<TReturn>>
    where TDescriptor : ISszType<TReturn>
{
    public readonly long Count;
    public readonly TDescriptor MemberType;
    
    public SszVector(TDescriptor memberType, long count)
    {
        MemberType = memberType;
        Count = count;
    }

    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public (IEnumerable<TReturn>, int) Deserialize(ReadOnlySpan<byte> span)
    {
        var ret = new TReturn[Count];
        int lastOffset = -1;
        int nextExpectedOffset = -1;
        int fixedPartIndex = 0;
        int totalConsumed = 0;
        
        for (int i = 0; i < Count; i++)
        {
            if (MemberType.IsVariableLength())
            {
                var nextOffset = BitConverter.ToUInt32(span.Slice(fixedPartIndex));
                var nextOffsetLimit = i == (Count - 1)
                    ? span.Length
                    : (int)BitConverter.ToUInt32(span.Slice(fixedPartIndex + 4));
                totalConsumed += 4;
                fixedPartIndex += 4;

                if (nextOffset > int.MaxValue || nextOffset > span.Length)
                {
                    throw new Exception("Offset too large");
                }

                if (lastOffset != -1 && (nextOffset <= lastOffset))
                {
                    throw new Exception("Next offset is equal to last offset");
                }

                if (nextExpectedOffset != -1 && nextExpectedOffset != nextOffset)
                {
                    throw new Exception("Gap in variable parts");
                }

                (TReturn deserialized, int consumedBytes) = MemberType.Deserialize(span.Slice((int)nextOffset, nextOffsetLimit - (int)nextOffset));
                totalConsumed += consumedBytes;
                lastOffset = (int)nextOffset;
                nextExpectedOffset = (int)nextOffset + consumedBytes;

                ret[i] = deserialized;
            }
            else
            {
                if (lastOffset == -1)
                    lastOffset = 0;

                (TReturn deserialized, int consumedBytes) = MemberType.Deserialize(span.Slice(lastOffset, MemberType.Length(default)));
                totalConsumed += consumedBytes;
                lastOffset += consumedBytes;
                ret[i] = deserialized;
            }
        }

        return (ret, totalConsumed);
    }

    public int SerializeUntyped(object obj, Span<byte> span) => Serialize((IEnumerable<TReturn>)obj, span);
    public int Serialize(IEnumerable<TReturn> t, Span<byte> span)
    {
        var enumerated = t.ToList();

        if (enumerated.Count != Count)
        {
            throw new InvalidOperationException($"Vector must contain {Count} elements, given {enumerated.Count}");
        }
        
        var fixedParts = new int[enumerated.Count];
        int consumed = 0;

        // First pass, write fixed parts and placeholders for variable offsets
        for (int i = 0; i < enumerated.Count; i++)
        {
            var value = enumerated[i];
            if (MemberType.IsVariableLength())
            {
                //fixedParts[i] = span.Slice(consumed, 4);
                fixedParts[i] = consumed;
                consumed += 4;
            }
            else
            {
                fixedParts[i] = consumed;
                var serializedLength = MemberType.Serialize(value, span.Slice(consumed));
                consumed += serializedLength;
            }
        }

        int fixedLength = consumed;
        
        // Second pass, serialize and write variable parts
        for (int i = 0; i < enumerated.Count; i++)
        {
            var value = enumerated[i];
            if (MemberType.IsVariableLength())
            {
                var variablePartSpan = span.Slice(consumed);
                var serializedLength = MemberType.Serialize(value, variablePartSpan);
                BitConverter.GetBytes(consumed).CopyTo(span.Slice(fixedParts[i], 4));
                consumed += serializedLength;
            }
        }

        return consumed;
    }
    
    public int Length(IEnumerable<TReturn> t) => (int)(MemberType.IsVariableLength() ? t.Select(MemberType.Length).Sum() + (MemberType.IsVariableLength() ? SszConstants.BytesPerOffset * Count : 0) : Count * MemberType.Length(default));
    public bool IsVariableLength() => MemberType.IsVariableLength();
    public long ChunkCount(IEnumerable<TReturn> t)
    {
        if (MemberType is SszInteger || MemberType is SszBoolean)
        {
            return (Count * MemberType.Length(default(TReturn)) + 31) / 32;
        }

        return Count;
    }
    public int LengthUntyped(object t) => Length((IEnumerable<TReturn>)t);
    public long ChunkCountUntyped(object t) => ChunkCount((IEnumerable<TReturn>)t);
}