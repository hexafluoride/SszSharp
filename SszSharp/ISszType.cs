namespace SszSharp;

public interface ISszType
{
    public Type RepresentativeType { get; }
    public (object, int) DeserializeUntyped(ReadOnlySpan<byte> span);
    public int SerializeUntyped(object t, Span<byte> span);
    public int LengthUntyped(object t);
    public long ChunkCountUntyped(object t);
    public bool IsVariableLength();
}

public interface ISszType<T> : ISszType
{
    public (T, int) Deserialize(ReadOnlySpan<byte> span);
    public int Serialize(T t, Span<byte> span);
    public int Length(T t);
    public long ChunkCount(T t);
}