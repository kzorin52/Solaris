using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeTransferFeeConfig(PublicKey mint,
        PublicKey? transferFeeConfigAuthority, PublicKey? withdrawWithheldAuthority,
        ushort transferFeeBasisPoints, ulong maximumFee)
    {
        var data = new FluentSerializer(14 + (transferFeeConfigAuthority is null ? 0 : 32) +
            (withdrawWithheldAuthority is null ? 0 : 32));
        data.Write(26).Write(0).Write(transferFeeConfigAuthority is null ? (byte)0 : (byte)1);
        if (transferFeeConfigAuthority is not null) data.Write(transferFeeConfigAuthority.KeySpan);
        data.Write(withdrawWithheldAuthority is null ? (byte)0 : (byte)1);
        if (withdrawWithheldAuthority is not null) data.Write(withdrawWithheldAuthority.KeySpan);
        data.WriteInteger(transferFeeBasisPoints).WriteInteger(maximumFee);
        return Instruction(data.Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction TransferCheckedWithFee(PublicKey source, PublicKey mint,
        PublicKey destination, PublicKey authority, ulong amount, byte decimals, ulong fee,
        ReadOnlySpan<PublicKey> multisigSigners = default, ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        var keys = new AccountMeta[4 + multisigSigners.Length + remainingAccounts.Length];
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.ReadOnly(mint);
        keys[2] = AccountMeta.Writable(destination);
        keys[3] = AccountMeta.ReadOnly(authority, multisigSigners.IsEmpty);
        for (var i = 0; i < multisigSigners.Length; i++)
            keys[4 + i] = AccountMeta.ReadOnly(multisigSigners[i], true);
        remainingAccounts.CopyTo(keys.AsSpan(4 + multisigSigners.Length));
        return Instruction(new FluentSerializer(19).Write(26).Write(1).WriteInteger(amount)
            .Write(decimals).WriteInteger(fee).Build(), keys);
    }

    public static TransactionInstruction WithdrawWithheldTokensFromMint(PublicKey mint, PublicKey destination,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(mint);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(new FluentSerializer(2).Write(26).Write(2).Build(), keys);
    }

    public static TransactionInstruction WithdrawWithheldTokensFromAccounts(PublicKey mint, PublicKey destination,
        PublicKey authority, ReadOnlySpan<PublicKey> sources, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        if (sources.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(sources));
        var keys = new AccountMeta[3 + multisigSigners.Length + sources.Length];
        keys[0] = AccountMeta.ReadOnly(mint);
        keys[1] = AccountMeta.Writable(destination);
        keys[2] = AccountMeta.ReadOnly(authority, multisigSigners.IsEmpty);
        for (var i = 0; i < multisigSigners.Length; i++)
            keys[3 + i] = AccountMeta.ReadOnly(multisigSigners[i], true);
        for (var i = 0; i < sources.Length; i++)
            keys[3 + multisigSigners.Length + i] = AccountMeta.Writable(sources[i]);
        return Instruction(new FluentSerializer(3).Write(26).Write(3).Write((byte)sources.Length).Build(), keys);
    }

    public static TransactionInstruction HarvestWithheldTokensToMint(PublicKey mint, ReadOnlySpan<PublicKey> sources)
    {
        var keys = new AccountMeta[1 + sources.Length];
        keys[0] = AccountMeta.Writable(mint);
        for (var i = 0; i < sources.Length; i++) keys[1 + i] = AccountMeta.Writable(sources[i]);
        return Instruction(new FluentSerializer(2).Write(26).Write(4).Build(), keys);
    }

    public static TransactionInstruction SetTransferFee(PublicKey mint, PublicKey authority,
        ushort transferFeeBasisPoints, ulong maximumFee, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return Instruction(new FluentSerializer(12).Write(26).Write(5).WriteInteger(transferFeeBasisPoints)
            .WriteInteger(maximumFee).Build(), ExtensionAuthorityKeys(mint, authority, multisigSigners));
    }

    public static TransactionInstruction InitializeDefaultAccountState(PublicKey mint, Token2022AccountState state)
    {
        return Instruction(new FluentSerializer(3).Write(28).Write(0).Write((byte)state).Build(),
            [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction UpdateDefaultAccountState(PublicKey mint, PublicKey freezeAuthority,
        Token2022AccountState state, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return Instruction(new FluentSerializer(3).Write(28).Write(1).Write((byte)state).Build(),
            ExtensionAuthorityKeys(mint, freezeAuthority, multisigSigners));
    }

    public static TransactionInstruction EnableRequiredTransferMemos(PublicKey account, PublicKey owner,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return ExtensionToggle(30, 0, account, owner, multisigSigners);
    }

    public static TransactionInstruction DisableRequiredTransferMemos(PublicKey account, PublicKey owner,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return ExtensionToggle(30, 1, account, owner, multisigSigners);
    }

    public static TransactionInstruction InitializeInterestBearingMint(PublicKey mint, PublicKey? rateAuthority,
        short rate)
    {
        var data = new FluentSerializer(36);
        data.Write(33).Write(0);
        WriteExtensionOptionalKey(ref data, rateAuthority, nameof(rateAuthority));
        data.WriteInteger(rate);
        return Instruction(data.Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction UpdateInterestBearingMintRate(PublicKey mint, PublicKey rateAuthority,
        short rate, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return Instruction(new FluentSerializer(4).Write(33).Write(1).WriteInteger(rate).Build(),
            ExtensionAuthorityKeys(mint, rateAuthority, multisigSigners));
    }

    public static TransactionInstruction EnableCpiGuard(PublicKey account, PublicKey owner,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return ExtensionToggle(34, 0, account, owner, multisigSigners);
    }

    public static TransactionInstruction DisableCpiGuard(PublicKey account, PublicKey owner,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return ExtensionToggle(34, 1, account, owner, multisigSigners);
    }

    public static TransactionInstruction InitializeTransferHook(PublicKey mint, PublicKey? authority,
        PublicKey? transferHookProgramId)
    {
        return InitializeExtensionPointer(36, mint, authority, transferHookProgramId, nameof(transferHookProgramId));
    }

    public static TransactionInstruction UpdateTransferHook(PublicKey mint, PublicKey authority,
        PublicKey? transferHookProgramId, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return UpdateExtensionPointer(36, mint, authority, transferHookProgramId, nameof(transferHookProgramId),
            multisigSigners);
    }

    public static TransactionInstruction InitializeMetadataPointer(PublicKey mint, PublicKey? authority,
        PublicKey? metadataAddress)
    {
        return InitializeExtensionPointer(39, mint, authority, metadataAddress, nameof(metadataAddress));
    }

    public static TransactionInstruction UpdateMetadataPointer(PublicKey mint, PublicKey authority,
        PublicKey? metadataAddress, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return UpdateExtensionPointer(39, mint, authority, metadataAddress, nameof(metadataAddress), multisigSigners);
    }

    public static TransactionInstruction InitializeGroupPointer(PublicKey mint, PublicKey? authority,
        PublicKey? groupAddress)
    {
        return InitializeExtensionPointer(40, mint, authority, groupAddress, nameof(groupAddress));
    }

    public static TransactionInstruction UpdateGroupPointer(PublicKey mint, PublicKey authority,
        PublicKey? groupAddress, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return UpdateExtensionPointer(40, mint, authority, groupAddress, nameof(groupAddress), multisigSigners);
    }

    public static TransactionInstruction InitializeGroupMemberPointer(PublicKey mint, PublicKey? authority,
        PublicKey? memberAddress)
    {
        return InitializeExtensionPointer(41, mint, authority, memberAddress, nameof(memberAddress));
    }

    public static TransactionInstruction UpdateGroupMemberPointer(PublicKey mint, PublicKey authority,
        PublicKey? memberAddress, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return UpdateExtensionPointer(41, mint, authority, memberAddress, nameof(memberAddress), multisigSigners);
    }

    public static TransactionInstruction InitializeScaledUiAmountMint(PublicKey mint, PublicKey? authority,
        double multiplier)
    {
        var data = new FluentSerializer(42);
        data.Write(43).Write(0);
        WriteExtensionOptionalKey(ref data, authority, nameof(authority));
        data.WriteInteger(multiplier);
        return Instruction(data.Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction UpdateScaledUiAmountMultiplier(PublicKey mint, PublicKey authority,
        double multiplier, long effectiveTimestamp, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return Instruction(new FluentSerializer(18).Write(43).Write(1).WriteInteger(multiplier)
            .WriteInteger(effectiveTimestamp).Build(), ExtensionAuthorityKeys(mint, authority, multisigSigners));
    }

    public static TransactionInstruction InitializePausableMint(PublicKey mint, PublicKey authority)
    {
        return Instruction(new FluentSerializer(34).Write(44).Write(0).Write(authority.KeySpan).Build(),
            [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction Pause(PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return ExtensionToggle(44, 1, mint, authority, multisigSigners);
    }

    public static TransactionInstruction Resume(PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        return ExtensionToggle(44, 2, mint, authority, multisigSigners);
    }

    private static AccountMeta[] ExtensionAuthorityKeys(PublicKey account, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners)
    {
        var keys = AuthorityKeys(1, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        return keys;
    }

    private static TransactionInstruction ExtensionToggle(byte extension, byte instruction, PublicKey account,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners)
    {
        return Instruction(new FluentSerializer(2).Write(extension).Write(instruction).Build(),
            ExtensionAuthorityKeys(account, authority, multisigSigners));
    }

    private static TransactionInstruction InitializeExtensionPointer(byte extension, PublicKey mint,
        PublicKey? authority, PublicKey? address, string addressParameterName)
    {
        var data = new FluentSerializer(66);
        data.Write(extension).Write(0);
        WriteExtensionOptionalKey(ref data, authority, nameof(authority));
        WriteExtensionOptionalKey(ref data, address, addressParameterName);
        return Instruction(data.Build(), [AccountMeta.Writable(mint)]);
    }

    private static TransactionInstruction UpdateExtensionPointer(byte extension, PublicKey mint,
        PublicKey authority, PublicKey? address, string addressParameterName, ReadOnlySpan<PublicKey> multisigSigners)
    {
        var data = new FluentSerializer(34);
        data.Write(extension).Write(1);
        WriteExtensionOptionalKey(ref data, address, addressParameterName);
        return Instruction(data.Build(), ExtensionAuthorityKeys(mint, authority, multisigSigners));
    }

    private static void WriteExtensionOptionalKey(ref FluentSerializer data, PublicKey? key, string parameterName)
    {
        if (key is null)
        {
            data.Skip(32);
            return;
        }
        if (key.KeySpan.IndexOfAnyExcept((byte)0) < 0)
            throw new ArgumentException("An optional public key must be nonzero; use null for no key.", parameterName);
        data.Write(key.KeySpan);
    }
}
