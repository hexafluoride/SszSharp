using System.Numerics;
using System.Reflection;
using System.Xml;

namespace SszSharp;

public class ExecutionPayload
{
    protected bool Equals(ExecutionPayload other)
    {
        return Root.SequenceEqual(other.Root) && FeeRecipient.SequenceEqual(other.FeeRecipient) &&
               StateRoot.SequenceEqual(other.StateRoot) && ReceiptsRoot.SequenceEqual(other.ReceiptsRoot) &&
               LogsBloom.SequenceEqual(other.LogsBloom) && PrevRandao.SequenceEqual(other.PrevRandao) &&
               BlockNumber == other.BlockNumber && GasLimit == other.GasLimit && GasUsed == other.GasUsed &&
               Timestamp == other.Timestamp && ExtraData.SequenceEqual(other.ExtraData) &&
               BaseFeePerGas.Equals(other.BaseFeePerGas) && BlockHash.SequenceEqual(other.BlockHash) &&
               Transactions.Count == other.Transactions.Count &&
               Transactions.Zip(other.Transactions).All(p => p.First.SequenceEqual(p.Second));
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ExecutionPayload) obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Root);
        hashCode.Add(FeeRecipient);
        hashCode.Add(StateRoot);
        hashCode.Add(ReceiptsRoot);
        hashCode.Add(LogsBloom);
        hashCode.Add(PrevRandao);
        hashCode.Add(BlockNumber);
        hashCode.Add(GasLimit);
        hashCode.Add(GasUsed);
        hashCode.Add(Timestamp);
        hashCode.Add(ExtraData);
        hashCode.Add(BaseFeePerGas);
        hashCode.Add(BlockHash);
        hashCode.Add(Transactions);
        return hashCode.ToHashCode();
    }

    [SszElement(0, "Vector[uint8, 32]")]
    public byte[] Root { get; set; }
    [SszElement(1, "Vector[uint8, 20]")]
    public byte[] FeeRecipient { get; set; }
    [SszElement(2, "Vector[uint8, 32]")]
    public byte[] StateRoot { get; set; }
    [SszElement(3, "Vector[uint8, 32]")]
    public byte[] ReceiptsRoot { get; set; }
    [SszElement(4, "Vector[uint8, 256]")]
    public byte[] LogsBloom { get; set; }
    [SszElement(5, "Vector[uint8, 32]")]
    public byte[] PrevRandao { get; set; }
    [SszElement(6, "uint64")]
    public ulong BlockNumber { get; set; }
    [SszElement(7, "uint64")]
    public ulong GasLimit { get; set; }
    [SszElement(8, "uint64")]
    public ulong GasUsed { get; set; }
    [SszElement(9, "uint64")]
    public ulong Timestamp { get; set; }
    [SszElement(10, "List[uint8, 32]")]
    public byte[] ExtraData { get; set; }
    [SszElement(11, "uint256")]
    public BigInteger BaseFeePerGas { get; set; }
    [SszElement(12, "Vector[uint8, 32]")]
    public byte[] BlockHash { get; set; }
    [SszElement(13, "List[List[uint8, 1073741824], 1048576]")]
    public List<byte[]> Transactions { get; set; }
}

public class ExecutionPayloadHeader
{
    protected bool Equals(ExecutionPayloadHeader other)
    {
        return Root.SequenceEqual(other.Root) && FeeRecipient.SequenceEqual(other.FeeRecipient) &&
               StateRoot.SequenceEqual(other.StateRoot) && ReceiptsRoot.SequenceEqual(other.ReceiptsRoot) &&
               LogsBloom.SequenceEqual(other.LogsBloom) && PrevRandao.SequenceEqual(other.PrevRandao) &&
               BlockNumber == other.BlockNumber && GasLimit == other.GasLimit && GasUsed == other.GasUsed &&
               Timestamp == other.Timestamp && ExtraData.SequenceEqual(other.ExtraData) &&
               BaseFeePerGas.Equals(other.BaseFeePerGas) && BlockHash.SequenceEqual(other.BlockHash) &&
               TransactionsRoot.SequenceEqual(other.TransactionsRoot);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ExecutionPayloadHeader) obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Root);
        hashCode.Add(FeeRecipient);
        hashCode.Add(StateRoot);
        hashCode.Add(ReceiptsRoot);
        hashCode.Add(LogsBloom);
        hashCode.Add(PrevRandao);
        hashCode.Add(BlockNumber);
        hashCode.Add(GasLimit);
        hashCode.Add(GasUsed);
        hashCode.Add(Timestamp);
        hashCode.Add(ExtraData);
        hashCode.Add(BaseFeePerGas);
        hashCode.Add(BlockHash);
        hashCode.Add(TransactionsRoot);
        return hashCode.ToHashCode();
    }

    [SszElement(0, "Vector[uint8, 32]")]
    public byte[] Root { get; set; }
    [SszElement(1, "Vector[uint8, 20]")]
    public byte[] FeeRecipient { get; set; }
    [SszElement(2, "Vector[uint8, 32]")]
    public byte[] StateRoot { get; set; }
    [SszElement(3, "Vector[uint8, 32]")]
    public byte[] ReceiptsRoot { get; set; }
    [SszElement(4, "Vector[uint8, 256]")]
    public byte[] LogsBloom { get; set; }
    [SszElement(5, "Vector[uint8, 32]")]
    public byte[] PrevRandao { get; set; }
    [SszElement(6, "uint64")]
    public ulong BlockNumber { get; set; }
    [SszElement(7, "uint64")]
    public ulong GasLimit { get; set; }
    [SszElement(8, "uint64")]
    public ulong GasUsed { get; set; }
    [SszElement(9, "uint64")]
    public ulong Timestamp { get; set; }
    [SszElement(10, "List[uint8, 32]")]
    public byte[] ExtraData { get; set; }
    [SszElement(11, "uint256")]
    public BigInteger BaseFeePerGas { get; set; }
    [SszElement(12, "Vector[uint8, 32]")]
    public byte[] BlockHash { get; set; }
    [SszElement(13, "Vector[uint8, 32]")]
    public byte[] TransactionsRoot { get; set; }
}