namespace SszSharp;

public interface ISszContainerSchema
{
    public SizePreset? Preset { get; }
    public ISszContainerField[] FieldsUntyped { get; }
    public void SetUntyped(object t, int fieldIndex, object? value);
    public object? GetUntyped(object t, int fieldIndex);
    public object? DefaultUntyped();
}

public interface ISszContainerSchema<T> : ISszContainerSchema
{
    public ISszContainerField<T>[] Fields { get; }
    public void Set(T t, int fieldIndex, object? value);
    public object? Get(T t, int fieldIndex);
    public T Default();
}

public interface ISszContainerField
{
    public ISszType FieldType { get; }
    public string Name { get; }
    public void SetUntyped(object t, object? value);
    public object? GetUntyped(object t);
}

public interface ISszContainerField<T> : ISszContainerField
{
    public void Set(T t, object? value);
    public object? Get(T t);
}

public class SszContainerField<T> : ISszContainerField<T>
{
    public ISszType FieldType { get; }
    public string Name { get; }
    
    readonly Func<T, object?> Getter;
    readonly Action<T, object?> Setter;

    public SszContainerField(ISszType fieldType, Func<T, object?> getter, Action<T, object?> setter, string name)
    {
        FieldType = fieldType;
        Getter = getter;
        Setter = setter;
        Name = name;
    }

    public object? Get(T t) => Getter(t);
    public object? GetUntyped(object t) => Getter((T) t);
    public void Set(T t, object? value) => Setter(t, value);
    public void SetUntyped(object t, object? value) => Setter((T) t, value);
}

public class SszContainerSchema<T> : ISszContainerSchema<T>
{
    public SizePreset? Preset { get; }
    public ISszContainerField[] FieldsUntyped { get; }
    public ISszContainerField<T>[] Fields { get; }
    public readonly Func<T> Factory;

    public SszContainerSchema(ISszContainerField<T>[] fields, Func<T> factory, SizePreset? preset)
    {
        FieldsUntyped = Fields = fields;
        Factory = factory;
        Preset = preset;
    }
    public SszContainerSchema(ISszContainerField<T>[] fields, Func<object> untypedFactory, SizePreset? preset)
    {
        FieldsUntyped = Fields = fields;
        Factory = () => (T)untypedFactory();
        Preset = preset;
    }

    public void Set(T t, int fieldIndex, object? value) => Fields[fieldIndex].Set(t, value);
    public object? Get(T t, int fieldIndex) => Fields[fieldIndex].Get(t);
    public void SetUntyped(object t, int fieldIndex, object? value) => Fields[fieldIndex].SetUntyped(t, value);
    public object? GetUntyped(object t, int fieldIndex) => Fields[fieldIndex].GetUntyped(t);
    public T Default() => Factory();
    public object? DefaultUntyped() => Default();
}