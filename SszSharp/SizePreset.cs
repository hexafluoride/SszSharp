namespace SszSharp;

public class SizePreset
{
    protected bool Equals(SizePreset other)
    {
        return Parameters.Equals(other.Parameters);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((SizePreset) obj);
    }

    public override int GetHashCode()
    {
        return Parameters.GetHashCode();
    }

    public readonly Dictionary<string, long> Parameters;

    public SizePreset(Dictionary<string, long> parameters)
    {
        Parameters = parameters;
    }

    public static SizePreset MainnetPreset = new(new()
    {
        {"MAX_BYTES_PER_TRANSACTION", 1073741824},
        {"MAX_TRANSACTIONS_PER_PAYLOAD", 1048576},
        {"BYTES_PER_LOGS_BLOOM", 256},
        {"MAX_EXTRA_DATA_BYTES", 32},
        {"SYNC_COMMITTEE_SIZE", 512},
        {"EPOCHS_PER_SYNC_COMMITTEE_PERIOD", 256},
        {"VALIDATOR_REGISTRY_LIMIT", 1099511627776},
        {"HISTORICAL_ROOTS_LIMIT", 16777216},
        {"EPOCHS_PER_HISTORICAL_VECTOR", 65536},
        {"EPOCHS_PER_SLASHINGS_VECTOR", 8192},
        {"ETH1_VOTE_DATA_LIMIT", 64 * 32},
        {"JUSTIFICATION_BITS_LENGTH", 4},
        {"SLOTS_PER_HISTORICAL_ROOT", 8192},
        
        {"MAX_PROPOSER_SLASHINGS", 16},
        {"MAX_ATTESTER_SLASHINGS", 2},
        {"MAX_ATTESTATIONS", 128},
        {"MAX_DEPOSITS", 16},
        {"MAX_VOLUNTARY_EXITS", 16},
        {"MAX_VALIDATORS_PER_COMMITTEE", 2048},
        {"DEPOSIT_PROOF_LENGTH", 33}
    });

    public static SizePreset MinimalPreset = new(new()
    {
        {"MAX_BYTES_PER_TRANSACTION", 1073741824},
        {"MAX_TRANSACTIONS_PER_PAYLOAD", 1048576},
        {"BYTES_PER_LOGS_BLOOM", 256},
        {"MAX_EXTRA_DATA_BYTES", 32},
        {"SYNC_COMMITTEE_SIZE", 32},
        {"EPOCHS_PER_SYNC_COMMITTEE_PERIOD", 8},
        {"VALIDATOR_REGISTRY_LIMIT", 1099511627776},
        {"HISTORICAL_ROOTS_LIMIT", 16777216},
        {"EPOCHS_PER_HISTORICAL_VECTOR", 64},
        {"EPOCHS_PER_SLASHINGS_VECTOR", 64},
        {"ETH1_VOTE_DATA_LIMIT", 32},
        {"JUSTIFICATION_BITS_LENGTH", 4},
        {"SLOTS_PER_HISTORICAL_ROOT", 64},
        
        {"MAX_PROPOSER_SLASHINGS", 16},
        {"MAX_ATTESTER_SLASHINGS", 2},
        {"MAX_ATTESTATIONS", 128},
        {"MAX_DEPOSITS", 16},
        {"MAX_VOLUNTARY_EXITS", 16},
        {"MAX_VALIDATORS_PER_COMMITTEE", 2048},
        {"DEPOSIT_PROOF_LENGTH", 33},
    });

    public static SizePreset Empty = new(new ());
    public static SizePreset DefaultPreset = Empty;
}