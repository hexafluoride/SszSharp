namespace SszSharp;

public interface ISszCollection
{
    public ISszType MemberTypeUntyped { get; }
    public long Capacity { get; }
}

public interface ISszCollection<TReturn, TDescriptor> : ISszCollection, ISszType<IEnumerable<TReturn>>
    where TDescriptor : ISszType<TReturn>
{
    public TDescriptor MemberType { get; }
}