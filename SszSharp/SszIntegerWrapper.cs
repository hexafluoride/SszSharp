using System.Numerics;

namespace SszSharp;

public struct SszIntegerWrapper
{
    public bool Equals(SszIntegerWrapper other)
    {
        return Value.Equals(other.Value) && Bits == other.Bits;
    }

    public override bool Equals(object? obj)
    {
        return obj is SszIntegerWrapper other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Bits);
    }

    public readonly Type IntegerType;
    public readonly object Value;
    public readonly byte[] Bytes;
    public readonly int Bits;

    public SszIntegerWrapper(int bits, ReadOnlySpan<byte> bytes)
    {
        Bits = bits;
        switch (bits)
        {
            case 8:
                IntegerType = typeof(byte);
                Value = bytes[0];
                break;
            case 16:
                IntegerType = typeof(ushort);
                Value = BitConverter.ToUInt16(bytes);
                break;
            case 32:
                IntegerType = typeof(uint);
                Value = BitConverter.ToUInt32(bytes);
                break;
            case 64:
                IntegerType = typeof(ulong);
                Value = BitConverter.ToUInt64(bytes);
                break;
            case 128:
            case 256:
                IntegerType = typeof(BigInteger);
                Value = new BigInteger(bytes.Slice(0, bits / 8), isUnsigned: true, isBigEndian: false);
                break;
            default:
                throw new InvalidOperationException($"SSZ integer of size {bits} is not supported");
        }

        Bytes = bytes.Slice(0, bits / 8).ToArray();
    }

    public SszIntegerWrapper(int bits, object value)
    {
        Bits = bits;
        Value = value;
        IntegerType = typeof(object);
        Bytes = Array.Empty<byte>();

        switch (bits)
        {
            case 8:
                IntegerType = typeof(byte);
                AssertType();
                Bytes = new[] {(byte)value};
                break;
            case 16:
                IntegerType = typeof(ushort);
                AssertType();
                Bytes = BitConverter.GetBytes((ushort) value);
                break;
            case 32:
                IntegerType = typeof(uint);
                AssertType();
                Bytes = BitConverter.GetBytes((uint) value);
                break;
            case 64:
                IntegerType = typeof(ulong);
                AssertType();
                Bytes = BitConverter.GetBytes((ulong)value);
                break;
            case 128:
            case 256:
                IntegerType = typeof(BigInteger);
                AssertType();
                var unpadded = ((BigInteger) value).ToByteArray(isUnsigned: true, isBigEndian: false);
                Bytes = new byte[Bits / 8];
                Array.Copy(unpadded, 0, Bytes, 0, unpadded.Length);
                break;
            default:
                throw new InvalidOperationException($"SSZ integer of size {bits} is not supported");
        }
    }
    
    void AssertType()
    {
        if (Value.GetType() != IntegerType)
        {
            throw new Exception($"Integer type {Value.GetType()} does not match expected type {IntegerType}");
        }
    }

}