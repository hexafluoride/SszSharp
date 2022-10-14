using System.Text;

namespace SszSharp;

public class SszElementAttribute : Attribute
{
    public int Index { get; set; }
    public string TypeDescriptor { get; set; }
    public SszElementAttribute(int index, string type)
    {
        Index = index;
        TypeDescriptor = type;
    }

    public static string[] SplitUpperLevelCommas(string commaSeparated)
    {
        var ret = new List<string>();
        int bracketLevel = 0;
        var currentFragment = new StringBuilder();

        for (int i = 0; i < commaSeparated.Length; i++)
        {
            var c = commaSeparated[i];

            if (c == '[')
            {
                bracketLevel++;
            }
            else if (c == ']')
            {
                bracketLevel--;
            }

            if (c == ',')
            {
                if (bracketLevel == 0)
                {
                    ret.Add(currentFragment.ToString().Trim());
                    currentFragment.Clear();
                    continue;
                }
            }

            currentFragment.Append(c);
        }

        var remaining = currentFragment.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            ret.Add(remaining);
        }

        return ret.ToArray();
    }

    public static ISszType? ConstructType(string descriptor, Type parameterType)
    {
        if (descriptor.StartsWith("uint"))
        {
            var bitNumberSpec = descriptor.Substring("uint".Length);
            if (int.TryParse(bitNumberSpec, out int bitNumber))
            {
                return new SszInteger(bitNumber);
            }
        }
        
        switch (descriptor)
        {
            case "bool":
            case "boolean":
                return new SszBoolean();
            case "none":
                return null;
        }

        if (descriptor.StartsWith("Bitvector"))
        {
            var lengthSpec = descriptor.Substring("Bitvector".Length).Trim('[', ']');
            if (long.TryParse(lengthSpec, out long length))
            {
                return new SszBitvector(length);
            }
        }
        if (descriptor.StartsWith("Bitlist"))
        {
            var lengthSpec = descriptor.Substring("Bitlist".Length).Trim('[', ']');
            if (long.TryParse(lengthSpec, out long length))
            {
                return new SszBitlist(length);
            }
        }
        if (descriptor.StartsWith("Union"))
        {
            var typesSpec = SplitUpperLevelCommas(descriptor.Substring("Union".Length).Trim('[', ']'));
            return new SszUnion(typesSpec.Select(s => ConstructType(s, parameterType)).ToArray());
        }

        if (descriptor.StartsWith("Vector"))
        {
            var spec = SplitUpperLevelCommas(descriptor.Substring("Vector".Length).Trim('[', ']'));
            var typeSpec = spec[0];
            var lengthSpec = spec[1];
            if (long.TryParse(lengthSpec, out long length))
            {
                Type memberRepresentativeType = default(Type);

                if (parameterType.IsArray)
                {
                    memberRepresentativeType = parameterType.GetElementType();
                }
                else if (parameterType.GetInterfaces()
                         .Any(iface => iface.FullName.StartsWith("System.Collections.Generic.IList")))
                {
                    memberRepresentativeType = parameterType.GetInterfaces()
                        .First(iface => iface.FullName.StartsWith("System.Collections.Generic.IList")).GenericTypeArguments[0];
                }
                
                var memberType = ConstructType(typeSpec, memberRepresentativeType);
                var returnType = memberType.GetType().GetInterfaces()
                    .First(iface => iface.Name.StartsWith("ISszType") && iface.IsConstructedGenericType)
                    .GenericTypeArguments[0];
                return (ISszType)Activator.CreateInstance(
                    typeof(SszVector<,>).MakeGenericType(new[] {returnType, memberType.GetType()}), new object[] { memberType, length });
            }
        }
        if (descriptor.StartsWith("List"))
        {
            var spec = SplitUpperLevelCommas(descriptor.Substring("List".Length).Trim('[', ']'));
            var typeSpec = spec[0];
            var lengthSpec = spec[1];
            if (long.TryParse(lengthSpec, out long length))
            {
                Type memberRepresentativeType = default(Type);

                if (parameterType.IsArray)
                {
                    memberRepresentativeType = parameterType.GetElementType();
                }
                else if (parameterType.GetInterfaces()
                         .Any(iface => iface.FullName.StartsWith("System.Collections.Generic.IList")))
                {
                    memberRepresentativeType = parameterType.GetInterfaces()
                        .First(iface => iface.FullName.StartsWith("System.Collections.Generic.IList")).GenericTypeArguments[0];
                }
                
                var memberType = ConstructType(typeSpec, memberRepresentativeType);
                var returnType = memberType.GetType().GetInterfaces()
                    .First(iface => iface.Name.StartsWith("ISszType") && iface.IsConstructedGenericType)
                    .GenericTypeArguments[0];
                return (ISszType)Activator.CreateInstance(
                    typeof(SszList<,>).MakeGenericType(new[] {returnType, memberType.GetType()}), new object[] { memberType, length });
            }
        }

        if (descriptor == "Container")
        {
            var containerSchema = (typeof(SszSchemaGenerator).GetMethod("GetSchemaWithUntypedFactory")
                    .MakeGenericMethod(new[] {parameterType}))
                .Invoke(null, new object[] {() => Activator.CreateInstance(parameterType, null)});
            return (ISszType)(Activator.CreateInstance(typeof(SszContainer<>).MakeGenericType(new[] {parameterType}),
                new[] {containerSchema}));
        }

        if (descriptor.StartsWith("Container["))
        {
            var containerTypeName = descriptor.Substring("Container".Length).Trim('[', ']');
            var containerType = Type.GetType(containerTypeName) ?? throw new Exception($"Could not find type {containerTypeName}");
            
            var containerSchema = (typeof(SszSchemaGenerator).GetMethod("GetSchemaWithUntypedFactory").MakeGenericMethod(new[] {containerType}))
                .Invoke(null, new object[] {() => Activator.CreateInstance(containerType, null)});
            return (ISszType)(Activator.CreateInstance(typeof(SszContainer<>).MakeGenericType(new[] {containerType}),
                new[] {containerSchema}));
        }

        return null;
    }
}