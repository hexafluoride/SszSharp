namespace SszSharp;

public class SszInteger : ISszType<SszIntegerWrapper>
{
    public readonly int Bits;
    public readonly int[] AllowedSizes = new[] {8, 16, 32, 64, 128, 256}; 
    
    public SszInteger(int bits)
    {
        if (!AllowedSizes.Contains(bits))
        {
            throw new InvalidOperationException($"SSZ integer of size {bits} is not supported");
        }
        Bits = bits;
    }
    
    public int SerializeUntyped(object obj, Span<byte> span) => Serialize((SszIntegerWrapper)obj, span);
    public int Serialize(SszIntegerWrapper t, Span<byte> span)
    {
        if (t.Bits != Bits)
        {
            throw new Exception($"Expected uint{Bits}, got uint{t.Bits}");
        }
        t.Bytes.CopyTo(span);
        return t.Bytes.Length;
    }

    public (SszIntegerWrapper, int) Deserialize(ReadOnlySpan<byte> span) => (new SszIntegerWrapper(Bits, span), Bits / 8);
    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public int LengthUntyped(object t) => Bits / 8;
    public long ChunkCountUntyped(object t) => 1;
    public int Length(SszIntegerWrapper t) => Bits / 8;
    public long ChunkCount(SszIntegerWrapper t) => 1;
    public bool IsVariableLength() => false;
}