using System.IO;
using System.Text;
using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Programs.Token2022;

internal static class Token2022ExtensionData
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static BorshDeserializer Read(Token2022ExtensionType type, ReadOnlyMemory<byte> data)
    {
        Validate(type, data.Span);
        return new BorshDeserializer(data.Span);
    }

    internal static PublicKey? OptionalKey(ref BorshDeserializer des)
    {
        var bytes = des.Span(32);
        return bytes.IndexOfAnyExcept((byte)0) < 0 ? null : new PublicKey(bytes);
    }

    internal static ReadOnlyMemory<byte> Bytes(ref BorshDeserializer des, ReadOnlyMemory<byte> data, int length)
    {
        var offset = des.Offset;
        des.Span(length);
        return data.Slice(offset, length);
    }

    internal static ReadOnlyMemory<byte>? OptionalBytes(ref BorshDeserializer des, ReadOnlyMemory<byte> data, int length)
    {
        var bytes = Bytes(ref des, data, length);
        return bytes.Span.IndexOfAnyExcept((byte)0) < 0 ? null : bytes;
    }

    internal static string String(ref BorshDeserializer des) => Utf8.GetString(StringBytes(ref des));

    private static ReadOnlySpan<byte> StringBytes(ref BorshDeserializer des)
    {
        if (des.Remaining < 4) throw new InvalidDataException("Truncated metadata string length.");
        var length = des.Integer<uint>();
        if (length > (uint)des.Remaining) throw new InvalidDataException("Truncated metadata string.");
        return des.Span((int)length);
    }

    internal static int AdditionalMetadataCount(ref BorshDeserializer des)
    {
        if (des.Remaining < 4) throw new InvalidDataException("Truncated additional metadata count.");
        var count = des.Integer<uint>();
        if (count > (uint)(des.Remaining / 8)) throw new InvalidDataException("Truncated additional metadata.");
        return (int)count;
    }

    internal static void Validate(Token2022ExtensionType type, ReadOnlySpan<byte> data)
    {
        if (type == Token2022ExtensionType.TokenMetadata)
        {
            if (data.Length < 80) throw new InvalidDataException("Truncated token metadata.");
            var metadata = new BorshDeserializer(data);
            metadata.Skip(64);
            ValidateString(ref metadata);
            ValidateString(ref metadata);
            ValidateString(ref metadata);
            var count = AdditionalMetadataCount(ref metadata);
            for (var i = 0; i < count; i++)
            {
                ValidateString(ref metadata);
                ValidateString(ref metadata);
            }
            if (metadata.Remaining != 0) throw new InvalidDataException("Trailing token metadata payload.");
            return;
        }
        if (data.Length != Token2022ExtensionSizes.GetPayloadSize(type)) throw new InvalidDataException("Invalid extension payload length.");
        switch (type)
        {
            case Token2022ExtensionType.ConfidentialTransferMint: ValidateBoolean(data, 32); break;
            case Token2022ExtensionType.ConfidentialTransferAccount:
                ValidateBoolean(data, 0);
                ValidateBoolean(data, 261);
                ValidateBoolean(data, 262);
                break;
            case Token2022ExtensionType.DefaultAccountState:
                if (data[0] > 2) throw new InvalidDataException("Invalid default account state.");
                break;
            case Token2022ExtensionType.MemoTransfer:
            case Token2022ExtensionType.CpiGuard:
            case Token2022ExtensionType.TransferHookAccount: ValidateBoolean(data, 0); break;
            case Token2022ExtensionType.ConfidentialTransferFeeConfig: ValidateBoolean(data, 64); break;
            case Token2022ExtensionType.Pausable: ValidateBoolean(data, 32); break;
            case Token2022ExtensionType.TransferFeeConfig:
                var des = new BorshDeserializer(data);
                des.Skip(88);
                if (des.Integer<ushort>() > 10000) throw new InvalidDataException("Invalid transfer fee basis points.");
                des.Skip(16);
                if (des.Integer<ushort>() > 10000) throw new InvalidDataException("Invalid transfer fee basis points.");
                break;
        }
    }

    private static void ValidateString(ref BorshDeserializer des)
    {
        try { _ = Utf8.GetCharCount(StringBytes(ref des)); }
        catch (DecoderFallbackException exception) { throw new InvalidDataException("Invalid metadata UTF-8.", exception); }
    }

    private static void ValidateBoolean(ReadOnlySpan<byte> data, int offset)
    {
        if (data[offset] > 1) throw new InvalidDataException("Invalid extension boolean.");
    }
}

