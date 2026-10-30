using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public readonly struct Token2022ProofLocation
{
    public sbyte Offset { get; }
    public PublicKey? ContextState { get; }

    private Token2022ProofLocation(sbyte offset, PublicKey? contextState)
    {
        Offset = offset;
        ContextState = contextState;
    }

    public static Token2022ProofLocation InstructionOffset(sbyte offset)
    {
        if (offset == 0) throw new ArgumentOutOfRangeException(nameof(offset));
        return new Token2022ProofLocation(offset, null);
    }

    public static Token2022ProofLocation ContextStateAccount(PublicKey contextState)
    {
        ArgumentNullException.ThrowIfNull(contextState);
        return new Token2022ProofLocation(0, contextState);
    }

    internal void Validate()
    {
        if (Offset == 0 && ContextState is null)
            throw new ArgumentException("A zero proof offset requires a context-state account.");
    }
}

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeConfidentialTransferMint(PublicKey mint, PublicKey? authority,
        bool autoApproveNewAccounts, ReadOnlySpan<byte> auditorElGamalPubkey = default)
    {
        ConfidentialOptionalPubkey(auditorElGamalPubkey, nameof(auditorElGamalPubkey));
        var data = new FluentSerializer(67).Write(27).Write(0).Write(authority ?? SystemProgram.ProgramId)
            .Write(autoApproveNewAccounts);
        if (auditorElGamalPubkey.IsEmpty) data.Skip(32); else data.Write(auditorElGamalPubkey);
        return Instruction(data.Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction UpdateConfidentialTransferMint(PublicKey mint, PublicKey authority,
        bool autoApproveNewAccounts, ReadOnlySpan<byte> auditorElGamalPubkey = default,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialOptionalPubkey(auditorElGamalPubkey, nameof(auditorElGamalPubkey));
        var data = new FluentSerializer(35).Write(27).Write(1).Write(autoApproveNewAccounts);
        if (auditorElGamalPubkey.IsEmpty) data.Skip(32); else data.Write(auditorElGamalPubkey);
        var keys = AuthorityKeys(1, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(mint);
        return Instruction(data.Build(), keys);
    }

    public static TransactionInstruction ConfigureConfidentialTransferAccount(PublicKey tokenAccount, PublicKey mint,
        ReadOnlySpan<byte> decryptableZeroBalance, ulong maximumPendingBalanceCreditCounter, PublicKey authority,
        Token2022ProofLocation pubkeyValidityProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(decryptableZeroBalance, 36, nameof(decryptableZeroBalance));
        var keys = ConfidentialKeys(2, authority, multisigSigners, new(1, pubkeyValidityProof));
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        return Instruction(new FluentSerializer(47).Write(27).Write(2).Write(decryptableZeroBalance)
            .WriteInteger(maximumPendingBalanceCreditCounter).WriteInteger(pubkeyValidityProof.Offset).Build(), keys);
    }

    public static TransactionInstruction ApproveConfidentialTransferAccount(PublicKey tokenAccount, PublicKey mint,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        return Instruction([27, 3], keys);
    }

    public static TransactionInstruction EmptyConfidentialTransferAccount(PublicKey tokenAccount, PublicKey authority,
        Token2022ProofLocation zeroCiphertextProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = ConfidentialKeys(1, authority, multisigSigners, new(1, zeroCiphertextProof));
        keys[0] = AccountMeta.Writable(tokenAccount);
        return Instruction(new FluentSerializer(3).Write(27).Write(4).WriteInteger(zeroCiphertextProof.Offset).Build(), keys);
    }

    public static TransactionInstruction ConfidentialDeposit(PublicKey tokenAccount, PublicKey mint, ulong amount,
        byte decimals, PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(2, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        return Instruction(new FluentSerializer(11).Write(27).Write(5).WriteInteger(amount).Write(decimals).Build(), keys);
    }

    public static TransactionInstruction ConfidentialWithdraw(PublicKey tokenAccount, PublicKey mint, ulong amount,
        byte decimals, ReadOnlySpan<byte> newDecryptableAvailableBalance, PublicKey authority,
        Token2022ProofLocation equalityProof, Token2022ProofLocation rangeProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(newDecryptableAvailableBalance, 36, nameof(newDecryptableAvailableBalance));
        var keys = ConfidentialKeys(2, authority, multisigSigners, new(2, equalityProof, rangeProof));
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        return Instruction(new FluentSerializer(49).Write(27).Write(6).WriteInteger(amount).Write(decimals)
            .Write(newDecryptableAvailableBalance).WriteInteger(equalityProof.Offset).WriteInteger(rangeProof.Offset).Build(), keys);
    }

    public static TransactionInstruction ConfidentialTransfer(PublicKey sourceTokenAccount, PublicKey mint,
        PublicKey destinationTokenAccount, ReadOnlySpan<byte> newSourceDecryptableAvailableBalance,
        ReadOnlySpan<byte> transferAmountAuditorCiphertextLo, ReadOnlySpan<byte> transferAmountAuditorCiphertextHi,
        PublicKey authority, Token2022ProofLocation equalityProof, Token2022ProofLocation ciphertextValidityProof,
        Token2022ProofLocation rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default,
        ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        var data = ConfidentialAmountData(27, 7, newSourceDecryptableAvailableBalance,
            transferAmountAuditorCiphertextLo, transferAmountAuditorCiphertextHi,
            new(3, equalityProof, ciphertextValidityProof, rangeProof));
        var keys = ConfidentialKeys(3, authority, multisigSigners,
            new(3, equalityProof, ciphertextValidityProof, rangeProof), remainingAccounts);
        keys[0] = AccountMeta.Writable(sourceTokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        keys[2] = AccountMeta.Writable(destinationTokenAccount);
        return Instruction(data, keys);
    }

    public static TransactionInstruction ApplyConfidentialPendingBalance(PublicKey tokenAccount,
        ulong expectedPendingBalanceCreditCounter, ReadOnlySpan<byte> newDecryptableAvailableBalance,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(newDecryptableAvailableBalance, 36, nameof(newDecryptableAvailableBalance));
        var keys = AuthorityKeys(1, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(tokenAccount);
        return Instruction(new FluentSerializer(46).Write(27).Write(8).WriteInteger(expectedPendingBalanceCreditCounter)
            .Write(newDecryptableAvailableBalance).Build(), keys);
    }

    public static TransactionInstruction EnableConfidentialCredits(PublicKey tokenAccount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(27, 9, tokenAccount, authority, multisigSigners);

    public static TransactionInstruction DisableConfidentialCredits(PublicKey tokenAccount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(27, 10, tokenAccount, authority, multisigSigners);

    public static TransactionInstruction EnableNonConfidentialCredits(PublicKey tokenAccount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(27, 11, tokenAccount, authority, multisigSigners);

    public static TransactionInstruction DisableNonConfidentialCredits(PublicKey tokenAccount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(27, 12, tokenAccount, authority, multisigSigners);

    public static TransactionInstruction ConfidentialTransferWithFee(PublicKey sourceTokenAccount, PublicKey mint,
        PublicKey destinationTokenAccount, ReadOnlySpan<byte> newSourceDecryptableAvailableBalance,
        ReadOnlySpan<byte> transferAmountAuditorCiphertextLo, ReadOnlySpan<byte> transferAmountAuditorCiphertextHi,
        PublicKey authority, Token2022ProofLocation equalityProof, Token2022ProofLocation transferAmountCiphertextValidityProof,
        Token2022ProofLocation percentageWithCapProof, Token2022ProofLocation feeCiphertextValidityProof,
        Token2022ProofLocation rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default,
        ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        var proofs = new ConfidentialProofSet(5, equalityProof, transferAmountCiphertextValidityProof,
            percentageWithCapProof, feeCiphertextValidityProof, rangeProof);
        var data = ConfidentialAmountData(27, 13, newSourceDecryptableAvailableBalance,
            transferAmountAuditorCiphertextLo, transferAmountAuditorCiphertextHi, proofs);
        var keys = ConfidentialKeys(3, authority, multisigSigners, proofs, remainingAccounts);
        keys[0] = AccountMeta.Writable(sourceTokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        keys[2] = AccountMeta.Writable(destinationTokenAccount);
        return Instruction(data, keys);
    }

    public static TransactionInstruction ConfigureConfidentialTransferAccountWithRegistry(PublicKey tokenAccount,
        PublicKey mint, PublicKey elGamalRegistryAccount, PublicKey? payer = null)
    {
        AccountMeta[] keys = new AccountMeta[payer is null ? 3 : 5];
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.ReadOnly(mint);
        keys[2] = AccountMeta.ReadOnly(elGamalRegistryAccount);
        if (payer is not null)
        {
            keys[3] = AccountMeta.Writable(payer, true);
            keys[4] = AccountMeta.ReadOnly(SystemProgram.ProgramId);
        }
        return Instruction([27, 14], keys);
    }

    private static void ConfidentialLength(ReadOnlySpan<byte> value, int length, string name)
    {
        if (value.Length != length) throw new ArgumentException($"Expected {length} bytes.", name);
    }

    private static void ConfidentialOptionalPubkey(ReadOnlySpan<byte> value, string name)
    {
        if (!value.IsEmpty) ConfidentialLength(value, 32, name);
    }

    private readonly struct ConfidentialProofSet(int length, Token2022ProofLocation first,
        Token2022ProofLocation second = default, Token2022ProofLocation third = default,
        Token2022ProofLocation fourth = default, Token2022ProofLocation fifth = default)
    {
        public int Length => length;
        public Token2022ProofLocation this[int index] => index switch
        {
            0 => first,
            1 => second,
            2 => third,
            3 => fourth,
            4 => fifth,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    private static AccountMeta[] ConfidentialKeys(int prefixCount, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners, ConfidentialProofSet proofs,
        ReadOnlySpan<AccountMeta> remainingAccounts = default, PublicKey? permissionedBurnAuthority = null)
    {
        var contexts = 0;
        var hasInstruction = false;
        for (var i = 0; i < proofs.Length; i++)
        {
            var proof = proofs[i];
            proof.Validate();
            if (proof.Offset == 0) contexts++; else hasInstruction = true;
        }
        var next = prefixCount;
        var authorityIndex = prefixCount + contexts + (hasInstruction ? 1 : 0) + (permissionedBurnAuthority is null ? 0 : 1);
        var keys = new AccountMeta[authorityIndex + 1 + multisigSigners.Length + remainingAccounts.Length];
        if (hasInstruction) keys[next++] = AccountMeta.ReadOnly(SysVar.Instructions);
        for (var i = 0; i < proofs.Length; i++)
            if (proofs[i].Offset == 0) keys[next++] = AccountMeta.ReadOnly(proofs[i].ContextState!);
        if (permissionedBurnAuthority is not null)
            keys[next++] = AccountMeta.ReadOnly(permissionedBurnAuthority, true);
        keys[next++] = AccountMeta.ReadOnly(authority, multisigSigners.IsEmpty);
        foreach (var signer in multisigSigners) keys[next++] = AccountMeta.ReadOnly(signer, true);
        remainingAccounts.CopyTo(keys.AsSpan(next));
        return keys;
    }

    private static byte[] ConfidentialAmountData(byte family, byte variant, ReadOnlySpan<byte> decryptableBalance,
        ReadOnlySpan<byte> auditorCiphertextLo, ReadOnlySpan<byte> auditorCiphertextHi,
        ConfidentialProofSet proofs)
    {
        ConfidentialLength(decryptableBalance, 36, nameof(decryptableBalance));
        ConfidentialLength(auditorCiphertextLo, 64, nameof(auditorCiphertextLo));
        ConfidentialLength(auditorCiphertextHi, 64, nameof(auditorCiphertextHi));
        var data = new FluentSerializer(166 + proofs.Length).Write(family).Write(variant).Write(decryptableBalance)
            .Write(auditorCiphertextLo).Write(auditorCiphertextHi);
        for (var i = 0; i < proofs.Length; i++) data.WriteInteger(proofs[i].Offset);
        return data.Build();
    }

    private static TransactionInstruction ConfidentialToggle(byte family, byte variant, PublicKey account,
        PublicKey authority, ReadOnlySpan<PublicKey> multisigSigners)
    {
        var keys = AuthorityKeys(1, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(account);
        return Instruction([family, variant], keys);
    }
}
