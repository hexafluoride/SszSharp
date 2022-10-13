using System.Collections;
using System.Numerics;

namespace SszSharp;

public static class SszSchemaGenerator
{
    public static object Wrap(ISszType sszType, object value)
    {
        if (sszType is SszInteger integerType)
        {
            return new SszIntegerWrapper(integerType.Bits, value);
        }
        
        var typeType = sszType.GetType();
        var typeInterfaces = typeType.GetInterfaces();

        if (typeType.Name.StartsWith("SszVector") || typeType.Name.StartsWith("SszList"))
        {
            if (value is not IEnumerable valueEnumerable)
                throw new Exception("value not enumerable");
            var enumerableCast = valueEnumerable.Cast<object>();
            var elementType = (ISszType) typeType
                .GetField("MemberType")
                .GetValue(sszType);

            var representativeValueType = elementType.GetType().GetInterfaces()
                .First(iface => iface.Name.StartsWith("ISszType") && iface.IsGenericType).GenericTypeArguments[0];
            var wrappedEnumerable = enumerableCast.Select(element => Wrap(elementType, element));
            var wrappedCastEnumerable = typeof(Enumerable).GetMethod("Cast")
                .MakeGenericMethod(new[] {representativeValueType}).Invoke(null, new object[] { wrappedEnumerable });
            //var realType = 
            return wrappedCastEnumerable;
        }

        return value;
    }

    public static object Unwrap(ISszType sourceType, Type targetType, object value)
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

            return Convert.ChangeType(integerWrapper.Value, targetType);
        }
        
        var typeType = sourceType.GetType();
        var typeInterfaces = typeType.GetInterfaces();
        var targetTypeInterfaces = targetType.GetInterfaces();

        if (typeType.Name.StartsWith("SszVector") || typeType.Name.StartsWith("SszList"))
        {
            if (value is not IEnumerable valueEnumerable)
                throw new Exception("value not enumerable");
            var enumerableCast = valueEnumerable.Cast<object>();
            var elementType = (ISszType) typeType
                .GetField("MemberType")
                .GetValue(sourceType);
            var targetElementType = default(Type);

            if (targetType.IsArray)
            {
                targetElementType = targetType.GetElementType();
            }
            else if (targetTypeInterfaces.Any(iface => iface.FullName.StartsWith("System.Collections.Generic.IList")))
            {
                targetElementType = targetTypeInterfaces
                    .First(iface => iface.FullName.StartsWith("System.Collections.Generic.IList")).GenericTypeArguments[0];
            }
            
            var unwrappedEnumerable = enumerableCast.Select(element => Unwrap(elementType, targetElementType, element));

            if (targetType.IsArray)
            {
                //return unwrappedEnumerable.ToArray();
                return typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(new [] { targetElementType }).Invoke(null, new object[] {(typeof(Enumerable).GetMethod("Cast")
                    .MakeGenericMethod(new[] {targetElementType}).Invoke(null, new object[] { unwrappedEnumerable })) });
            }
            else if (targetType == typeof(List<>).MakeGenericType(new [] { targetElementType }))
            {
                return typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(new [] { targetElementType }).Invoke(null, new object[] {(typeof(Enumerable).GetMethod("Cast")
                    .MakeGenericMethod(new[] {targetElementType}).Invoke(null, new object[] { unwrappedEnumerable })) });
            }
            else
            {
                throw new Exception($"Target type {targetType} unsupported");
            }
        }
        
        return value;
    }
    
    public static SszContainerSchema<T> GetSchema<T>(Func<T> factory)
    {
        (var fieldTypes, var getters, var setters) = GetSchemaElements<T>();
        return new SszContainerSchema<T>(fieldTypes, getters, setters, factory);
    }
    
    public static SszContainerSchema<T> GetSchemaWithUntypedFactory<T>(Func<object> factory)
    {
        (var fieldTypes, var getters, var setters) = GetSchemaElements<T>();
        return new SszContainerSchema<T>(fieldTypes, getters, setters, factory);
    }
    
    static (ISszType[], Func<T, object>[], Action<T, object>[]) GetSchemaElements<T>()
    {
        var containerType = typeof(T);
        var typeProperties = containerType.GetProperties();
        var sszFieldCount =
            typeProperties.Count(prop => prop.GetCustomAttributes(typeof(SszElementAttribute), true).Any());
        
        var fieldTypes = new ISszType[sszFieldCount];
        var getters = new Func<T, object>[sszFieldCount];
        var setters = new Action<T, object>[sszFieldCount];

        foreach (var property in typeProperties)
        {
            var elementAttributes = property.GetCustomAttributes(typeof(SszElementAttribute), true);
            if (!elementAttributes.Any())
                continue;

            var elementAttribute = elementAttributes.OfType<SszElementAttribute>().Single();
            var index = elementAttribute.Index;
            var type = SszElementAttribute.ConstructType(elementAttribute.TypeDescriptor, property.PropertyType);

            fieldTypes[index] = type;
            getters[index] = (p) => Wrap(type, property.GetValue(p));
            setters[index] = (t, p) => property.SetValue(t, Unwrap(type, property.PropertyType, p));
        }

        return (fieldTypes, getters, setters);
    }
}