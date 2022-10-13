using System.Numerics;
using System.Security.Cryptography;

namespace SszSharp;

public static class Merkleizer
{
    static IEnumerable<byte[]> PackBytes(byte[] buffer)
    {
        var paddedLength = buffer.Length;
        if (paddedLength % SszConstants.BytesPerChunk != 0)
            paddedLength += (SszConstants.BytesPerChunk - (buffer.Length % SszConstants.BytesPerChunk));
        for (int i = 0; i < paddedLength; i += SszConstants.BytesPerChunk)
        {
            var minibuf = new byte[SszConstants.BytesPerChunk];
            Array.Copy(buffer, i, minibuf, 0, Math.Min(buffer.Length - i, minibuf.Length));
            yield return minibuf;
        }
    }

    public static IEnumerable<byte[]> Pack<T>(ISszType<T> itemType, IEnumerable<T> values)
    {
        var valuesEnumerated = values.ToList();
        int totalLength = itemType.Length(default!) * valuesEnumerated.Count;
        var buffer = new byte[totalLength];
        var span = new Span<byte>(buffer);
        int lastOffset = 0;

        foreach (var value in valuesEnumerated)
        {
            var written = itemType.Serialize(value, span.Slice(lastOffset));
            lastOffset += written;
        }

        return PackBytes(buffer);
    }
    
    public static IEnumerable<byte[]> Pack(ISszType itemType, IEnumerable<object> values)
    {
        var valuesEnumerated = values.ToList();
        int totalLength = itemType.LengthUntyped(default!) * valuesEnumerated.Count;
        var buffer = new byte[totalLength];
        var span = new Span<byte>(buffer);
        int lastOffset = 0;

        foreach (var value in valuesEnumerated)
        {
            var written = itemType.SerializeUntyped(value, span.Slice(lastOffset));
            lastOffset += written;
        }

        return PackBytes(buffer);
    }

