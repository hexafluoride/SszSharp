using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace SszSharp;

public static class SszSchemaGenerator
{
    public static object? Wrap(ISszType sszType, object? value)
    {
        if (sszType is SszInteger integerType)
        {
            if (value.GetType().IsEnum)
            {
                switch (integerType.Bits)
                {
                    case 8:
                        value = Convert.ToByte(value);
                        break;
                    default:
                        throw new Exception("Non-uint8 enums are not supported");
                }
            }
            
            return new SszIntegerWrapper(integerType.Bits, value);
        }
        
        var typeType = sszType.GetType();

        if (sszType is ISszCollection collection)
        {
            var elementType = collection.MemberTypeUntyped;
            var representativeValueType = elementType.RepresentativeType;
            IEnumerable? valueEnumerable;
            
            if (value is string valueString && elementType is SszInteger integerElement)
            {
                if (integerElement.Bits != 8)
                {
                    throw new Exception($"Strings must be backed by uint8 collection, found uint{integerElement.Bits}");
                }

                valueEnumerable = Encoding.UTF8.GetBytes(valueString);
            }
            else if (value is not IEnumerable)
            {
                throw new Exception("Value not enumerable");
            }
            else
            {
                valueEnumerable = value as IEnumerable;
            }

            var enumerableCast = valueEnumerable.Cast<object>();
            
            var wrappedEnumerable = enumerableCast.Select(element => Wrap(elementType, element));
            return wrappedEnumerable.GetTypedEnumerable(representativeValueType);
        }

        return value;
    }

    public static object? Unwrap(ISszType sourceType, Type targetType, object? value)
    {
        if (sourceType is SszInteger integerType)
        {
            if (value is not SszIntegerWrapper integerWrapper)
                throw new Exception("Type is integer but value is not integer wrapper");

            if (targetType == typeof(BigInteger) && integerWrapper.IntegerType != typeof(BigInteger))
            {
                return new BigInteger((ulong)integerWrapper.Value);
            }
            else if (integerWrapper.IntegerType == typeof(BigInteger))
            {
                if (targetType == typeof(byte[]))
                {
                    var unpadded =
                        ((BigInteger) integerWrapper.Value).ToByteArray(isUnsigned: true, isBigEndian: false);
                    var targetLength = integerWrapper.Bits / 8;
                    if (unpadded.Length == targetLength)
                        return unpadded;
                    byte[] ret = new byte[targetLength];
                    Array.Copy(unpadded, 0, ret, 0, unpadded.Length);
                    return ret;
                }
                
                if (targetType != typeof(BigInteger))
                {
                    throw new Exception($"{targetType} can not fit {integerWrapper.Bits} bits");
                }
            }

            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, integerWrapper.Value);
            }
            
