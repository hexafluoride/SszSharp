namespace SszSharp;

public class SszBitvector : ISszType<IEnumerable<bool>>
{
    public readonly long Count;
    public Type RepresentativeType => typeof(IEnumerable<bool>);

    public SszBitvector(long count)
    {
        Count = count;
    }

    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public int SerializeUntyped(object t, Span<byte> span) => Serialize((IEnumerable<bool>) t, span);

    public int LengthUntyped(object t) => Length((IEnumerable<bool>) t);

    public long ChunkCountUntyped(object t) => ChunkCount((IEnumerable<bool>) t);

    public bool IsVariableLength() => false;

    public (IEnumerable<bool>, int) Deserialize(ReadOnlySpan<byte> span)
    {
        var ret = new bool[Count];
        long bytes = (Count / 8) + 1;
        if (span.Length < bytes)
        {
            throw new Exception($"Expected {bytes} bytes for Bitvector[{Count}], got {span.Length} bytes");
        }
        
        for (int i = 0; i < bytes; i++)
        {
            byte b = span[i];
            for (int j = 0; j < 8; j++)
            {
                var bitIndex = (i * 8) + j;

                if (bitIndex >= Count)
                {
                    continue;
                }
                
                bool bit = (b & (1 << j)) > 0;
                ret[bitIndex] = bit;
            }
        }

        return (ret, (int)bytes);
    }

    public int Serialize(IEnumerable<bool> t, Span<byte> span)
    {
        var enumerated = t.ToList();
        if (enumerated.Count != Count)
        {
            throw new Exception($"Expected {Count} bits, got {enumerated.Count}");
        }
        
        int index = 0;
        foreach (bool b in enumerated)
        {
            if (b)
            {
                span[index / 8] |= (byte)(1 << (index % 8));
            }
            index++;
        }

        return (index / 8) + 1;
    }

    public int Length(IEnumerable<bool> t) => (int)((Count / 8) + 1);
    public long ChunkCount(IEnumerable<bool> t) => (Count + 255) / 256;
}