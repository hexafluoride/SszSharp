namespace SszSharp;

public class SszUnion : ISszType<SszUnionWrapper>
{
    public Type RepresentativeType => typeof(SszUnionWrapper);
    public readonly ISszType?[] MemberTypes;

    public SszUnion(ISszType?[] memberTypes)
    {
        MemberTypes = memberTypes;
    }
    
    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public (SszUnionWrapper, int) Deserialize(ReadOnlySpan<byte> span)
    {
        var typeIndex = span[0];

        if (typeIndex >= MemberTypes.Length || typeIndex >= 128)
        {
            throw new Exception($"Unrecognized type index {typeIndex}");
        }

        var type = MemberTypes[typeIndex];
        if (type is null)
        {
            if (typeIndex == 0)
            {
                return (new SszUnionWrapper(empty: true), 1);
            }
            else
            {
                throw new Exception($"Type at index {typeIndex} is None, which is illegal");
            }
        }

        (var value, var consumed) = type.DeserializeUntyped(span.Slice(1));
        return (new SszUnionWrapper(type, value), 1 + consumed);
    }

    public int SerializeUntyped(object obj, Span<byte> span) => Serialize((SszUnionWrapper)obj, span);
    public int Serialize(SszUnionWrapper t, Span<byte> span)
    {
        var index = Array.IndexOf(MemberTypes, t.TypeDescriptor);

        if (index == -1)
        {
            throw new Exception($"Unrecognized union type {t.TypeDescriptor}");
        }

        var valueIsNone = !t.HasValue;
        if (valueIsNone)
        {
            if (index != 0)
            {
                throw new Exception("Value is none but index is nonzero");
            }

            span[0] = 0;
            return 1;
        }
        
        span[0] = (byte)index;
        return 1 + t.TypeDescriptor!.SerializeUntyped(t.Value!, span.Slice(1));
    }

    public int LengthUntyped(object t) => Length((SszUnionWrapper)t);
    public long ChunkCountUntyped(object t) => ChunkCount((SszUnionWrapper)t);
    public int Length(SszUnionWrapper t) => !t.HasValue ? 0 : 1 + t.TypeDescriptor!.LengthUntyped(t.Value!);
    public long ChunkCount(SszUnionWrapper t) => 1;
    public bool IsVariableLength() => true;
}