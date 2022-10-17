namespace SszSharp;

public class Fork
{
    [SszElement(0, "Vector[uint8, 4]")] public byte[] PreviousVersion { get; set; }
    [SszElement(1, "Vector[uint8, 4]")] public byte[] CurrentVersion { get; set; }
    [SszElement(2, "uint64")] public ulong Epoch { get; set; }
}

public class BeaconBlockHeader
{
    [SszElement(0, "uint64")] public ulong Slot { get; set; }
    [SszElement(1, "uint64")] public ulong ValidatorIndex { get; set; }
    [SszElement(2, "Vector[uint8, 32]")] public byte[] ParentRoot { get; set; }
    [SszElement(3, "Vector[uint8, 32]")] public byte[] StateRoot { get; set; }
    [SszElement(4, "Vector[uint8, 32]")] public byte[] BodyRoot { get; set; }
}

public class Eth1Data
{
    [SszElement(0, "Vector[uint8, 32]")] public byte[] DepositRoot { get; set; }
    [SszElement(1, "uint64")] public ulong DepositCount { get; set; }
    [SszElement(2, "Vector[uint8, 32]")] public byte[] BlockHash { get; set; }
}

public class ValidatorNodeStruct
{
    [SszElement(0, "Vector[uint8, 48]")] public byte[] Pubkey { get; set; }
    [SszElement(1, "Vector[uint8, 32]")] public byte[] WithdrawalCredentials { get; set; }
    [SszElement(2, "uint64")] public ulong EffectiveBalance { get; set; }
    [SszElement(3, "boolean")] public bool Slashed { get; set; }
    [SszElement(4, "uint64")] public ulong ActivationEligibilityEpoch { get; set; }
    [SszElement(5, "uint64")] public ulong ActivationEpoch { get; set; }
    [SszElement(6, "uint64")] public ulong ExitEpoch { get; set; }
    [SszElement(7, "uint64")] public ulong WithdrawableEpoch { get; set; }
}

public class Checkpoint
{
    [SszElement(0, "uint64")] public ulong Epoch { get; set; }
    [SszElement(1, "Vector[uint8, 32]")] public byte[] Root { get; set; }
}

public class SyncCommittee
{
    [SszElement(0, "Vector[Vector[uint8,48], SYNC_COMMITTEE_SIZE]")] public byte[][] Pubkeys { get; set; }
    [SszElement(1, "Vector[uint8,48]")] public byte[] AggregatePubkey { get; set; }
}

public class BeaconState
{
    [SszElement(0, "uint64")] public ulong GenesisTime { get; set; }
    [SszElement(1, "Vector[uint8, 32]")] public byte[] GenesisValidatorsRoot { get; set; }
    [SszElement(2, "uint64")] public ulong Slot { get; set; }
    [SszElement(3, "Container")] public Fork Fork { get; set; }
    [SszElement(4, "Container")] public BeaconBlockHeader LatestBlockHeader { get; set; }
    [SszElement(5, "Vector[Vector[uint8, 32], SLOTS_PER_HISTORICAL_ROOT]")] public byte[][] BlockRoots { get; set; }
    [SszElement(6, "Vector[Vector[uint8, 32], SLOTS_PER_HISTORICAL_ROOT]")] public byte[][] StateRoots { get; set; }
    [SszElement(7, "List[Vector[uint8, 32], HISTORICAL_ROOTS_LIMIT]")] public byte[][] HistoricalRoots { get; set; }
    [SszElement(8, "Container")] public Eth1Data Eth1Data { get; set; }
    [SszElement(9, "List[Container[SszSharp.Eth1Data], ETH1_VOTE_DATA_LIMIT]")] public List<Eth1Data> Eth1DataVotes { get; set; }
    [SszElement(10, "uint64")] public ulong Eth1DepositIndex { get; set; }
    [SszElement(11, "List[Container[SszSharp.ValidatorNodeStruct], VALIDATOR_REGISTRY_LIMIT]")] public List<ValidatorNodeStruct> Validators { get; set; }
    [SszElement(12, "List[uint64, VALIDATOR_REGISTRY_LIMIT]")] public List<ulong> Balances { get; set; }
    [SszElement(13, "Vector[Vector[uint8, 32], EPOCHS_PER_HISTORICAL_VECTOR]")] public byte[][] RandaoMixes { get; set; }
    [SszElement(14, "Vector[uint64, EPOCHS_PER_SLASHINGS_VECTOR]")] public ulong[] Slashings { get; set; }
    [SszElement(15, "List[uint8, VALIDATOR_REGISTRY_LIMIT]")] public byte[] PreviousEpochParticipation { get; set; }
    [SszElement(16, "List[uint8, VALIDATOR_REGISTRY_LIMIT]")] public byte[] CurrentEpochParticipation { get; set; }
    [SszElement(17, "Bitvector[JUSTIFICATION_BITS_LENGTH]")] public bool[] JustificationBits { get; set; }
    [SszElement(18, "Container")] public Checkpoint PreviousJustifiedCheckpoint { get; set; }
    [SszElement(19, "Container")] public Checkpoint CurrentJustifiedCheckpoint { get; set; }
    [SszElement(20, "Container")] public Checkpoint FinalizedCheckpoint { get; set; }
    [SszElement(21, "List[uint64, VALIDATOR_REGISTRY_LIMIT]")] public ulong[] InactivityScores { get; set; }
    [SszElement(22, "Container")] public SyncCommittee CurrentSyncCommittee { get; set; }
    [SszElement(23, "Container")] public SyncCommittee NextSyncCommittee { get; set; }
    [SszElement(24, "Container")] public ExecutionPayloadHeader LastExecutionPayloadHeader { get; set; }
}