public readonly record struct Token2022TransferFee(ulong Epoch, ulong MaximumFee, ushort TransferFeeBasisPoints) : IBorshDeserializable<Token2022TransferFee>
{
    public static Token2022TransferFee Deserialize(ref BorshDeserializer des)
    {
        var result = new Token2022TransferFee(des.Integer<ulong>(), des.Integer<ulong>(), des.Integer<ushort>());
        if (result.TransferFeeBasisPoints > 10000) throw new InvalidDataException("Invalid transfer fee basis points.");
        return result;
    }

    public ulong CalculateFee(ulong preFeeAmount)
    {
        ValidateRate();
        var fee = ((UInt128)preFeeAmount * TransferFeeBasisPoints + 9999) / 10000;
        return (ulong)UInt128.Min(fee, MaximumFee);
    }

    public ulong CalculatePostFeeAmount(ulong preFeeAmount) => preFeeAmount - CalculateFee(preFeeAmount);

    public ulong? CalculatePreFeeAmount(ulong postFeeAmount)
    {
        ValidateRate();
        if (TransferFeeBasisPoints == 0 || postFeeAmount == 0) return postFeeAmount;
        if (TransferFeeBasisPoints == 10000)
            return (UInt128)postFeeAmount + MaximumFee > ulong.MaxValue ? null : postFeeAmount + MaximumFee;
        var denominator = (uint)(10000 - TransferFeeBasisPoints);
        var raw = ((UInt128)postFeeAmount * 10000 + denominator - 1) / denominator;
        var preFeeAmount = raw - postFeeAmount >= MaximumFee ? (UInt128)postFeeAmount + MaximumFee : raw;
        return preFeeAmount > ulong.MaxValue ? null : (ulong)preFeeAmount;
    }

    public ulong? CalculateInverseFee(ulong postFeeAmount)
    {
        var amount = CalculatePreFeeAmount(postFeeAmount);
        return amount.HasValue ? CalculateFee(amount.Value) : null;
    }

    private void ValidateRate()
    {
        if (TransferFeeBasisPoints > 10000) throw new InvalidOperationException("Transfer fee basis points exceed 10000.");
    }
}

public readonly record struct Token2022TransferFeeConfig(PublicKey? TransferFeeConfigAuthority, PublicKey? WithdrawWithheldAuthority,
    ulong WithheldAmount, Token2022TransferFee OlderTransferFee, Token2022TransferFee NewerTransferFee) : IToken2022Extension<Token2022TransferFeeConfig>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TransferFeeConfig;
    public static Token2022TransferFeeConfig Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), Token2022ExtensionData.OptionalKey(ref des), des.Integer<ulong>(),
            des.Deserialize<Token2022TransferFee>(), des.Deserialize<Token2022TransferFee>());
    }
    public Token2022TransferFee GetEpochFee(ulong epoch) => epoch >= NewerTransferFee.Epoch ? NewerTransferFee : OlderTransferFee;
    public ulong CalculateEpochFee(ulong epoch, ulong preFeeAmount) => GetEpochFee(epoch).CalculateFee(preFeeAmount);
    public ulong? CalculateInverseEpochFee(ulong epoch, ulong postFeeAmount) => GetEpochFee(epoch).CalculateInverseFee(postFeeAmount);
}

