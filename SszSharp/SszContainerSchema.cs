namespace SszSharp;

public interface ISszContainerSchema
{
    public ISszType[] FieldTypes { get; }
    public string[] FieldNames { get; }
    public void SetUntyped(object t, int fieldIndex, object value);
    public object GetUntyped(object t, int fieldIndex);
    public object DefaultUntyped();
}

public interface ISszContainerSchema<T> : ISszContainerSchema
{
    public void Set(T t, int fieldIndex, object value);
    public object Get(T t, int fieldIndex);
    public T Default();
}

public class SszContainerSchema<T> : ISszContainerSchema<T>
{
    public ISszType[] FieldTypes { get; private set; }
    public string[] FieldNames { get; private set; }
    public readonly Action<T, object>[] Setters;
    public readonly Func<T, object>[] Getters;
    public readonly Func<T> Factory;

    public SszContainerSchema(ISszType[] fieldTypes, Func<T, object>[] getters, Action<T, object>[] setters, string[] names, Func<T> factory)
    {
        FieldTypes = fieldTypes;
        Setters = setters;
        Getters = getters;
        FieldNames = names;
        Factory = factory;
    }
    
    public SszContainerSchema(ISszType[] fieldTypes, Func<T, object>[] getters, Action<T, object>[] setters, string[] names, Func<object> factory)
    {
        FieldTypes = fieldTypes;
        Setters = setters;
        Getters = getters;
        FieldNames = names;
        Factory = () => (T)factory();
    }

    public void Set(T t, int fieldIndex, object value) => Setters[fieldIndex](t, value);
    public object Get(T t, int fieldIndex) => Getters[fieldIndex](t);
    public T Default() => Factory();
    public void SetUntyped(object t, int fieldIndex, object value) => Set((T) t, fieldIndex, value);
    public object GetUntyped(object t, int fieldIndex) => Get((T) t, fieldIndex);
    public object DefaultUntyped() => Default();
}