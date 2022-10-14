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

    static long? ResolveLength(string lengthSpec, SizePreset? preset)
    {
        if (long.TryParse(lengthSpec, out long length))
            return length;

        if (preset?.Parameters.ContainsKey(lengthSpec) == true)
            return preset.Parameters[lengthSpec];

        return null;
    }

    public static ISszType? ConstructType(string descriptor, Type parameterType, SizePreset? preset)
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
            var length = ResolveLength(lengthSpec, preset);
            if (length.HasValue)
            {
                return new SszBitvector(length.Value);
            }
        }
        if (descriptor.StartsWith("Bitlist"))
        {
            var lengthSpec = descriptor.Substring("Bitlist".Length).Trim('[', ']');
            var length = ResolveLength(lengthSpec, preset);
            if (length.HasValue)
            {
                return new SszBitlist(length.Value);
            }
        }
        if (descriptor.StartsWith("Union"))
        {
            var typesSpec = SplitUpperLevelCommas(descriptor.Substring("Union".Length).Trim('[', ']'));
            return new SszUnion(typesSpec.Select(s => ConstructType(s, parameterType, preset)).ToArray());
        }

        if (descriptor.StartsWith("Vector"))
        {
            var spec = SplitUpperLevelCommas(descriptor.Substring("Vector".Length).Trim('[', ']'));
            var typeSpec = spec[0];
            var lengthSpec = spec[1];
            var length = ResolveLength(lengthSpec, preset);
            if (length.HasValue)
            {
                var memberRepresentativeType = parameterType.GetEnumerationMemberType() ??
                                               throw new Exception($"Could not resolve member type of parameter wih type {parameterType}");
                var memberType = ConstructType(typeSpec, memberRepresentativeType, preset) ??
                                 throw new Exception($"Could not create member SSZ type with typespec {typeSpec} and representative type {memberRepresentativeType}");
                var returnType = memberType.RepresentativeType;
                
                return (ISszType) (Activator.CreateInstance(
                                       typeof(SszVector<,>).MakeGenericType(returnType, memberType.GetType()), 
                                       memberType, 
                                       length.Value) ?? throw new Exception($"Failed to create vector of type {memberType}"));
            }
        }
        if (descriptor.StartsWith("List"))
        {
            var spec = SplitUpperLevelCommas(descriptor.Substring("List".Length).Trim('[', ']'));
            var typeSpec = spec[0];
            var lengthSpec = spec[1];
            var length = ResolveLength(lengthSpec, preset);
            if (length.HasValue)
            {
                var memberRepresentativeType = parameterType.GetEnumerationMemberType() ?? 
                                               throw new Exception($"Could not resolve member type of parameter wih type {parameterType}");
                var memberType = ConstructType(typeSpec, memberRepresentativeType, preset) ??
                                 throw new Exception($"Could not create member SSZ type with typespec {typeSpec} and representative type {memberRepresentativeType}");
                var returnType = memberType.RepresentativeType;
                
                return (ISszType) (Activator.CreateInstance(
                                       typeof(SszList<,>).MakeGenericType(returnType, memberType.GetType()), 
                                       memberType,
                                       length.Value) ?? throw new Exception($"Failed to create list of type {memberType}"));
            }
        }

        if (descriptor == "Container")
        {
            var containerSchema = SszSchemaGenerator.GetSchema(parameterType, preset);
            return (ISszType)(Activator.CreateInstance(typeof(SszContainer<>).MakeGenericType(parameterType), containerSchema) ?? throw new Exception($"Failed to create container for {parameterType}"));
        }

        if (descriptor.StartsWith("Container["))
        {
            var containerTypeName = descriptor.Substring("Container".Length).Trim('[', ']');
            var containerType = Type.GetType(containerTypeName) ?? throw new Exception($"Could not find type {containerTypeName}");
            
            var containerSchema = SszSchemaGenerator.GetSchema(containerType, preset);
            return (ISszType)(Activator.CreateInstance(typeof(SszContainer<>).MakeGenericType(containerType), containerSchema) ?? throw new Exception($"Failed to create container for {containerType}"));
        }

        throw new Exception($"Could not construct type from typespec {descriptor}");
    }
}