public readonly record struct Token2022TransferFeeAmount(ulong WithheldAmount) : IToken2022Extension<Token2022TransferFeeAmount>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TransferFeeAmount;
    public static Token2022TransferFeeAmount Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.Integer<ulong>());
    }
}

public readonly record struct Token2022MintCloseAuthority(PublicKey? CloseAuthority) : IToken2022Extension<Token2022MintCloseAuthority>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.MintCloseAuthority;
    public static Token2022MintCloseAuthority Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des));
    }
}

public readonly record struct Token2022ConfidentialTransferMint(PublicKey? Authority, bool AutoApproveNewAccounts,
    ReadOnlyMemory<byte>? AuditorElgamalPublicKey) : IToken2022Extension<Token2022ConfidentialTransferMint>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ConfidentialTransferMint;
    public static Token2022ConfidentialTransferMint Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), des.Bool(), Token2022ExtensionData.OptionalBytes(ref des, data, 32));
    }
}

public readonly record struct Token2022ConfidentialTransferAccount(bool Approved, ReadOnlyMemory<byte> ElgamalPublicKey,
    ReadOnlyMemory<byte> PendingBalanceLow, ReadOnlyMemory<byte> PendingBalanceHigh, ReadOnlyMemory<byte> AvailableBalance,
    ReadOnlyMemory<byte> DecryptableAvailableBalance, bool AllowConfidentialCredits, bool AllowNonConfidentialCredits,
    ulong PendingBalanceCreditCounter, ulong MaximumPendingBalanceCreditCounter, ulong ExpectedPendingBalanceCreditCounter,
    ulong ActualPendingBalanceCreditCounter) : IToken2022Extension<Token2022ConfidentialTransferAccount>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ConfidentialTransferAccount;
    public static Token2022ConfidentialTransferAccount Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.Bool(), Token2022ExtensionData.Bytes(ref des, data, 32), Token2022ExtensionData.Bytes(ref des, data, 64),
            Token2022ExtensionData.Bytes(ref des, data, 64), Token2022ExtensionData.Bytes(ref des, data, 64),
            Token2022ExtensionData.Bytes(ref des, data, 36), des.Bool(), des.Bool(), des.Integer<ulong>(), des.Integer<ulong>(),
            des.Integer<ulong>(), des.Integer<ulong>());
    }
}

public readonly record struct Token2022DefaultAccountState(Token2022AccountState State) : IToken2022Extension<Token2022DefaultAccountState>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.DefaultAccountState;
    public static Token2022DefaultAccountState Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.Integer<Token2022AccountState>());
    }
}

public readonly record struct Token2022ImmutableOwner : IToken2022Extension<Token2022ImmutableOwner>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ImmutableOwner;
    public static Token2022ImmutableOwner Decode(ReadOnlyMemory<byte> data) { Token2022ExtensionData.Validate(Type, data.Span); return new(); }
}

public readonly record struct Token2022MemoTransfer(bool RequireIncomingTransferMemos) : IToken2022Extension<Token2022MemoTransfer>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.MemoTransfer;
    public static Token2022MemoTransfer Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.Bool());
    }
}

public readonly record struct Token2022NonTransferable : IToken2022Extension<Token2022NonTransferable>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.NonTransferable;
    public static Token2022NonTransferable Decode(ReadOnlyMemory<byte> data) { Token2022ExtensionData.Validate(Type, data.Span); return new(); }
}

public readonly record struct Token2022InterestBearingConfig(PublicKey? RateAuthority, long InitializationTimestamp,
    short PreUpdateAverageRate, long LastUpdateTimestamp, short CurrentRate) : IToken2022Extension<Token2022InterestBearingConfig>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.InterestBearingConfig;
    public static Token2022InterestBearingConfig Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), des.Integer<long>(), des.Integer<short>(), des.Integer<long>(), des.Integer<short>());
    }
}