            return Convert.ChangeType(integerWrapper.Value, targetType);
        }
        
        var typeType = sourceType.GetType();
        var typeInterfaces = typeType.GetInterfaces();
        var targetTypeInterfaces = targetType.GetInterfaces();

        if (sourceType is SszBitlist or SszBitvector)
        {
            if (value is not IEnumerable<bool> boolEnumerable)
                throw new Exception("value not bool enumerable");

            if (targetType == typeof(bool[]))
                return boolEnumerable.ToArray();

            if (targetType == typeof(List<bool>))
                return boolEnumerable.ToList();
        }

        if (sourceType is ISszCollection collection)
        {
            if (value is not IEnumerable valueEnumerable)
                throw new Exception("value not enumerable");
            var enumerableCast = valueEnumerable.Cast<object>();
            var elementType = collection.MemberTypeUntyped;
            var targetElementType = default(Type);

            if (targetType == typeof(string))
            {
                if (elementType is SszInteger integerElement)
                {
                    if (integerElement.Bits != 8)
                    {
                        throw new Exception(
                            $"Strings must be backed by uint8 collection, found uint{integerElement.Bits}");
                    }

                    return Encoding.UTF8.GetString(enumerableCast
                        .Select(element => (byte) ((SszIntegerWrapper) element).Value).ToArray());
                }
            }
            else if (targetType.IsArray)
            {
                targetElementType = targetType.GetElementType() ?? throw new Exception("Could not derive element type of array");
            }
            else if (targetTypeInterfaces.Any(iface => iface.FullName?.StartsWith("System.Collections.Generic.IList") ?? false))
            {
                targetElementType = targetTypeInterfaces
                    .First(iface => iface.FullName?.StartsWith("System.Collections.Generic.IList") ?? false).GenericTypeArguments[0];
            }
            else
            {
                throw new Exception("Could not derive element type");
            }
            
            var unwrappedEnumerable = enumerableCast.Select(element => Unwrap(elementType, targetElementType, element));

            if (targetType.IsArray)
            {
                return typeof(Enumerable).GetMethod("ToArray")!.MakeGenericMethod(new[] {targetElementType}).Invoke(null,
                    new [] {unwrappedEnumerable.GetTypedEnumerable(targetElementType)});
            }
            else if (targetType == typeof(List<>).MakeGenericType(new [] { targetElementType }))
            {
                return typeof(Enumerable).GetMethod("ToList")!.MakeGenericMethod(new[] {targetElementType}).Invoke(null,
                    new [] {unwrappedEnumerable.GetTypedEnumerable(targetElementType)});
            }
            else
            {
                throw new Exception($"Target type {targetType} unsupported");
            }
        }
        
        return value;
    }

    public static Dictionary<(Type, SizePreset?), ISszContainerSchema> CachedSchemas = new();

    public static SszContainerSchema<T> GetSchema<T>(SizePreset? preset, Func<T>? factory = null)
    {
        preset ??= SizePreset.DefaultPreset;
        var cacheKey = (typeof(T), preset);
        if (CachedSchemas.ContainsKey(cacheKey))
            return (SszContainerSchema<T>)CachedSchemas[cacheKey];

        var schema = new SszContainerSchema<T>(GetSchemaFieldsFromAttributes<T>(preset),
            factory ?? (() => Activator.CreateInstance<T>()), preset);
        
        CachedSchemas[cacheKey] = schema;
        return schema;
    }
    
    public static ISszContainerSchema GetSchema(Type t, SizePreset? preset, Func<object>? factory = null)
    {
        preset ??= SizePreset.DefaultPreset;
        var cacheKey = (t, preset);
        if (CachedSchemas.ContainsKey(cacheKey))
            return CachedSchemas[cacheKey];
        
        var fields = typeof(SszSchemaGenerator).GetMethod(nameof(GetSchemaFieldsFromAttributes), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t)
            .Invoke(null, new object?[] {preset}) ?? throw new Exception($"Could not generate schema for {t}");
        var containerType = typeof(SszContainerSchema<>).MakeGenericType(t);
        var schema =
            (ISszContainerSchema) (Activator.CreateInstance(containerType, fields,
                                       factory ?? (() => Activator.CreateInstance(t)!),
                                       preset) ??
                                   throw new Exception($"Could not construct SszContainer for {t}"));
        
        CachedSchemas[cacheKey] = schema;
        return schema;
    }
    
    static ISszContainerField<T>[] GetSchemaFieldsFromAttributes<T>(SizePreset? preset)
    {
        var containerType = typeof(T);
        var typeProperties = containerType.GetProperties();
        var sszFieldCount =
            typeProperties.Count(prop => prop.GetCustomAttributes(typeof(SszElementAttribute), true).Any());

        var fields = new ISszContainerField<T>[sszFieldCount];

        foreach (var property in typeProperties)
        {
            var elementAttributes = property.GetCustomAttributes(typeof(SszElementAttribute), true);
            if (!elementAttributes.Any())
                continue;

            var elementAttribute = elementAttributes.OfType<SszElementAttribute>().Single();
            var index = elementAttribute.Index;
            var type = SszElementAttribute.ConstructType(elementAttribute.TypeDescriptor, property.PropertyType, preset) ??
                       throw new Exception(
                           $"Failed to construct type for property {property.Name} in type {containerType}");

            var field = new SszContainerField<T>(
                fieldType: type,
                getter: (p) => Wrap(type, property.GetValue(p)),
                setter: (t, p) => property.SetValue(t, Unwrap(type, property.PropertyType, p)),
                name: property.Name
            );

            fields[index] = field;
        }

        return fields;
    }
}