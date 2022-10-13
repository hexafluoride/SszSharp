namespace SszSharp;

public class SszContainerSchema<T>
{
    public readonly ISszType[] FieldTypes;
    public readonly Action<T, object>[] Setters;
    public readonly Func<T, object>[] Getters;
    public readonly Func<T> Factory;

    public SszContainerSchema(ISszType[] fieldTypes, Func<T, object>[] getters, Action<T, object>[] setters, Func<T> factory)
    {
        FieldTypes = fieldTypes;
        Setters = setters;
        Getters = getters;
        Factory = factory;
    }
    
    public SszContainerSchema(ISszType[] fieldTypes, Func<T, object>[] getters, Action<T, object>[] setters, Func<object> factory)
    {
        FieldTypes = fieldTypes;
        Setters = setters;
        Getters = getters;
        Factory = () => (T)factory();
    }

    public void Set(T t, int fieldIndex, object value) => Setters[fieldIndex](t, value);
    public object Get(T t, int fieldIndex) => Getters[fieldIndex](t);
    public T Default() => Factory();
}