public readonly record struct Token2022CpiGuard(bool LockCpi) : IToken2022Extension<Token2022CpiGuard>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.CpiGuard;
    public static Token2022CpiGuard Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.Bool());
    }
}

public readonly record struct Token2022PermanentDelegate(PublicKey? Delegate) : IToken2022Extension<Token2022PermanentDelegate>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.PermanentDelegate;
    public static Token2022PermanentDelegate Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des));
    }
}

public readonly record struct Token2022NonTransferableAccount : IToken2022Extension<Token2022NonTransferableAccount>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.NonTransferableAccount;
    public static Token2022NonTransferableAccount Decode(ReadOnlyMemory<byte> data) { Token2022ExtensionData.Validate(Type, data.Span); return new(); }
}

public readonly record struct Token2022TransferHook(PublicKey? Authority, PublicKey? ProgramId) : IToken2022Extension<Token2022TransferHook>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TransferHook;
    public static Token2022TransferHook Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), Token2022ExtensionData.OptionalKey(ref des));
    }
}

public readonly record struct Token2022TransferHookAccount(bool Transferring) : IToken2022Extension<Token2022TransferHookAccount>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TransferHookAccount;
    public static Token2022TransferHookAccount Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.Bool());
    }
}

public readonly record struct Token2022ConfidentialTransferFeeConfig(PublicKey? Authority, ReadOnlyMemory<byte> WithdrawWithheldAuthorityElgamalPublicKey,
    bool HarvestToMintEnabled, ReadOnlyMemory<byte> WithheldAmount) : IToken2022Extension<Token2022ConfidentialTransferFeeConfig>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ConfidentialTransferFeeConfig;
    public static Token2022ConfidentialTransferFeeConfig Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), Token2022ExtensionData.Bytes(ref des, data, 32), des.Bool(), Token2022ExtensionData.Bytes(ref des, data, 64));
    }
}

public readonly record struct Token2022ConfidentialTransferFeeAmount(ReadOnlyMemory<byte> WithheldAmount) : IToken2022Extension<Token2022ConfidentialTransferFeeAmount>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ConfidentialTransferFeeAmount;
    public static Token2022ConfidentialTransferFeeAmount Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.Bytes(ref des, data, 64));
    }
}

public readonly record struct Token2022MetadataPointer(PublicKey? Authority, PublicKey? MetadataAddress) : IToken2022Extension<Token2022MetadataPointer>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.MetadataPointer;
    public static Token2022MetadataPointer Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), Token2022ExtensionData.OptionalKey(ref des));
    }
}

public readonly record struct Token2022MetadataEntry(string Key, string Value);

public readonly record struct Token2022TokenMetadata(PublicKey? UpdateAuthority, PublicKey Mint, string Name, string Symbol,
    string Uri, ReadOnlyMemory<Token2022MetadataEntry> AdditionalMetadata) : IToken2022Extension<Token2022TokenMetadata>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TokenMetadata;
    public static Token2022TokenMetadata Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        var authority = Token2022ExtensionData.OptionalKey(ref des);
        var mint = des.PublicKey();
        var name = Token2022ExtensionData.String(ref des);
        var symbol = Token2022ExtensionData.String(ref des);
        var uri = Token2022ExtensionData.String(ref des);
        var count = Token2022ExtensionData.AdditionalMetadataCount(ref des);
        var additional = count == 0 ? Array.Empty<Token2022MetadataEntry>() : new Token2022MetadataEntry[count];
        for (var i = 0; i < count; i++) additional[i] = new(Token2022ExtensionData.String(ref des), Token2022ExtensionData.String(ref des));
        return new(authority, mint, name, symbol, uri, additional);
    }

    public int GetPayloadSize()
    {
        var length = checked(80 + Encoding.UTF8.GetByteCount(Name) + Encoding.UTF8.GetByteCount(Symbol) + Encoding.UTF8.GetByteCount(Uri));
        foreach (var field in AdditionalMetadata.Span)
            length = checked(length + 8 + Encoding.UTF8.GetByteCount(field.Key) + Encoding.UTF8.GetByteCount(field.Value));
        return length;
    }
}

