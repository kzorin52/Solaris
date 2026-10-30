using Solaris.Base.Account;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static readonly PublicKey ProgramId = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";
    public static readonly PublicKey NativeMint = "9pan9bMn5HatX4EJdBwg9VgCa7Uz5HL8N1m5D3NdXejP";
    public const byte NativeMintDecimals = 9;
    public const int MinSigners = 1;
    public const int MaxSigners = 11;

    internal static TransactionInstruction Instruction(byte[] data, AccountMeta[] keys)
    {
        return new TransactionInstruction { ProgramId = ProgramId, Data = data, Keys = keys };
    }

    internal static AccountMeta[] AuthorityKeys(int prefixCount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners)
    {
        return AuthorityKeys(prefixCount, authority, multisigSigners, default);
    }

    internal static AccountMeta[] AuthorityKeys(int prefixCount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners, ReadOnlySpan<AccountMeta> remainingAccounts)
    {
        var keys = new AccountMeta[checked(prefixCount + 1 + multisigSigners.Length + remainingAccounts.Length)];
        keys[prefixCount] = AccountMeta.ReadOnly(authority, multisigSigners.IsEmpty);
        for (var i = 0; i < multisigSigners.Length; i++)
            keys[prefixCount + 1 + i] = AccountMeta.ReadOnly(multisigSigners[i], true);
        remainingAccounts.CopyTo(keys.AsSpan(prefixCount + 1 + multisigSigners.Length));
        return keys;
    }
}

public enum Token2022AuthorityType : byte
{
    MintTokens = 0,
    FreezeAccount = 1,
    AccountOwner = 2,
    CloseAccount = 3,
    TransferFeeConfig = 4,
    WithheldWithdraw = 5,
    CloseMint = 6,
    InterestRate = 7,
    PermanentDelegate = 8,
    ConfidentialTransferMint = 9,
    TransferHookProgramId = 10,
    ConfidentialTransferFeeConfig = 11,
    MetadataPointer = 12,
    GroupPointer = 13,
    GroupMemberPointer = 14,
    ScaledUiAmount = 15,
    Pause = 16,
    PermissionedBurn = 17
}
