[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SszSharp.Tests")]

namespace SszSharp;
internal static class ReflectionHelpers
{
    public static bool IsBasicType(this ISszType type) => type is SszBoolean || type is SszInteger;
    public static bool IsList(this ISszType type) => (type.GetType().IsGenericType && type.GetType().GetGenericTypeDefinition() == typeof(SszList<,>));
    public static bool IsVector(this ISszType type) => (type.GetType().IsGenericType && type.GetType().GetGenericTypeDefinition() == typeof(SszVector<,>));
    public static bool IsContainer(this ISszType type) => (type.GetType().IsGenericType && type.GetType().GetGenericTypeDefinition() == typeof(SszContainer<>));
    public static bool IsUnion(this ISszType type) => type is SszUnion;

    public static ISszContainerSchema GetSchema(this ISszType type) =>
        (ISszContainerSchema) (type.GetType().GetField("Schema")!.GetValue(type)!);

    public static IEnumerable<object> GetGenericEnumerable(this object o) => GetTypedEnumerable<object>(o);
    public static IEnumerable<T> GetTypedEnumerable<T>(this object o) => (IEnumerable<T>)(typeof(Enumerable).GetMethod("Cast")!.MakeGenericMethod(new[] {typeof(T)}).Invoke(null, new object[] { o })!);
    public static object GetTypedEnumerable(this object o, Type t) => (typeof(Enumerable).GetMethod("Cast")!.MakeGenericMethod(new[] {t}).Invoke(null, new object[] { o })!);

    public static Type? GetEnumerationMemberType(this Type type)
    {
        Type? memberRepresentativeType = default;
        if (type.IsArray)
        {
            memberRepresentativeType = type.GetElementType();
        }
        else if (type.GetInterfaces()
                 .Any(iface => iface.FullName?.StartsWith("System.Collections.Generic.IList") ?? false))
        {
            memberRepresentativeType = type.GetInterfaces()
                .First(iface => iface.FullName?.StartsWith("System.Collections.Generic.IList") ?? false).GenericTypeArguments[0];
        }
        else if (type == typeof(string))
        {
            return typeof(byte);
        }

        return memberRepresentativeType;
    }
}