public readonly record struct Token2022GroupPointer(PublicKey? Authority, PublicKey? GroupAddress) : IToken2022Extension<Token2022GroupPointer>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.GroupPointer;
    public static Token2022GroupPointer Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), Token2022ExtensionData.OptionalKey(ref des));
    }
}

public readonly record struct Token2022TokenGroup(PublicKey? UpdateAuthority, PublicKey Mint, ulong Size, ulong MaximumSize) : IToken2022Extension<Token2022TokenGroup>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TokenGroup;
    public static Token2022TokenGroup Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), des.PublicKey(), des.Integer<ulong>(), des.Integer<ulong>());
    }
}

public readonly record struct Token2022GroupMemberPointer(PublicKey? Authority, PublicKey? MemberAddress) : IToken2022Extension<Token2022GroupMemberPointer>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.GroupMemberPointer;
    public static Token2022GroupMemberPointer Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), Token2022ExtensionData.OptionalKey(ref des));
    }
}

public readonly record struct Token2022TokenGroupMember(PublicKey Mint, PublicKey Group, ulong MemberNumber) : IToken2022Extension<Token2022TokenGroupMember>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.TokenGroupMember;
    public static Token2022TokenGroupMember Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(des.PublicKey(), des.PublicKey(), des.Integer<ulong>());
    }
}

public readonly record struct Token2022ConfidentialMintBurn(ReadOnlyMemory<byte> ConfidentialSupply, ReadOnlyMemory<byte> DecryptableSupply,
    ReadOnlyMemory<byte> SupplyElgamalPublicKey, ReadOnlyMemory<byte> PendingBurn) : IToken2022Extension<Token2022ConfidentialMintBurn>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ConfidentialMintBurn;
    public static Token2022ConfidentialMintBurn Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.Bytes(ref des, data, 64), Token2022ExtensionData.Bytes(ref des, data, 36),
            Token2022ExtensionData.Bytes(ref des, data, 32), Token2022ExtensionData.Bytes(ref des, data, 64));
    }
}

public readonly record struct Token2022ScaledUiAmountConfig(PublicKey? Authority, double Multiplier, long NewMultiplierEffectiveTimestamp,
    double NewMultiplier) : IToken2022Extension<Token2022ScaledUiAmountConfig>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.ScaledUiAmount;
    public static Token2022ScaledUiAmountConfig Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), des.Integer<double>(), des.Integer<long>(), des.Integer<double>());
    }
    public double GetMultiplier(long unixTimestamp) => unixTimestamp >= NewMultiplierEffectiveTimestamp ? NewMultiplier : Multiplier;
}

public readonly record struct Token2022PausableConfig(PublicKey? Authority, bool Paused) : IToken2022Extension<Token2022PausableConfig>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.Pausable;
    public static Token2022PausableConfig Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des), des.Bool());
    }
}

public readonly record struct Token2022PausableAccount : IToken2022Extension<Token2022PausableAccount>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.PausableAccount;
    public static Token2022PausableAccount Decode(ReadOnlyMemory<byte> data) { Token2022ExtensionData.Validate(Type, data.Span); return new(); }
}

public readonly record struct Token2022PermissionedBurnConfig(PublicKey? Authority) : IToken2022Extension<Token2022PermissionedBurnConfig>
{
    public static Token2022ExtensionType Type => Token2022ExtensionType.PermissionedBurn;
    public static Token2022PermissionedBurnConfig Decode(ReadOnlyMemory<byte> data)
    {
        var des = Token2022ExtensionData.Read(Type, data);
        return new(Token2022ExtensionData.OptionalKey(ref des));
    }
}
