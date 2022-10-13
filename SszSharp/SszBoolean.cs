namespace SszSharp;

public class SszBoolean : ISszType<bool>
{
    public int SerializeUntyped(object obj, Span<byte> span) => Serialize((bool)obj, span);
    public int Serialize(bool b, Span<byte> span)
    {
        span[0] = (byte)(b ? 1 : 0);
        return 1;
    }

    public (bool, int) Deserialize(ReadOnlySpan<byte> span) => (span[0] == 1, 1);
    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span) => Deserialize(span);
    public int Length(bool b) => 1;
    public long ChunkCount(bool b) => 1;
    public int LengthUntyped(object t) => 1;
    public long ChunkCountUntyped(object t) => 1;
    public bool IsVariableLength() => false;
}