using System.Text.Json.Serialization;
using Solaris.Base.Crypto;

namespace Solaris.Rpc.RpcModels;

public class GPASingleResult
{
    [JsonIgnore] public string Data => Account.Data[0];
    [JsonIgnore] public ReadOnlySpan<byte> DataSpan => Account.DataSpan;
    
    [JsonPropertyName("account")] public required ProgramAccount Account { get; set; }
    [JsonPropertyName("pubkey")] public required string PubKey { get; set; }
}

public class ProgramAccount
{
    [JsonIgnore] public ReadOnlySpan<byte> DataSpan => Convert.FromBase64String(Data[0]);
    [JsonPropertyName("data")] public required string[] Data { get; set; }
    [JsonPropertyName("lamports")] public ulong Lamports { get; set; }
    [JsonPropertyName("space")] public ulong Space { get; set; }
    // other fields are useless, if u need it, ping me in some way, i'll add
}

// REQUEST

[JsonDerivedType(typeof(MemCmpFilter))]
[JsonDerivedType(typeof(DataSizeFilter))]
public interface IGPAFilter;

public class GPAFilters
{
    public static readonly GPAFilters Empty = new();
    
    [JsonPropertyName("filters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<IGPAFilter>? Filters { get; set; } = null;

    [JsonPropertyName("dataSlice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DataSlice? DataSlice { get; set; } = null;

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "base64";

    [JsonPropertyName("commitment")]
    public string Commitment { get; set; } = "processed";
}

public class DataSlice
{
    public DataSlice() { }

    public DataSlice(long offset, long length)
    {
        Length = length;
        Offset = offset;
    }
    
    [JsonPropertyName("length")]
    public long Length { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }
}

public class DataSizeFilter : IGPAFilter
{
    public DataSizeFilter() {}
    public DataSizeFilter(int size)
    {
        DataSize = size;
    }

    [JsonPropertyName("dataSize")]
    public int DataSize { get; set; }
}

public class MemCmpFilter : IGPAFilter
{
    public MemCmpFilter() { }

    public MemCmpFilter(string bytes, int offset)
    {
        MemCmp = new MemCmpFilterItem
        {
            Bytes = bytes,
            Offset = offset
        };
    }

    public MemCmpFilter(ReadOnlySpan<byte> bytes, int offset)
    {
        MemCmp = new MemCmpFilterItem
        {
            Bytes = Base58.EncodeData(bytes),
            Offset = offset
        };
    }

    [JsonPropertyName("memcmp")]
    public MemCmpFilterItem MemCmp { get; set; }
}

public class MemCmpFilterItem
{
    [JsonPropertyName("offset")]
    public required int Offset { get; set; }

    [JsonPropertyName("bytes")]
    public required string Bytes { get; set; }
}