using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace SszSharp.Tests;

public class AssortedTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public AssortedTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void VectorTest1()
    {
        var intVectorType = new SszVector<SszIntegerWrapper, SszInteger>(new SszInteger(32), 100);
        var elems = Enumerable.Range(1, 100).Select(i => new SszIntegerWrapper(32, (uint)Random.Shared.Next())).ToList();
        TestRoundtrip(intVectorType, elems);
    }

    [Fact]
    public void VectorTest_Nested1()
    {
        var vectorOfIntVectorsType = new SszVector<IEnumerable<SszIntegerWrapper>, SszVector<SszIntegerWrapper, SszInteger>>(memberType: new SszVector<SszIntegerWrapper, SszInteger>(memberType: new SszInteger(bits: 32), capacity: 20), capacity: 4);
        var elems = Enumerable.Range(1, 4).Select(k => Enumerable.Range(1, 20).Select(i => new SszIntegerWrapper(32, (uint)Random.Shared.Next())).ToList()).ToList();
        TestRoundtrip(vectorOfIntVectorsType, elems);
    }
    
    [Fact]
    public void ListTest1()
    {
        var intListType = new SszList<SszIntegerWrapper, SszInteger>(new SszInteger(32), 100);
        var elems = Enumerable.Range(1, 20).Select(i => new SszIntegerWrapper(32, (uint) Random.Shared.Next()))
            .ToList();
        TestRoundtrip(intListType, elems);
    }
    [Fact]
    public void ListTest2()
    {
        var intListType = new SszList<SszIntegerWrapper, SszInteger>(new SszInteger(32), 100);
        var elems = Enumerable.Range(1, 100).Select(i => new SszIntegerWrapper(32, (uint) Random.Shared.Next()))
            .ToList();
        TestRoundtrip(intListType, elems);
    }

    [Fact]
    public void UnionTest()
    {
        var intType1 = new SszInteger(32);
        var intType2 = new SszInteger(64);
        var unionType = new SszUnion(new[] {intType1, intType2});

        var value = new SszUnionWrapper(intType2, new SszIntegerWrapper(64, (ulong)100));
        Span<byte> span = stackalloc byte[100];
        var len = unionType.Serialize(value, span);
        
        PrintBytes(span, len);
        TestRoundtrip(unionType, value);
    }

    [Fact]
    public void BitlistTest()
    {
        var bitlistType = new SszBitlist(100);
        var elems = Enumerable.Range(1, 20).Select(i => Random.Shared.NextDouble() > 0.5).ToList();
        TestRoundtrip(bitlistType, elems);
    }
    [Fact]
    public void BitlistTest2()
    {
        var bitlistType = new SszBitlist(100);
        var elems = Enumerable.Range(1, 100).Select(i => Random.Shared.NextDouble() > 0.5).ToList();
        TestRoundtrip(bitlistType, elems);
    }
    [Fact]
    public void BitvectorTest()
    {
        var bitvectorType = new SszBitvector(100);
        var elems = Enumerable.Range(1, 100).Select(i => Random.Shared.NextDouble() > 0.5).ToList();
        TestRoundtrip(bitvectorType, elems);
    }

    [Fact]
    public void ValidatorsTest()
    {
        var bytes = File.ReadAllBytes("TestData/Validators.ssz");
        var validatorType = SszContainer.GetContainer<ValidatorNodeStruct>(SizePreset.MainnetPreset);
        var validatorsType =
            new SszList<ValidatorNodeStruct, SszContainer<ValidatorNodeStruct>>(validatorType, 1099511627776);
        (var deserialized, var consumed) = validatorsType.Deserialize(bytes);
        var buf = new byte[consumed];
        var span = new Span<byte>(buf);
        var reserializedCount = validatorsType.Serialize(deserialized, span);
        
        Assert.Equal(buf, bytes);
        
        _testOutputHelper.WriteLine(ToPrettyString(Merkleizer.HashTreeRoot(validatorsType, deserialized)));

        var i = 0;
        foreach (var validator in deserialized)
        {
            _testOutputHelper.WriteLine(i++ + ": " + ToPrettyString(Merkleizer.HashTreeRoot(validatorType, validator)));
        }
    }
    
    [Fact]
    public void BeaconStateMinimalTest() =>
        TestRoundtripContainer<BeaconState>("TestData/genesis.ssz", SizePreset.MinimalPreset);
    [Fact]
    public void BeaconStateMainnetTest() =>
        TestRoundtripContainer<BeaconState>("TestData/BeaconState.ssz", SizePreset.MainnetPreset);
    [Fact]
    public void ValidatorNodeStructTest() =>
        TestRoundtripContainer<ValidatorNodeStruct>("TestData/ValidatorNodeStruct.ssz", SizePreset.MainnetPreset);
    [Fact]
    public void ExecutionPayloadTest() =>
        TestRoundtripContainer<ExecutionPayload>("TestData/ExecutionPayload.ssz", SizePreset.MainnetPreset);
    [Fact]
    public void ExecutionPayloadHeaderTest() =>
        TestRoundtripContainer<ExecutionPayloadHeader>("TestData/ExecutionPayloadHeader.ssz", SizePreset.MainnetPreset);
    [Fact]
    public void SyncCommiteeTest() =>
        TestRoundtripContainer<SyncCommittee>("TestData/SyncCommittee.ssz", SizePreset.MainnetPreset);

    void TestRoundtripContainer<TContainer>(string filename, SizePreset? preset) =>
        TestRoundtripContainer<TContainer>(File.ReadAllBytes(filename), preset);
    void TestRoundtripContainer<TContainer>(byte[] bytes, SizePreset? preset)
    {
        var containerType = SszContainer.GetContainer<TContainer>(preset);
        (TContainer deserialized, var consumedBytes) = containerType.Deserialize(bytes);
        
        Assert.Equal(bytes.Length, consumedBytes);
        
        var buf = new byte[consumedBytes];
        var reserializedBytes = containerType.Serialize(deserialized, buf);
        (var deserializedAgain, var deserializedBytes) = containerType.Deserialize(buf); 
        
        Assert.Equal(reserializedBytes, consumedBytes);
        Assert.Equal(reserializedBytes, deserializedBytes);
        Assert.Equal(bytes, buf);
        Assert.True(RecursiveEqualityCheck(containerType, deserialized, deserializedAgain));
    }
    
    void TestRoundtrip<T>(ISszType<T> sszType, T value, int bufSize = 65536)
    {
        var buf = new byte[bufSize];
        var span = new Span<byte>(buf);
        var writtenBytes = sszType.Serialize(value, span);
        (var deserialized, var consumedBytes) = sszType.Deserialize(span.Slice(0, writtenBytes));
        
        PrintBytes(span.Slice(0, writtenBytes));
        Assert.Equal(writtenBytes, consumedBytes);
        Assert.True(RecursiveEqualityCheck(sszType, deserialized, value));
    }

    bool ContainerEqualityCheck(ISszType type, object a, object b)
    {
        if (!type.IsContainer())
            return false;
        
        var representativeType = type.RepresentativeType;
        var aType = a.GetType();
        var bType = b.GetType();

        if (aType != representativeType || bType != representativeType)
            return false;

        var schema = type.GetSchema();
        var fields = schema.FieldsUntyped;

        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var aValue = schema.GetUntyped(a, i);
            var bValue = schema.GetUntyped(b, i);

            if (!RecursiveEqualityCheck(field.FieldType, aValue, bValue))
                return false;
        }

        return true;
    }
    
    bool RecursiveEqualityCheck(ISszType type, object a, object b)
    {
        if (type.IsContainer())
        {
            return ContainerEqualityCheck(type, a, b);
        }
        
        if (type is ISszCollection || type is SszBitvector || type is SszBitlist)
        {
            var aArray = a.GetGenericEnumerable().ToArray();
            var bArray = b.GetGenericEnumerable().ToArray();
            
            var collectionMemberType = type switch
            {
                ISszCollection collectionType => collectionType.MemberTypeUntyped,
                SszBitvector => new SszBoolean(),
                SszBitlist => new SszBoolean(),
                _ => throw new Exception("Could not obtain member type")
            };

            if (aArray.Length != bArray.Length)
            {
                return false;
            }

            for (int i = 0; i < aArray.Length; i++)
            {
                if (!RecursiveEqualityCheck(collectionMemberType, aArray[i], bArray[i]))
                    return false;
            }

            return true;
        }
        
        return a.Equals(b);
    }

    void PrintBytes(ReadOnlySpan<byte> buf, int length = -1)
    {
        if (length == -1)
            length = buf.Length;
        _testOutputHelper.WriteLine($"Serialized to {length} bytes");
        for (int i = 0; i < length; i += 16)
        {
            var range = buf.Slice(i, Math.Min(16, length - i));
            _testOutputHelper.WriteLine(string.Join(' ', range.ToArray().Select(b => $"{b:X2}")));
        }
    }
    
    string ToPrettyString(byte[] arr)
    {
        return BitConverter.ToString(arr).Replace("-", "").ToLower();
    }
    
    byte[] GenerateRandomBytes(int len)
    {
        var arr = new byte[len];
        Random.Shared.NextBytes(arr);
        return arr;
    }
}