    public static IEnumerable<byte[]> PackBits(bool[] bits)
    {
        var totalLength = (int)Math.Ceiling(bits.Length / 8d);
        var buffer = new byte[totalLength];
        var span = new Span<byte>(buffer);
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i])
            {
                span[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return PackBytes(buffer);
    }

    public static byte[] PadBigInteger(BigInteger value)
    {
        var unpadded = value.ToByteArray(isUnsigned: true, isBigEndian: false);

        if (unpadded.Length == 32)
            return unpadded;
        
        var buf = new byte[32];
        Array.Copy(unpadded, 0, buf, 0, unpadded.Length);
        return buf;
    }

    public static byte[] MixInLength(byte[] root, BigInteger length) =>
        MixInLength(root, PadBigInteger(length));
    public static byte[] MixInLength(byte[] root, byte[] length)
    {
        if (root.Length != 32 || length.Length != 32)
            throw new Exception($"Incorrect sizes {root.Length}, {length.Length} passed to MixInLength");

        var buf = new byte[32];
        Hash(new Span<byte>(buf), root, length);
        return buf;
    }

    public static byte[] MixInSelector(byte[] root, BigInteger selector) =>
        MixInSelector(root, PadBigInteger(selector));
    public static byte[] MixInSelector(byte[] root, byte[] selector)
    {
        if (root.Length != 32 || selector.Length != 32)
            throw new Exception($"Incorrect sizes {root.Length}, {selector.Length} passed to MixInSelector");

        var buf = new byte[32];
        Hash(new Span<byte>(buf), root, selector);
        return buf;
    }

    public static byte[] Merkleize(IEnumerable<byte[]> chunks, long limit = -1)
    {
        var chunksEnumerated = chunks.ToList();
        if (limit != -1 && chunksEnumerated.Count > limit)
            throw new Exception("Chunk count exceeds limit");

        var chunkCount = chunksEnumerated.Count;
        var padTarget = limit == -1 ? NextPowerOfTwo(chunksEnumerated.Count) : NextPowerOfTwo(limit);

        //chunksEnumerated.AddRange(Enumerable.Repeat<byte[]?>(new byte[32], padTarget - chunksEnumerated.Count));

        var layerCount = padTarget == 1 ? 0 : (int)(Math.Floor(Math.Log2(padTarget - 1)) + 1);
        
        if (chunksEnumerated.Count == 0)
        {
            return ZeroHash(layerCount);
        }

        for (int l = 0; l < layerCount; l++)
        {
            var padCount = chunkCount % 2;
            var paddedChunkCount = chunkCount + padCount;
            for (int i = 0; i < padCount; i++)
            {
                chunksEnumerated.Add(new byte[32]);
                chunksEnumerated[chunkCount + i] = ZeroHash(l);
            }

            for (int i = 0; i < paddedChunkCount; i += 2)
            {
                var span = new Span<byte>(chunksEnumerated[i / 2]);
                Hash(span, chunksEnumerated[i], chunksEnumerated[i + 1]);
            }

            chunkCount = paddedChunkCount / 2;
        }
        
        return chunksEnumerated[0];
    }

    public static byte[] HashTreeRoot(ISszType type, object value)
    {
        bool isVector = type.IsVector();
        bool isList = type.IsList();
        bool isBasic = type.IsBasicType();
        bool isContainer = type.IsContainer();
        bool isUnion = type.IsUnion();

        if (isBasic)
        {
            return Merkleize(Pack(type, new [] { value }));
        }

        if (isVector)
        {
            var elementType = type.GetElementType();
            if (elementType.IsBasicType())
            {
                return Merkleize(Pack(elementType, value.GetGenericEnumerable()));
            }
            else
            {
                return Merkleize(value.GetGenericEnumerable().Select(elem => HashTreeRoot(elementType, elem)));
            }
        }

        if (isList)
        {
            var elementType = type.GetElementType();
            var enumerated = value.GetGenericEnumerable().ToList();
            if (elementType.IsBasicType())
            {
                return MixInLength(Merkleize(Pack(elementType, enumerated), limit: type.ChunkCountUntyped(value)),
                    enumerated.Count);
            }
            else
            {
                return MixInLength(
                    Merkleize(enumerated.Select(elem => HashTreeRoot(elementType, elem)),
                        limit: type.ChunkCountUntyped(value)), enumerated.Count);
            }
        }

        if (isContainer)
        {
            var schema = type.GetSchema();
            return Merkleize(schema.FieldTypes.Select((t, i) =>
                HashTreeRoot(t, schema.GetUntyped(value, i))));
        }

        if (type is SszBitvector)
        {
            var bitsArray = value.GetTypedEnumerable<bool>().ToArray();
            return Merkleize(PackBits(bitsArray), type.ChunkCountUntyped(bitsArray));
        }

        if (type is SszBitlist)
        {
            var bitsArray = value.GetTypedEnumerable<bool>().ToArray();
            return MixInLength(Merkleize(PackBits(bitsArray), limit: type.ChunkCountUntyped(bitsArray)), bitsArray.Length);
        }
        
        // TODO: Implement unions

        throw new Exception("Unrecognized type");
    }

    private static readonly List<byte[]> ZeroHashes = new List<byte[]>() { new byte[32] };

    static byte[] ZeroHash(int depth)
    {
        if (depth >= ZeroHashes.Count)
        {
            for (int i = ZeroHashes.Count; i <= depth; i++)
            {
                ZeroHashes.Add(new byte[32]);
                Hash(new Span<byte>(ZeroHashes[i]), ZeroHashes[i - 1], ZeroHashes[i - 1]);
            }
        }

        return ZeroHashes[depth];
    }

    /*
     * export function zeroHash(depth: number): Uint8Array {
  if (depth >= zeroHashes.length) {
    for (let i = zeroHashes.length; i <= depth; i++) {
      zeroHashes[i] = digest2Bytes32(zeroHashes[i - 1], zeroHashes[i - 1]);
    }
  }
  return zeroHashes[depth];
}
     */
    
    public static long NextPowerOfTwo(int i) => (long)Math.Pow(2, Math.Ceiling(Math.Log2(i)));
    public static long NextPowerOfTwo(long l) => (long)Math.Pow(2, Math.Ceiling(Math.Log2(l)));
    public static long LastPowerOfTwo(int i) => (long)Math.Pow(2, Math.Floor(Math.Log2(i)));
    public static long LastPowerOfTwo(long l) => (long)Math.Pow(2, Math.Floor(Math.Log2(l)));

    public static bool IsBasicType(this ISszType type) => type is SszBoolean || type is SszInteger;
    public static bool IsList(this ISszType type) => (type.GetType().IsGenericType && type.GetType().GetGenericTypeDefinition() == typeof(SszList<,>));
    public static bool IsVector(this ISszType type) => (type.GetType().IsGenericType && type.GetType().GetGenericTypeDefinition() == typeof(SszVector<,>));
    public static bool IsContainer(this ISszType type) => (type.GetType().IsGenericType && type.GetType().GetGenericTypeDefinition() == typeof(SszContainer<>));
    public static bool IsUnion(this ISszType type) => type is SszUnion;
    public static ISszType GetElementType(this ISszType type) => (ISszType)(type.GetType().GetField("MemberType")!.GetValue(type)!);

    public static ISszContainerSchema GetSchema(this ISszType type) =>
        (ISszContainerSchema) (type.GetType().GetField("Schema")!.GetValue(type)!);
    public static ISszType GetFieldType(this ISszType type, int index) => GetSchema(type).FieldTypes[index];

    public static IEnumerable<object> GetGenericEnumerable(this object o) => GetTypedEnumerable<object>(o);
    public static IEnumerable<T> GetTypedEnumerable<T>(this object o) => (IEnumerable<T>)(typeof(Enumerable).GetMethod("Cast")!.MakeGenericMethod(new[] {typeof(T)}).Invoke(null, new object[] { o })!);
    public static int MerkleItemLength(this ISszType type) => type.IsBasicType() ? type.LengthUntyped(default!) : 32;

    public static (int, int, int) GetItemPosition(ISszType type, int index)
    {
        var typeType = type.GetType();

        if (!typeType.IsGenericType)
        {
            throw new Exception("Received non-generic type");
        }

        var deconstructedType = typeType.GetGenericTypeDefinition();
        if (deconstructedType == typeof(SszVector<,>) || deconstructedType == typeof(SszList<,>))
        {
            var elementType = (ISszType)(typeType.GetField("MemberType")!.GetValue(type)!);
            var length = elementType.MerkleItemLength();
            var start = index * length;
            return (start / SszConstants.BytesPerChunk, start % SszConstants.BytesPerChunk, (start % SszConstants.BytesPerChunk) + length);
        }
        
        if (deconstructedType == typeof(SszContainer<>))
        {
            var schema = (ISszContainerSchema) (typeType.GetField("Schema")!.GetValue(type)!);
            var fieldType = schema.FieldTypes[index];
            var length = fieldType.MerkleItemLength();
            return (index, 0, length);
        }
        
        throw new Exception("Only lists, vectors, and containers are supported.");
    }
    
    public static long GetGeneralizedIndex(ISszType type, IEnumerable<int> path)
    {
        var pathEnumerated = path.ToList();
        var root = 1L;
        bool typeIsListOrVector = type.IsList() || type.IsVector();

        foreach (var hop in pathEnumerated)
        {
            if (type.IsBasicType())
            {
                throw new Exception("Cannot continue further");
            }
            if (hop == -1)
            {
                if (!typeIsListOrVector)
                    throw new Exception("Cannot get length of non-list/vector type");
                type = new SszInteger(64);
                typeIsListOrVector = false;
                root = root * 2 + 1;
            }
            else
            {
                (var position, _, _) = GetItemPosition(type, hop);
                var baseIndex = typeIsListOrVector ? 2 : 1;
                root = root * baseIndex * NextPowerOfTwo(type.ChunkCountUntyped(default!)) + position;

                ISszType? elementType = (ISszType?)(typeIsListOrVector
                    ? (type.GetType().GetField("MemberType")!.GetValue(type))
                    : ((ISszContainerSchema?) type.GetType().GetField("Schema")?.GetValue(type))?.FieldTypes[hop]);
                
                type = elementType ?? throw new Exception("Could not resolve element type");
                typeIsListOrVector = type.IsList() || type.IsVector();
            }
        }

        return root;
    }

    public static long GeneralizedIndexParent(long index) => index / 2;
    public static long GeneralizedIndexChild(long index, bool right) => index * 2 + (right ? 1 : 0);
    public static long GeneralizedIndexSibling(long index) => index ^ 1;
    public static bool GetGeneralizedIndexBit(long index, int position) => (index & (1 << position)) > 0;
    public static int GetGeneralizedIndexLength(long index) => (int)Math.Log2(index);

    public static long ConcatGeneralizedIndices(IEnumerable<long> indices)
    {
        var o = 1L;
        foreach (var i in indices)
        {
            o = (o * LastPowerOfTwo(i) + (i - LastPowerOfTwo(i)));
        }

        return o;
    }
    
    public static IEnumerable<long> GetBranchIndices(long treeIndex)
    {
        var o = new List<long>() { GeneralizedIndexSibling(treeIndex) };
        while (o.Last() > 1)
        {
            o.Add(GeneralizedIndexSibling(GeneralizedIndexParent(o.Last())));
        }
        o.RemoveAt(o.Count - 1);
        return o;
    }

    public static IEnumerable<long> GetPathIndices(long treeIndex)
    {
        var o = new List<long>() {treeIndex};
        while (o.Last() > 1)
        {
            o.Add(GeneralizedIndexParent(o.Last()));
        }
        o.RemoveAt(o.Count - 1);
        return o;
    }

    public static IEnumerable<long> GetHelperIndices(IEnumerable<long> indices)
    {
        var indicesEnumerated = indices.ToList();
        var helperIndices = indicesEnumerated.SelectMany(GetBranchIndices).ToHashSet();
        var pathIndices = indicesEnumerated.SelectMany(GetPathIndices).ToHashSet();
        helperIndices.ExceptWith(pathIndices);
        return helperIndices.OrderByDescending(i => i).ToList();
    }
    
    public static byte[] CalculateMerkleRoot(byte[] leaf, byte[][] proof, long index)
    {
        if (proof.Length != GetGeneralizedIndexLength(index))
            throw new Exception("Assertion failed");

        var leafSpan = new Span<byte>(leaf);
        for (int i = 0; i < proof.Length; i++)
        {
            byte[] h = proof[i];
            if (GetGeneralizedIndexBit(index, i))
            {
                Hash(leafSpan, h, leaf);
            }
            else
            {
                Hash(leafSpan, leaf, h);
            }
        }

        return leaf;
    }

    public static int Hash(Span<byte> output, byte[] left, byte[] right)
    {
        return SHA256.HashData(left.Concat(right).ToArray(), output);
    }
}