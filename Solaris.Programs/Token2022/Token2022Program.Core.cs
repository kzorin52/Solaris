using System.Text;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeMint(PublicKey mint, byte decimals, PublicKey mintAuthority,
        PublicKey? freezeAuthority = null)
    {
        return Instruction(CoreMintData(0, decimals, mintAuthority, freezeAuthority),
            [AccountMeta.Writable(mint), AccountMeta.ReadOnly(SysVar.Rent)]);
    }

    public static TransactionInstruction InitializeMint2(PublicKey mint, byte decimals, PublicKey mintAuthority,
        PublicKey? freezeAuthority = null)
    {
        return Instruction(CoreMintData(20, decimals, mintAuthority, freezeAuthority), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction InitializeAccount(PublicKey account, PublicKey mint, PublicKey owner)
    {
        return Instruction(CoreData(1),
            [AccountMeta.Writable(account), AccountMeta.ReadOnly(mint), AccountMeta.ReadOnly(owner),
                AccountMeta.ReadOnly(SysVar.Rent)]);
    }

    public static TransactionInstruction InitializeAccount2(PublicKey account, PublicKey mint, PublicKey owner)
    {
        return Instruction(CorePublicKeyData(16, owner),
            [AccountMeta.Writable(account), AccountMeta.ReadOnly(mint), AccountMeta.ReadOnly(SysVar.Rent)]);
    }

    public static TransactionInstruction InitializeAccount3(PublicKey account, PublicKey mint, PublicKey owner)
    {
        return Instruction(CorePublicKeyData(18, owner), [AccountMeta.Writable(account), AccountMeta.ReadOnly(mint)]);
    }

    public static TransactionInstruction InitializeMultisig(PublicKey multisig, byte requiredSigners,
        ReadOnlySpan<PublicKey> signers)
    {
        return CoreInitializeMultisig(2, multisig, requiredSigners, signers, true);
    }

    public static TransactionInstruction InitializeMultisig2(PublicKey multisig, byte requiredSigners,
        ReadOnlySpan<PublicKey> signers)
    {
        return CoreInitializeMultisig(19, multisig, requiredSigners, signers, false);
    }

    public static TransactionInstruction Transfer(PublicKey source, PublicKey destination, ulong amount,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default,
        ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners, remainingAccounts);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(CoreAmountData(3, amount), keys);
    }

    public static TransactionInstruction TransferChecked(PublicKey source, PublicKey mint, PublicKey destination,
        ulong amount, byte decimals, PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default,
        ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        var keys = AuthorityKeys(3, authority, multisigSigners, remainingAccounts);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.ReadOnly(mint);
        keys[2] = AccountMeta.Writable(destination);
        return Instruction(CoreAmountDecimalsData(12, amount, decimals), keys);
    }

    public static TransactionInstruction Approve(PublicKey source, PublicKey delegateAccount, ulong amount,
        PublicKey owner, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, owner, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.ReadOnly(delegateAccount);
        return Instruction(CoreAmountData(4, amount), keys);
    }

    public static TransactionInstruction ApproveChecked(PublicKey source, PublicKey mint, PublicKey delegateAccount,
        ulong amount, byte decimals, PublicKey owner, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(3, owner, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.ReadOnly(mint);
        keys[2] = AccountMeta.ReadOnly(delegateAccount);
        return Instruction(CoreAmountDecimalsData(13, amount, decimals), keys);
    }

    public static TransactionInstruction Revoke(PublicKey source, PublicKey owner,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(1, owner, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        return Instruction(CoreData(5), keys);
    }

    public static TransactionInstruction SetAuthority(PublicKey account, PublicKey currentAuthority,
        Token2022AuthorityType authorityType, PublicKey? newAuthority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var data = new FluentSerializer(newAuthority is null ? 3 : 35);
        data.Write(6).Write((byte)authorityType).Write(newAuthority is not null);
        if (newAuthority is not null) data.Write(newAuthority.KeySpan);
        var keys = AuthorityKeys(1, currentAuthority, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        return Instruction(data.Build(), keys);
    }

    public static TransactionInstruction MintTo(PublicKey mint, PublicKey destination, ulong amount,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(mint);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(CoreAmountData(7, amount), keys);
    }

    public static TransactionInstruction MintToChecked(PublicKey mint, PublicKey destination, ulong amount,
        byte decimals, PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(mint);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(CoreAmountDecimalsData(14, amount, decimals), keys);
    }

    public static TransactionInstruction Burn(PublicKey source, PublicKey mint, ulong amount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.Writable(mint);
        return Instruction(CoreAmountData(8, amount), keys);
    }

    public static TransactionInstruction BurnChecked(PublicKey source, PublicKey mint, ulong amount, byte decimals,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.Writable(mint);
        return Instruction(CoreAmountDecimalsData(15, amount, decimals), keys);
    }

    public static TransactionInstruction CloseAccount(PublicKey account, PublicKey destination, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(CoreData(9), keys);
    }

    public static TransactionInstruction FreezeAccount(PublicKey account, PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        keys[1] = AccountMeta.ReadOnly(mint);
        return Instruction(CoreData(10), keys);
    }

    public static TransactionInstruction ThawAccount(PublicKey account, PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        keys[1] = AccountMeta.ReadOnly(mint);
        return Instruction(CoreData(11), keys);
    }

    public static TransactionInstruction SyncNative(PublicKey account)
    {
        return Instruction(CoreData(17), [AccountMeta.Writable(account)]);
    }

    public static TransactionInstruction SyncNativeWithRentSysvar(PublicKey account)
    {
        return Instruction(CoreData(17), [AccountMeta.Writable(account), AccountMeta.ReadOnly(SysVar.Rent)]);
    }

    public static TransactionInstruction GetAccountDataSize(PublicKey mint,
        ReadOnlySpan<Token2022ExtensionType> extensionTypes = default)
    {
        return Instruction(CoreExtensionTypesData(21, extensionTypes), [AccountMeta.ReadOnly(mint)]);
    }

    public static TransactionInstruction InitializeImmutableOwner(PublicKey account)
    {
        return Instruction(CoreData(22), [AccountMeta.Writable(account)]);
    }

    public static TransactionInstruction AmountToUiAmount(PublicKey mint, ulong amount)
    {
        return Instruction(CoreAmountData(23, amount), [AccountMeta.ReadOnly(mint)]);
    }

    public static TransactionInstruction UiAmountToAmount(PublicKey mint, string uiAmount)
    {
        var data = new byte[checked(1 + Encoding.UTF8.GetByteCount(uiAmount))];
        var serializer = new BorshSerializer(data);
        serializer.Write((byte)24);
        Encoding.UTF8.GetBytes(uiAmount, data.AsSpan(serializer.Offset));
        return Instruction(data, [AccountMeta.ReadOnly(mint)]);
    }

    public static TransactionInstruction InitializeMintCloseAuthority(PublicKey mint, PublicKey? closeAuthority = null)
    {
        var data = new FluentSerializer(closeAuthority is null ? 2 : 34);
        data.Write(25).Write(closeAuthority is not null);
        if (closeAuthority is not null) data.Write(closeAuthority.KeySpan);
        return Instruction(data.Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction Reallocate(PublicKey account, PublicKey payer, PublicKey owner,
        ReadOnlySpan<Token2022ExtensionType> extensionTypes, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(3, owner, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        keys[1] = AccountMeta.Writable(payer, true);
        keys[2] = AccountMeta.ReadOnly(SystemProgram.ProgramId);
        return Instruction(CoreExtensionTypesData(29, extensionTypes), keys);
    }

    public static TransactionInstruction CreateNativeMint(PublicKey payer)
    {
        return Instruction(CoreData(31),
            [AccountMeta.Writable(payer, true), AccountMeta.Writable(NativeMint),
                AccountMeta.ReadOnly(SystemProgram.ProgramId)]);
    }

    public static TransactionInstruction InitializeNonTransferableMint(PublicKey mint)
    {
        return Instruction(CoreData(32), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction InitializePermanentDelegate(PublicKey mint, PublicKey delegateAccount)
    {
        return Instruction(CorePublicKeyData(35, delegateAccount), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction WithdrawExcessLamports(PublicKey source, PublicKey destination,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(CoreData(38), keys);
    }

    public static TransactionInstruction UnwrapLamports(PublicKey source, PublicKey destination, PublicKey authority,
        ulong? amount = null, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var data = new FluentSerializer(amount.HasValue ? 10 : 2);
        data.Write(45).Write(amount.HasValue);
        if (amount.HasValue) data.WriteInteger(amount.Value);
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(source);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(data.Build(), keys);
    }

    public static TransactionInstruction Batch(ReadOnlySpan<TransactionInstruction> instructions)
    {
        if (instructions.IsEmpty)
            throw new ArgumentException("A batch must contain at least one instruction.", nameof(instructions));
        var dataLength = 1;
        var accountCount = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.ProgramId != ProgramId)
                throw new ArgumentException("All batch instructions must target Token-2022.", nameof(instructions));
            if (instruction.Data.Length is < 1 or > byte.MaxValue || instruction.Keys.Length > byte.MaxValue)
                throw new ArgumentException("Batch item data and account counts must fit in a byte.", nameof(instructions));
            if (instruction.Data[0] == byte.MaxValue)
                throw new ArgumentException("Nested batches are not supported.", nameof(instructions));
            dataLength = checked(dataLength + 2 + instruction.Data.Length);
            accountCount = checked(accountCount + instruction.Keys.Length);
        }
        var data = new byte[dataLength];
        var serializer = new BorshSerializer(data);
        serializer.Write(byte.MaxValue);
        var keys = new AccountMeta[accountCount];
        var accountOffset = 0;
        foreach (var instruction in instructions)
        {
            serializer.Write((byte)instruction.Keys.Length);
            serializer.Write((byte)instruction.Data.Length);
            serializer.Write(instruction.Data);
            instruction.Keys.AsSpan().CopyTo(keys.AsSpan(accountOffset));
            accountOffset += instruction.Keys.Length;
        }
        return Instruction(data, keys);
    }

    private static byte[] CoreData(byte discriminator)
    {
        return new FluentSerializer(1).Write(discriminator).Build();
    }

    private static byte[] CoreAmountData(byte discriminator, ulong amount)
    {
        return new FluentSerializer(9).Write(discriminator).WriteInteger(amount).Build();
    }

    private static byte[] CoreAmountDecimalsData(byte discriminator, ulong amount, byte decimals)
    {
        return new FluentSerializer(10).Write(discriminator).WriteInteger(amount).Write(decimals).Build();
    }

    private static byte[] CorePublicKeyData(byte discriminator, PublicKey key)
    {
        return new FluentSerializer(33).Write(discriminator).Write(key.KeySpan).Build();
    }

    private static byte[] CoreMintData(byte discriminator, byte decimals, PublicKey mintAuthority,
        PublicKey? freezeAuthority)
    {
        var data = new FluentSerializer(freezeAuthority is null ? 35 : 67);
        data.Write(discriminator).Write(decimals).Write(mintAuthority.KeySpan).Write(freezeAuthority is not null);
        if (freezeAuthority is not null) data.Write(freezeAuthority.KeySpan);
        return data.Build();
    }

    private static byte[] CoreExtensionTypesData(byte discriminator, ReadOnlySpan<Token2022ExtensionType> extensionTypes)
    {
        var data = new FluentSerializer(checked(1 + 2 * extensionTypes.Length));
        data.Write(discriminator);
        foreach (var extensionType in extensionTypes) data.WriteInteger((ushort)extensionType);
        return data.Build();
    }

    private static TransactionInstruction CoreInitializeMultisig(byte discriminator, PublicKey multisig,
        byte requiredSigners, ReadOnlySpan<PublicKey> signers, bool includeRent)
    {
        if (signers.Length is < MinSigners or > MaxSigners)
            throw new ArgumentOutOfRangeException(nameof(signers), "A multisig requires between 1 and 11 signers.");
        if (requiredSigners < MinSigners || requiredSigners > signers.Length)
            throw new ArgumentOutOfRangeException(nameof(requiredSigners));
        var prefixCount = includeRent ? 2 : 1;
        var keys = new AccountMeta[prefixCount + signers.Length];
        keys[0] = AccountMeta.Writable(multisig);
        if (includeRent) keys[1] = AccountMeta.ReadOnly(SysVar.Rent);
        for (var i = 0; i < signers.Length; i++) keys[prefixCount + i] = AccountMeta.ReadOnly(signers[i]);
        return Instruction(new FluentSerializer(2).Write(discriminator).Write(requiredSigners).Build(), keys);
    }
}
