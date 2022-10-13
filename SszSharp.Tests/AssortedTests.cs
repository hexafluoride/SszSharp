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
        var vectorOfIntVectorsType = new SszVector<IEnumerable<SszIntegerWrapper>, SszVector<SszIntegerWrapper, SszInteger>>(memberType: new SszVector<SszIntegerWrapper, SszInteger>(memberType: new SszInteger(bits: 32), count: 20), count: 4);
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
    public void ContainerTest()
    {
        var executionPayloadSchema =
            SszSchemaGenerator.GetSchema(() => new ExecutionPayload());
        var executionPayloadType = new SszContainer<ExecutionPayload>(executionPayloadSchema);
        var payload = new ExecutionPayload()
        {
            Root = GenerateRandomBytes(32),
            BaseFeePerGas = 1000,
            BlockHash = GenerateRandomBytes(32),
            BlockNumber = 1003000,
            ExtraData = GenerateRandomBytes(32),
            FeeRecipient = GenerateRandomBytes(20),
            GasLimit = 10000000,
            GasUsed = 5555,
            LogsBloom = GenerateRandomBytes(256),
            PrevRandao = GenerateRandomBytes(32),
            Timestamp = 193484832,
            ReceiptsRoot = GenerateRandomBytes(32),
            StateRoot = GenerateRandomBytes(32),
            Transactions = new []
            {
                GenerateRandomBytes(100),
                GenerateRandomBytes(200),
                GenerateRandomBytes(10),
            }
        };
        
        TestRoundtrip(executionPayloadType, payload);
    }

    byte[] GenerateRandomBytes(int len)
    {
        var arr = new byte[len];
        Random.Shared.NextBytes(arr);
        return arr;
    }

    [Fact]
    public void ExecutionPayloadDeserialize()
    {
        var bytesStr =
            "0000000000bb0000000000000000000000000000000000000000000000000000000000000000cc0000000000000000000000000000000000000000000dd00000000000000000000000000000000000000000000000000000000000ee0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000ff000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000110000000000000000000000000000000000000000000000000d204000000000000d8100000000000005704000000000000faaa19cc00000000fc010000672b0000000000000000000000000000000000000000000000000000000000000000fffff0000000000000000000000000000000000000000000000000000000fc010000";
        var bytes = new byte[bytesStr.Length / 2];

        for (int i = 0; i < bytesStr.Length; i += 2)
        {
            bytes[i / 2] = byte.Parse(bytesStr.Substring(i, 2), NumberStyles.HexNumber);
        }
        
        
        var executionPayloadSchema =
            SszSchemaGenerator.GetSchema(() => new ExecutionPayload());
        var executionPayloadType = new SszContainer<ExecutionPayload>(executionPayloadSchema);
        (var deserializedPayload, var consumed) = executionPayloadType.Deserialize(bytes);

        Debugger.Break();
    }

    [Fact]
    public void BeaconStateTest1()
    {
        var beaconState = new BeaconState()
        {
            BlockRoots = Enumerable.Repeat(1, 64).Select(_ => GenerateRandomBytes(32)).ToArray(),
            StateRoots = Enumerable.Repeat(1, 64).Select(_ => GenerateRandomBytes(32)).ToArray(),
            Eth1Data = new Eth1Data()
            {
                BlockHash = GenerateRandomBytes(32),
                DepositCount = 10,
                DepositRoot = GenerateRandomBytes(32)
            },
            Eth1DataVotes = new List<Eth1Data>(),
            Fork = new Fork()
            {
                CurrentVersion = GenerateRandomBytes(4),
                PreviousVersion = GenerateRandomBytes(4),
                Epoch = 10
            },
            GenesisTime = 10000,
            GenesisValidatorsRoot = GenerateRandomBytes(32),
            HistoricalRoots = Enumerable.Repeat(1, 10).Select(_ => GenerateRandomBytes(32)).ToArray(),
            LatestBlockHeader = new BeaconBlockHeader()
            {
                BodyRoot = GenerateRandomBytes(32),
                ParentRoot = GenerateRandomBytes(32),
                Slot = 20,
                StateRoot = GenerateRandomBytes(32),
                ValidatorIndex = 10
            },
            Slot = 20
        };
        //throw new Exception();

        var beaconStateType = new SszContainer<BeaconState>(SszSchemaGenerator.GetSchema(() => new BeaconState()));
        
        //TestRoundtrip(beaconStateType, beaconState, bufSize: 1000000);
    }

    [Fact]
    public void BeaconStateTest2()
    {
        var beaconStateBytes = File.ReadAllBytes("/home/kate/repos/lodestar/genesis.ssz");
        var beaconStateType = new SszContainer<BeaconState>(SszSchemaGenerator.GetSchema(() => new BeaconState()));
        (var beaconState, var consumed) = beaconStateType.Deserialize(beaconStateBytes);
        
        //Debugger.Break();
        var buf = new byte[consumed];
        var reserialized = beaconStateType.Serialize(beaconState, new Span<byte>(buf));
        
        Assert.Equal(buf, beaconStateBytes);
    }

    void TestRoundtrip<T>(ISszType<T> sszType, T value, int bufSize = 65536)
    {
        var buf = new byte[bufSize];
        var span = new Span<byte>(buf);
        var writtenBytes = sszType.Serialize(value, span);
        (var deserialized, var consumedBytes) = sszType.Deserialize(span.Slice(0, writtenBytes));
        
        PrintBytes(span.Slice(0, writtenBytes));
        Assert.Equal(writtenBytes, consumedBytes);
        Assert.True(RecursiveEqualityCheck(deserialized, value));
    }

    bool RecursiveEqualityCheck(object a, object b)
    {
        var aInterfaces = a.GetType().GetInterfaces();
        var bInterfaces = b.GetType().GetInterfaces();

        var aEnumerableInterface = aInterfaces.FirstOrDefault(iface => iface.FullName.StartsWith("System.Collections.Generic.IEnumerable"));
        var bEnumerableInterface = bInterfaces.FirstOrDefault(iface => iface.FullName.StartsWith("System.Collections.Generic.IEnumerable"));

        if (aEnumerableInterface is not null && bEnumerableInterface is not null)
        {
            var aArray = (Array) (typeof(Enumerable).GetMethods().Where(m => m.Name == "ToArray").First()
                .MakeGenericMethod(aEnumerableInterface.GenericTypeArguments[0]).Invoke(null, new[] {a}));
            var bArray = (Array) (typeof(Enumerable).GetMethods().Where(m => m.Name == "ToArray").First()
                .MakeGenericMethod(bEnumerableInterface.GenericTypeArguments[0]).Invoke(null, new[] {b}));

            if (aArray.Length != bArray.Length)
            {
                return false;
            }
            
            for (int i = 0; i < aArray.Length; i++)
            {
                if (!RecursiveEqualityCheck(aArray.GetValue(i), bArray.GetValue(i)))
                    return false;
            }

            return true;
        }

        return a.Equals(b);
    }

    void PrintBytes(Span<byte> buf, int length = -1)
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
}