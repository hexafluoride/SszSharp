namespace SszSharp;

public struct SszUnionWrapper
{
    public readonly ISszType? TypeDescriptor;
    public readonly Type? SszTypeDescriptorType;
    public readonly object? Value;
    public readonly bool HasValue;

    public SszUnionWrapper(bool empty)
    {
        if (!empty)
        {
            throw new Exception("Cannot use this constructor if not instantiating a None union value");
        }

        HasValue = false;
        TypeDescriptor = null;
        Value = null;
        SszTypeDescriptorType = null;
    }
    
    public SszUnionWrapper(ISszType typeDescriptor, object value)
    {
        SszTypeDescriptorType = typeDescriptor.GetType();
        TypeDescriptor = typeDescriptor;
        Value = value;
        HasValue = true;
    }

    public T Recover<T>()
    {
        if (!HasValue)
            return default;
        
        if (typeof(T) != TypeDescriptor.GetType().GenericTypeArguments[0])
            throw new Exception("Type mismatch in union");

        return (T)Value;
    }
}