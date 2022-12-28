namespace SszSharp;

public class SszBitlist : ISszType<IEnumerable<bool>>
{
    public readonly long Capacity;
    public Type RepresentativeType => typeof(IEnumerable<bool>);

    public SszBitlist(long capacity)
    {
        Capacity = capacity;
    }

    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public int SerializeUntyped(object t, Span<byte> span) => Serialize((IEnumerable<bool>) t, span);

    public int LengthUntyped(object t) => Length((IEnumerable<bool>) t);

    public long ChunkCountUntyped(object t) => ChunkCount((IEnumerable<bool>) t);

    public bool IsVariableLength() => true;

    public (IEnumerable<bool>, int) Deserialize(ReadOnlySpan<byte> span)
    {
        var ret = new List<bool>();
        int lastTrueBit = 0;

        for (int i = 0; i < span.Length; i++)
        {
            byte b = span[i];
            for (int j = 0; j < 8; j++)
            {
                bool bit = (b & (1 << j)) > 0;
                ret.Add(bit);

                if (bit)
                    lastTrueBit = ret.Count - 1;
            }
        }

        if (lastTrueBit > Capacity)
        {
            throw new Exception($"Read {lastTrueBit} bits for Bitlist[{Capacity}]");
        }

        return (ret.Take(lastTrueBit), span.Length);
    }

    public int Serialize(IEnumerable<bool> t, Span<byte> span)
    {
        int totalLength = (t.Count() / 8) + 1;
        int index = 0;
        foreach (bool b in t)
        {
            if (b)
            {
                span[index / 8] |= (byte)(1 << (index % 8));
            }
            index++;
        }
        
        // Advance once for last bit
        span[index / 8] |= (byte)(1 << (index % 8));

        for (int i = (index / 8) + 1; i < totalLength; i++)
        {
            span[i] = 0;
        }

        return totalLength;
    }

    public int Length(IEnumerable<bool> t) => (t.Count() / 8) + 1;
    public long ChunkCount(IEnumerable<bool> t) => (Capacity + 255) / 256;
}