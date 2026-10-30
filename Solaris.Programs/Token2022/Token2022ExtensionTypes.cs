namespace Solaris.Programs.Token2022;

public enum Token2022AccountType : byte
{
    Uninitialized,
    Mint,
    Account
}

public enum Token2022AccountState : byte
{
    Uninitialized,
    Initialized,
    Frozen
}

public enum Token2022ExtensionType : ushort
{
    Uninitialized,
    TransferFeeConfig,
    TransferFeeAmount,
    MintCloseAuthority,
    ConfidentialTransferMint,
    ConfidentialTransferAccount,
    DefaultAccountState,
    ImmutableOwner,
    MemoTransfer,
    NonTransferable,
    InterestBearingConfig,
    CpiGuard,
    PermanentDelegate,
    NonTransferableAccount,
    TransferHook,
    TransferHookAccount,
    ConfidentialTransferFeeConfig,
    ConfidentialTransferFeeAmount,
    MetadataPointer,
    TokenMetadata,
    GroupPointer,
    TokenGroup,
    GroupMemberPointer,
    TokenGroupMember,
    ConfidentialMintBurn,
    ScaledUiAmount,
    Pausable,
    PausableAccount,
    PermissionedBurn
}

public readonly record struct Token2022ExtensionSize(Token2022ExtensionType Type, int Length);

public static class Token2022ExtensionSizes
{
    public const int ExtensionStart = 166;
    public const int TlvHeaderSize = 4;

