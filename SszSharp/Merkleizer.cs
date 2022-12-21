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

    public static IEnumerable<byte[]> GetChunks(ISszType type, object value, IEnumerable<long> chunkIndices)
    {
        bool isVector = type.IsVector();
        bool isList = type.IsList();
        bool isBasic = type.IsBasicType();
        bool isContainer = type.IsContainer();
        bool isUnion = type.IsUnion();

        if (isBasic)
        {
            return MerkleizeMany(Pack(type, new [] { value }), chunkIndices);
        }
        
        if (type is SszBitvector)
        {
            var bitsArray = value.GetTypedEnumerable<bool>().ToArray();
            return MerkleizeMany(PackBits(bitsArray), chunkIndices, limit: type.ChunkCountUntyped(bitsArray));
        }
        
        if (type is SszBitlist)
        {
            var bitsArray = value.GetTypedEnumerable<bool>().ToArray();
            return MerkleizeMany(PackBits(bitsArray), chunkIndices, limit: type.ChunkCountUntyped(bitsArray));
        }
        
        var chunkIndicesEnumerated = chunkIndices.ToList();
        var childChunks = new Dictionary<long, byte[]>();
        var leafCount = Merkleizer.NextPowerOfTwo(type.ChunkCountUntyped(default!));
        var ourIndices = chunkIndicesEnumerated.Where(chunkIndex => chunkIndex < leafCount * 2).ToList();
        var childIndices = chunkIndicesEnumerated.Where(chunkIndex => chunkIndex >= leafCount * 2).ToList();

        if (isVector)
        {
            var elementType = (type as ISszCollection)!.MemberTypeUntyped;

            if (elementType.IsBasicType())
            {
                return MerkleizeMany(Pack(elementType, value.GetGenericEnumerable()), chunkIndicesEnumerated);
            }
            else
            {
                var childValues = value.GetGenericEnumerable().ToList();
                var localChunks = new List<byte[]>();

                for (int i = 0; i < childValues.Count; i++)
                {
                    var originalIndices = new List<long>() { 1 };
                    var indicesUnderElement = new List<long>() { 1 };
                    for (int j = 0; j < childIndices.Count; j++)
                    {
                        var childIndex = childIndices[j];
                        var parentAtLevel = childIndex;
                        while (parentAtLevel >= leafCount * 2)
                        {
                            parentAtLevel = GeneralizedIndexParent(parentAtLevel);
                        }

                        var indexAtLevel = parentAtLevel - (leafCount + 1);
                        if (indexAtLevel == i)
                        {
                            childIndices.RemoveAt(j);

                            originalIndices.Add(childIndex);
                            childIndex -= (parentAtLevel - 1) * 2 * LastPowerOfTwo(childIndex / (parentAtLevel - 1));
                            indicesUnderElement.Add(childIndex);
                        }
                    }

                    var childMerkles = GetChunks(elementType, childValues[i], indicesUnderElement).ToList();
                    localChunks.Add(childMerkles[0]);
                    for (int j = 1; j < indicesUnderElement.Count; j++)
                    {
                        childChunks[originalIndices[j]] = childMerkles[j];
                    }
                }

                var ourChunks = MerkleizeMany(localChunks, ourIndices).ToList();
                for (int i = 0; i < ourChunks.Count; i++)
                {
                    childChunks[ourIndices[i]] = ourChunks[i];
                }

                return chunkIndicesEnumerated.Select(i => childChunks[i]).ToList();
            }
        }
        
        if (isList)
        {
            var elementType = (type as ISszCollection)!.MemberTypeUntyped;
            var lengthMappedIndices = chunkIndicesEnumerated
                .Select(chunkIndex => chunkIndex == 1 ? 1 : chunkIndex - (LastPowerOfTwo(chunkIndex))).ToList();
            var childValues = value.GetGenericEnumerable().ToList();

            var submerkles = new List<byte[]>();

            var ourIndicesUnmapped = new List<long>();
            var ourIndicesMapped = new List<long>();

            for (int i = 0; i < lengthMappedIndices.Count; i++)
            {
                var index = lengthMappedIndices[i];
                if (index < leafCount * 2)
                {
                    ourIndicesUnmapped.Add(chunkIndicesEnumerated[i]);
                    ourIndicesMapped.Add(index);
                }
            }
            
            if (elementType.IsBasicType())
            {
                submerkles = MerkleizeMany(Pack(elementType, childValues), ourIndicesMapped,
                    limit: type.ChunkCountUntyped(default!)).ToList();
            }
            else
            {
                var localChunks = new List<byte[]>();

                for (int i = 0; i < childValues.Count; i++)
                {
                    var originalIndices = new List<long>() { 1 };
                    var indicesUnderElement = new List<long>() { 1 };
                    for (int j = 0; j < lengthMappedIndices.Count; j++)
                    {
                        var childIndex = lengthMappedIndices[j];
                        if (childIndex < leafCount * 2)
                            continue;
                        
                        var parentAtLevel = childIndex;
                        while (parentAtLevel >= leafCount * 2)
                        {
                            parentAtLevel = GeneralizedIndexParent(parentAtLevel);
                        }

                        var indexAtLevel = parentAtLevel - (leafCount);
                        if (indexAtLevel == i)
                        {
                            originalIndices.Add(chunkIndicesEnumerated[j]);
                            childIndex -= (parentAtLevel - 1) * 2 * LastPowerOfTwo(childIndex / (parentAtLevel - 1));
                            indicesUnderElement.Add(childIndex);
                        }
                    }
                    
                    var childMerkles = GetChunks(elementType, childValues[i], indicesUnderElement).ToList();
                    localChunks.Add(childMerkles[0]);
                    
                    for (int j = 1; j < indicesUnderElement.Count; j++)
                    {
                        childChunks[originalIndices[j]] = childMerkles[j];
                    }
                }
                
                submerkles = MerkleizeMany(localChunks, ourIndicesMapped, limit: type.ChunkCountUntyped(default!)).ToList();
            }
            
            for (int i = 0; i < ourIndicesMapped.Count; i++)
            {
                childChunks[ourIndicesUnmapped[i]] = submerkles[i];
            }

            childChunks[3] = PadBigInteger(childValues.Count);
            childChunks[2] = new byte[32];
            childChunks[1].CopyTo(childChunks[2], 0);
            childChunks[1] = new byte[32];
            Hash(childChunks[1], childChunks[2], childChunks[3]);
            return chunkIndicesEnumerated.Select(chunkIndex => childChunks[chunkIndex]).ToList();
        }
        
        if (isContainer)
        {
            var schema = type.GetSchema();
            var localChunks = new List<byte[]>();

            for (int i = 0; i < schema.FieldsUntyped.Length; i++)
            {
                var field = schema.FieldsUntyped[i];
                var fieldType = field.FieldType;
                var originalIndices = new List<long>() { 1 };
                var indicesUnderElement = new List<long>() { 1 };
                for (int j = 0; j < childIndices.Count; j++)
                {
                    var childIndex = childIndices[j];
                    var parentAtLevel = childIndex;

                    while (parentAtLevel >= leafCount * 2)
                    {
                        parentAtLevel = GeneralizedIndexParent(parentAtLevel);
                    }

                    var indexAtLevel = parentAtLevel - leafCount;
                    if (indexAtLevel == i)
                    {
                        originalIndices.Add(childIndex);
                        childIndex -= (long)((parentAtLevel - 1) * 2 * LastPowerOfTwo(childIndex / (parentAtLevel - 1)));
                        indicesUnderElement.Add(childIndex);
                    }
                }

                var childMerkles = GetChunks(fieldType, field.GetUntyped(value), indicesUnderElement).ToList();
                localChunks.Add(childMerkles[0]);
                for (int j = 1; j < indicesUnderElement.Count; j++)
                {
                    childChunks[originalIndices[j]] = childMerkles[j];
                }
            }

            var ourChunks = MerkleizeMany(localChunks, ourIndices).ToList();
            for (int i = 0; i < ourChunks.Count; i++)
            {
                childChunks[ourIndices[i]] = ourChunks[i];
            }

            return chunkIndicesEnumerated.Select(i => childChunks[i]).ToList();
        }
        
        // TODO: Implement unions

        throw new Exception("Unrecognized type");
    }

    public static byte[] Merkleize(IEnumerable<byte[]> chunks, long limit = -1, int chunkIndex = 1, char print = ' ')
    {
        return MerkleizeMany(chunks, new long[] {chunkIndex}, limit, print).Single();
    }
    
    public static IEnumerable<byte[]> MerkleizeMany(IEnumerable<byte[]> chunks, IEnumerable<long> chunkIndices, long limit = -1, char print = ' ')
    {
        var chunksEnumerated = chunks.Select(c => c.ToList().ToArray()).ToList();
        var chunkIndicesEnumerated = chunkIndices.ToHashSet();
        var chunkStorage = new Dictionary<long, byte[]>();
        
        if (limit != -1 && chunksEnumerated.Count > limit)
            throw new Exception("Chunk count exceeds limit");

        var chunkCount = chunksEnumerated.Count;
        var padTarget = limit == -1 ? NextPowerOfTwo(chunksEnumerated.Count) : NextPowerOfTwo(limit);
        var layerCount = padTarget == 1 ? 0 : (int)(Math.Floor(Math.Log2(padTarget - 1)) + 1);
        
        if (chunksEnumerated.Count == 0)
        {
            return Enumerable.Repeat(ZeroHash(layerCount), chunkIndicesEnumerated.Count);
        }

        foreach (var chunkIndex in chunkIndicesEnumerated)
        {
            if (chunkIndex >= padTarget)
            {
                var localIndex = (int) (chunkIndex - padTarget);
                if (localIndex < chunksEnumerated.Count)
                    chunkStorage[chunkIndex - 1] = chunksEnumerated[localIndex].ToArray();
                else
                {
                    chunkStorage[chunkIndex - 1] = ZeroHash(0);
                }
            }
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
                var index = (long)Math.Pow(2, layerCount - (l + 1)) + (i / 2);

                Hash(span, chunksEnumerated[i], chunksEnumerated[i + 1]);

                if (chunkIndicesEnumerated.Contains(index))
                {
                    chunkStorage[index - 1] = new byte[32];
                    span.CopyTo(chunkStorage[index - 1]);
                }
            }

            chunkCount = paddedChunkCount / 2;
        }

        byte[] GetChunk(int index)
        {
            if (chunkStorage.ContainsKey(index))
                return chunkStorage[index];
            
            var depth = layerCount - ((int)Math.Log2(index));
            return ZeroHash(depth);
        }

        return chunkIndicesEnumerated.Select(i => GetChunk((int)i - 1)).ToList();
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
            var elementType = (type as ISszCollection)!.MemberTypeUntyped;
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
            var elementType = (type as ISszCollection)!.MemberTypeUntyped;
            var enumerated = value.GetGenericEnumerable().ToList();
            if (elementType.IsBasicType())
            {
                return MixInLength(Merkleize(Pack(elementType, enumerated), limit: type.ChunkCountUntyped(value), print: 'b'),
                    enumerated.Count);
            }
            else
            {
                return MixInLength(
                    Merkleize(enumerated.Select(elem => HashTreeRoot(elementType, elem)),
                        limit: type.ChunkCountUntyped(value), print: 'l'), enumerated.Count);
            }
        }

        if (isContainer)
        {
            var schema = type.GetSchema();
            return Merkleize(schema.FieldsUntyped.Select((field, i) =>
                HashTreeRoot(field.FieldType, field.GetUntyped(value))), print: 'c');
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

    public static long NextPowerOfTwo(int i) => i == 0 ? 1 : (long)Math.Pow(2, Math.Ceiling(Math.Log2(i)));
    public static long NextPowerOfTwo(long l) => l == 0 ? 1 : (long)Math.Pow(2, Math.Ceiling(Math.Log2(l)));
    public static long LastPowerOfTwo(int i) => (long)Math.Pow(2, Math.Floor(Math.Log2(i)) - 1);
    public static long LastPowerOfTwo(long l) => (long)Math.Pow(2, Math.Floor(Math.Log2(l)) - 1);
    public static int MerkleItemLength(this ISszType type) => type.IsBasicType() ? type.LengthUntyped(default!) : 32;

    public static (int, int, int) GetItemPosition(ISszType type, int index)
    {
        var typeType = type.GetType();

        if (!typeType.IsGenericType)
        {
            throw new Exception("Received non-generic type");
        }

        var deconstructedType = typeType.GetGenericTypeDefinition();
        if (type is ISszCollection collection)
        {
            var elementType = collection.MemberTypeUntyped;
            var length = elementType.MerkleItemLength();
            var start = index * length;
            return (start / SszConstants.BytesPerChunk, start % SszConstants.BytesPerChunk, (start % SszConstants.BytesPerChunk) + length);
        }
        
        if (deconstructedType == typeof(SszContainer<>))
        {
            var schema = (ISszContainerSchema) (typeType.GetField("Schema")!.GetValue(type)!);
            var fieldType = schema.FieldsUntyped[index].FieldType;
            var length = fieldType.MerkleItemLength();
            return (index, 0, length);
        }
        
        throw new Exception("Only lists, vectors, and containers are supported.");
    }

    public static long GetGeneralizedIndex(ISszType type, IEnumerable<int> path)
    {
        var pathEnumerated = path.ToList();
        var root = 1L;
        bool typeIsList = type.IsList() || type is SszBitlist;
        bool typeIsListOrVector = typeIsList || type.IsVector();

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
                typeIsList = false;
                typeIsListOrVector = false;
                root = root * 2 + 1;
            }
            else
            {
                (var position, _, _) = GetItemPosition(type, hop);
                var baseIndex = typeIsList ? 2 : 1;
                root = (root * baseIndex * NextPowerOfTwo(type.ChunkCountUntyped(default!))) + position;
                
                ISszType? elementType = typeIsListOrVector
                    ? (type as ISszCollection)?.MemberTypeUntyped
                    : ((ISszContainerSchema?) type.GetType().GetField("Schema")?.GetValue(type))?.FieldsUntyped[hop].FieldType;
                
                type = elementType ?? throw new Exception("Could not resolve element type");
                typeIsList = type.IsList() || type is SszBitlist;
                typeIsListOrVector = typeIsList || type.IsVector();
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

    public static IEnumerable<long> GetHelperIndices(params int[] indices) =>
        GetHelperIndices(indices.Select(i => (long) i).ToArray());
    public static IEnumerable<long> GetHelperIndices(params long[] indices)
    {
        var indicesEnumerated = indices.ToList();
        var helperIndices = indicesEnumerated.SelectMany(GetBranchIndices).ToHashSet();
        var pathIndices = indicesEnumerated.SelectMany(GetPathIndices).ToHashSet();
        helperIndices.ExceptWith(pathIndices);
        return helperIndices.OrderByDescending(i => i).ToList();
    }

    static string PrintTruncated(this byte[] arr)
    {
        return
            $"{BitConverter.ToString(arr, 0, 2).Replace("-", "").ToLowerInvariant()}:{BitConverter.ToString(arr, arr.Length - 2).Replace("-", "").ToLowerInvariant()}";
    }
    
    public static byte[] CalculateMerkleRoot(byte[] leaf, byte[][] proof, long index)
    {
        if (proof.Length != GetGeneralizedIndexLength(index))
            throw new Exception("Assertion failed");

        var leafCopy = leaf.ToArray();
        var leafSpan = new Span<byte>(leafCopy);
        var proofIndex = index;
        for (int i = 0; i < proof.Length; i++)
        {
            byte[] h = proof[i];
            proofIndex = GeneralizedIndexSibling(proofIndex);
            if (GetGeneralizedIndexBit(index, i))
            {
                Hash(leafSpan, h, leafCopy);
            }
            else
            {
                Hash(leafSpan, leafCopy, h);
            }

            proofIndex = GeneralizedIndexParent(proofIndex);
        }

        return leafCopy;
    }

    public static int Hash(Span<byte> output, byte[] left, byte[] right)
    {
        Span<byte> catted = stackalloc byte[64];
        left.CopyTo(catted);
        right.CopyTo(catted.Slice(32));
        return SHA256.HashData(catted, output);
    }
}