    public static int GetPayloadSize(Token2022ExtensionType type) => type switch
    {
        Token2022ExtensionType.Uninitialized or Token2022ExtensionType.ImmutableOwner or
            Token2022ExtensionType.NonTransferable or Token2022ExtensionType.NonTransferableAccount or
            Token2022ExtensionType.PausableAccount => 0,
        Token2022ExtensionType.TransferFeeConfig => 108,
        Token2022ExtensionType.TransferFeeAmount => 8,
        Token2022ExtensionType.MintCloseAuthority or Token2022ExtensionType.PermanentDelegate or
            Token2022ExtensionType.PermissionedBurn => 32,
        Token2022ExtensionType.ConfidentialTransferMint => 65,
        Token2022ExtensionType.ConfidentialTransferAccount => 295,
        Token2022ExtensionType.DefaultAccountState or Token2022ExtensionType.MemoTransfer or
            Token2022ExtensionType.CpiGuard or Token2022ExtensionType.TransferHookAccount => 1,
        Token2022ExtensionType.InterestBearingConfig => 52,
        Token2022ExtensionType.TransferHook or Token2022ExtensionType.MetadataPointer or
            Token2022ExtensionType.GroupPointer or Token2022ExtensionType.GroupMemberPointer or
            Token2022ExtensionType.ConfidentialTransferFeeAmount => 64,
        Token2022ExtensionType.ConfidentialTransferFeeConfig => 129,
        Token2022ExtensionType.TokenGroup => 80,
        Token2022ExtensionType.TokenGroupMember => 72,
        Token2022ExtensionType.ConfidentialMintBurn => 196,
        Token2022ExtensionType.ScaledUiAmount => 56,
        Token2022ExtensionType.Pausable => 33,
        Token2022ExtensionType.TokenMetadata => throw new ArgumentException("Token metadata has a variable payload size.", nameof(type)),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static Token2022AccountType GetAccountType(Token2022ExtensionType type) => type switch
    {
        Token2022ExtensionType.Uninitialized => Token2022AccountType.Uninitialized,
        Token2022ExtensionType.TransferFeeAmount or Token2022ExtensionType.ConfidentialTransferAccount or
            Token2022ExtensionType.ImmutableOwner or Token2022ExtensionType.MemoTransfer or
            Token2022ExtensionType.CpiGuard or Token2022ExtensionType.NonTransferableAccount or
            Token2022ExtensionType.TransferHookAccount or Token2022ExtensionType.ConfidentialTransferFeeAmount or
            Token2022ExtensionType.PausableAccount => Token2022AccountType.Account,
        _ when (ushort)type <= (ushort)Token2022ExtensionType.PermissionedBurn => Token2022AccountType.Mint,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static int GetMintSize(ReadOnlySpan<Token2022ExtensionType> extensions) => GetSize(Token2022AccountType.Mint, extensions);
    public static int GetAccountSize(ReadOnlySpan<Token2022ExtensionType> extensions) => GetSize(Token2022AccountType.Account, extensions);
    public static int GetMintSize(ReadOnlySpan<Token2022ExtensionSize> extensions) => GetSize(Token2022AccountType.Mint, extensions);
    public static int GetAccountSize(ReadOnlySpan<Token2022ExtensionSize> extensions) => GetSize(Token2022AccountType.Account, extensions);

    public static int GetSize(Token2022AccountType accountType, ReadOnlySpan<Token2022ExtensionType> extensions)
    {
        ValidateAccountType(accountType);
        ulong seen = 0;
        var length = 0;
        foreach (var type in extensions)
        {
            ValidateExtensionType(accountType, type);
            var bit = 1UL << (int)type;
            if ((seen & bit) != 0) continue;
            seen |= bit;
            length = checked(length + TlvHeaderSize + GetPayloadSize(type));
        }
        return FinishSize(accountType, seen, length);
    }

    public static int GetSize(Token2022AccountType accountType, ReadOnlySpan<Token2022ExtensionSize> extensions)
    {
        ValidateAccountType(accountType);
        ulong seen = 0;
        var length = 0;
        for (var i = 0; i < extensions.Length; i++)
        {
            var extension = extensions[i];
            ValidateExtensionType(accountType, extension.Type);
            if ((uint)extension.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(extensions));
            if (extension.Type != Token2022ExtensionType.TokenMetadata && extension.Length != GetPayloadSize(extension.Type))
                throw new ArgumentException("Incorrect fixed extension size.", nameof(extensions));
            if (extension.Type == Token2022ExtensionType.TokenMetadata && extension.Length < 80)
                throw new ArgumentException("Token metadata requires at least 80 bytes.", nameof(extensions));
            var bit = 1UL << (int)extension.Type;
            if ((seen & bit) != 0)
            {
                for (var j = 0; j < i; j++)
                    if (extensions[j].Type == extension.Type && extensions[j].Length != extension.Length)
                        throw new ArgumentException("Conflicting duplicate extension sizes.", nameof(extensions));
                continue;
            }
            seen |= bit;
            length = checked(length + TlvHeaderSize + extension.Length);
        }
        return FinishSize(accountType, seen, length);
    }

    public static int GetRequiredAccountExtensions(ReadOnlySpan<Token2022ExtensionType> mintExtensions, Span<Token2022ExtensionType> destination)
    {
        ulong required = 0;
        var count = 0;
        foreach (var type in mintExtensions)
        {
            ValidateExtensionType(Token2022AccountType.Mint, type);
            switch (type)
            {
                case Token2022ExtensionType.TransferFeeConfig: AddRequired(Token2022ExtensionType.TransferFeeAmount, destination, ref required, ref count); break;
                case Token2022ExtensionType.NonTransferable:
                    AddRequired(Token2022ExtensionType.NonTransferableAccount, destination, ref required, ref count);
                    AddRequired(Token2022ExtensionType.ImmutableOwner, destination, ref required, ref count);
                    break;
                case Token2022ExtensionType.TransferHook: AddRequired(Token2022ExtensionType.TransferHookAccount, destination, ref required, ref count); break;
                case Token2022ExtensionType.Pausable: AddRequired(Token2022ExtensionType.PausableAccount, destination, ref required, ref count); break;
            }
        }
        return count;
    }

    public static int GetAccountSizeForMint(Token2022Mint mint, ReadOnlySpan<Token2022ExtensionType> additionalExtensions = default)
    {
        ArgumentNullException.ThrowIfNull(mint);
        Span<Token2022ExtensionType> types = stackalloc Token2022ExtensionType[29];
        if (!mint.IsInitialized) throw new ArgumentException("Mint is not initialized.", nameof(mint));
        var count = 0;
        foreach (var extension in mint.Extensions.Borrow())
        {
            if (extension.RawType > (ushort)Token2022ExtensionType.PermissionedBurn) continue;
            types[count++] = extension.Type;
        }
        Span<Token2022ExtensionType> required = stackalloc Token2022ExtensionType[5];
        var requiredCount = GetRequiredAccountExtensions(types[..count], required);
        ulong seen = 0;
        var length = 0;
        foreach (var type in required[..requiredCount]) AddSize(type, ref seen, ref length);
        foreach (var type in additionalExtensions) AddSize(type, ref seen, ref length);
        return FinishSize(Token2022AccountType.Account, seen, length);
    }

    public static void ValidateMintExtensionCombinations(ReadOnlySpan<Token2022ExtensionType> extensions)
    {
        ulong seen = 0;
        foreach (var type in extensions)
        {
            ValidateExtensionType(Token2022AccountType.Mint, type);
            seen |= 1UL << (int)type;
        }
        var fees = Has(seen, Token2022ExtensionType.TransferFeeConfig);
        var confidential = Has(seen, Token2022ExtensionType.ConfidentialTransferMint);
        var confidentialFees = Has(seen, Token2022ExtensionType.ConfidentialTransferFeeConfig);
        var mintBurn = Has(seen, Token2022ExtensionType.ConfidentialMintBurn);
        if ((confidentialFees && !(fees && confidential)) ||
            (fees && confidential && !confidentialFees) || (mintBurn && !confidential) ||
            (Has(seen, Token2022ExtensionType.ScaledUiAmount) && Has(seen, Token2022ExtensionType.InterestBearingConfig)) ||
            (Has(seen, Token2022ExtensionType.NonTransferable) && confidential && !mintBurn))
            throw new ArgumentException("Invalid mint extension combination.", nameof(extensions));
    }

    private static bool Has(ulong set, Token2022ExtensionType type) => (set & (1UL << (int)type)) != 0;

    private static void AddSize(Token2022ExtensionType type, ref ulong seen, ref int length)
    {
        ValidateExtensionType(Token2022AccountType.Account, type);
        var bit = 1UL << (int)type;
        if ((seen & bit) != 0) return;
        seen |= bit;
        length = checked(length + TlvHeaderSize + GetPayloadSize(type));
    }

    private static void AddRequired(Token2022ExtensionType type, Span<Token2022ExtensionType> destination, ref ulong seen, ref int count)
    {
        var bit = 1UL << (int)type;
        if ((seen & bit) != 0) return;
        if (count >= destination.Length) throw new ArgumentException("Destination is too short.", nameof(destination));
        seen |= bit;
        destination[count++] = type;
    }

    private static int FinishSize(Token2022AccountType accountType, ulong seen, int length)
    {
        if (seen == 0) return accountType == Token2022AccountType.Mint ? Token2022Mint.Size : Token2022Account.Size;
        length = checked(length + ExtensionStart);
        return length == Token2022Multisig.Size ? length + 2 : length;
    }

    private static void ValidateAccountType(Token2022AccountType accountType)
    {
        if (accountType is not (Token2022AccountType.Mint or Token2022AccountType.Account))
            throw new ArgumentOutOfRangeException(nameof(accountType));
    }

    private static void ValidateExtensionType(Token2022AccountType accountType, Token2022ExtensionType type)
    {
        if (type == Token2022ExtensionType.Uninitialized || GetAccountType(type) != accountType)
            throw new ArgumentException("Extension does not belong to this account type.", nameof(type));
    }
}
