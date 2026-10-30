using Solaris.Base.Account;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public readonly ref struct Token2022ProofInput
{
    private readonly byte _kind;
    private readonly PublicKey? _account;
    private readonly ReadOnlySpan<byte> _data;
    private readonly uint _recordOffset;
    private readonly Token2022ProofContext? _outputContext;

    private Token2022ProofInput(byte kind, PublicKey? account, ReadOnlySpan<byte> data, uint recordOffset,
        Token2022ProofContext? outputContext)
    {
        _kind = kind;
        _account = account;
        _data = data;
        _recordOffset = recordOffset;
        _outputContext = outputContext;
    }

    public static Token2022ProofInput ContextStateAccount(PublicKey contextState)
    {
        ArgumentNullException.ThrowIfNull(contextState);
        return new(1, contextState, default, 0, null);
    }

    public static Token2022ProofInput Inline(ReadOnlySpan<byte> proofData, Token2022ProofContext? outputContext = null)
        => new(2, null, proofData, 0, outputContext);

    public static Token2022ProofInput RecordAccount(PublicKey proofRecord, uint offset,
        Token2022ProofContext outputContext)
    {
        ArgumentNullException.ThrowIfNull(proofRecord);
        ArgumentNullException.ThrowIfNull(outputContext.Account);
        ArgumentNullException.ThrowIfNull(outputContext.Authority);
        return new(3, proofRecord, default, offset, outputContext);
    }

    internal bool IsContext => _kind == 1;
    internal bool IsRecord => _kind == 3;
    internal PublicKey? Record => _kind == 3 ? _account : null;
    internal ReadOnlySpan<byte> Data => _data;
    internal uint RecordOffset => _recordOffset;
    internal Token2022ProofContext? OutputContext => _outputContext;

    internal Token2022ProofLocation Resolve(ref sbyte nextOffset)
    {
        if (_kind == 1) return Token2022ProofLocation.ContextStateAccount(_account!);
        if (_kind == 3) return Token2022ProofLocation.ContextStateAccount(_outputContext!.Value.Account);
        if (_kind != 2) throw new ArgumentException("A proof input must specify inline data, a record, or a context state.");
        return Token2022ProofLocation.InstructionOffset(nextOffset++);
    }
}

public static partial class Token2022Program
{
    public static TransactionInstruction[] ConfigureConfidentialTransferAccount(PublicKey tokenAccount, PublicKey mint,
        ReadOnlySpan<byte> decryptableZeroBalance, ulong maximumPendingBalanceCreditCounter, PublicKey authority,
        Token2022ProofInput pubkeyValidityProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var instruction = ConfigureConfidentialTransferAccount(tokenAccount, mint, decryptableZeroBalance,
            maximumPendingBalanceCreditCounter, authority, pubkeyValidityProof.Resolve(ref next), multisigSigners);
        return ConfidentialBundle(instruction, pubkeyValidityProof, 4);
    }

    public static TransactionInstruction[] EmptyConfidentialTransferAccount(PublicKey tokenAccount, PublicKey authority,
        Token2022ProofInput zeroCiphertextProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var instruction = EmptyConfidentialTransferAccount(tokenAccount, authority,
            zeroCiphertextProof.Resolve(ref next), multisigSigners);
        return ConfidentialBundle(instruction, zeroCiphertextProof, 1);
    }

    public static TransactionInstruction[] ConfidentialWithdraw(PublicKey tokenAccount, PublicKey mint, ulong amount,
        byte decimals, ReadOnlySpan<byte> newDecryptableAvailableBalance, PublicKey authority,
        Token2022ProofInput equalityProof, Token2022ProofInput rangeProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var equality = equalityProof.Resolve(ref next);
        var range = rangeProof.Resolve(ref next);
        var instruction = ConfidentialWithdraw(tokenAccount, mint, amount, decimals, newDecryptableAvailableBalance,
            authority, equality, range, multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 3, rangeProof, 6);
    }

    public static TransactionInstruction[] ConfidentialTransfer(PublicKey sourceTokenAccount, PublicKey mint,
        PublicKey destinationTokenAccount, ReadOnlySpan<byte> newSourceDecryptableAvailableBalance,
        ReadOnlySpan<byte> transferAmountAuditorCiphertextLo, ReadOnlySpan<byte> transferAmountAuditorCiphertextHi,
        PublicKey authority, Token2022ProofInput equalityProof, Token2022ProofInput ciphertextValidityProof,
        Token2022ProofInput rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default,
        ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        sbyte next = 1;
        var equality = equalityProof.Resolve(ref next);
        var validity = ciphertextValidityProof.Resolve(ref next);
        var range = rangeProof.Resolve(ref next);
        var instruction = ConfidentialTransfer(sourceTokenAccount, mint, destinationTokenAccount,
            newSourceDecryptableAvailableBalance, transferAmountAuditorCiphertextLo, transferAmountAuditorCiphertextHi,
            authority, equality, validity, range, multisigSigners, remainingAccounts);
        return ConfidentialBundle(instruction, equalityProof, 3, ciphertextValidityProof, 12, rangeProof, 7);
    }

    public static TransactionInstruction[] ConfidentialTransferWithFee(PublicKey sourceTokenAccount, PublicKey mint,
        PublicKey destinationTokenAccount, ReadOnlySpan<byte> newSourceDecryptableAvailableBalance,
        ReadOnlySpan<byte> transferAmountAuditorCiphertextLo, ReadOnlySpan<byte> transferAmountAuditorCiphertextHi,
        PublicKey authority, Token2022ProofInput equalityProof, Token2022ProofInput transferAmountCiphertextValidityProof,
        Token2022ProofInput percentageWithCapProof, Token2022ProofInput feeCiphertextValidityProof,
        Token2022ProofInput rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default,
        ReadOnlySpan<AccountMeta> remainingAccounts = default)
    {
        sbyte next = 1;
        var equality = equalityProof.Resolve(ref next);
        var transferValidity = transferAmountCiphertextValidityProof.Resolve(ref next);
        var percentage = percentageWithCapProof.Resolve(ref next);
        var feeValidity = feeCiphertextValidityProof.Resolve(ref next);
        var range = rangeProof.Resolve(ref next);
        var instruction = ConfidentialTransferWithFee(sourceTokenAccount, mint, destinationTokenAccount,
            newSourceDecryptableAvailableBalance, transferAmountAuditorCiphertextLo, transferAmountAuditorCiphertextHi,
            authority, equality, transferValidity, percentage, feeValidity, range, multisigSigners, remainingAccounts);
        return ConfidentialBundle(instruction, equalityProof, 3, transferAmountCiphertextValidityProof, 12,
            percentageWithCapProof, 5, feeCiphertextValidityProof, 10, rangeProof, 8);
    }

    public static TransactionInstruction[] WithdrawConfidentialWithheldTokensFromMint(PublicKey mint, PublicKey destination,
        ReadOnlySpan<byte> newDecryptableAvailableBalance, PublicKey authority, Token2022ProofInput equalityProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var instruction = WithdrawConfidentialWithheldTokensFromMint(mint, destination, newDecryptableAvailableBalance,
            authority, equalityProof.Resolve(ref next), multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 2);
    }

    public static TransactionInstruction[] WithdrawConfidentialWithheldTokensFromAccounts(PublicKey mint,
        PublicKey destination, ReadOnlySpan<byte> newDecryptableAvailableBalance, PublicKey authority,
        ReadOnlySpan<PublicKey> sources, Token2022ProofInput equalityProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var instruction = WithdrawConfidentialWithheldTokensFromAccounts(mint, destination,
            newDecryptableAvailableBalance, authority, sources, equalityProof.Resolve(ref next), multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 2);
    }

    public static TransactionInstruction[] RotateConfidentialSupplyElGamalPubkey(PublicKey mint, PublicKey authority,
        ReadOnlySpan<byte> newSupplyElGamalPubkey, Token2022ProofInput equalityProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var instruction = RotateConfidentialSupplyElGamalPubkey(mint, authority, newSupplyElGamalPubkey,
            equalityProof.Resolve(ref next), multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 2);
    }

    public static TransactionInstruction[] ConfidentialMint(PublicKey tokenAccount, PublicKey mint,
        ReadOnlySpan<byte> newDecryptableSupply, ReadOnlySpan<byte> mintAmountAuditorCiphertextLo,
        ReadOnlySpan<byte> mintAmountAuditorCiphertextHi, PublicKey authority,
        Token2022ProofInput equalityProof, Token2022ProofInput ciphertextValidityProof,
        Token2022ProofInput rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var equality = equalityProof.Resolve(ref next);
        var validity = ciphertextValidityProof.Resolve(ref next);
        var range = rangeProof.Resolve(ref next);
        var instruction = ConfidentialMint(tokenAccount, mint, newDecryptableSupply, mintAmountAuditorCiphertextLo,
            mintAmountAuditorCiphertextHi, authority, equality, validity, range, multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 3, ciphertextValidityProof, 12, rangeProof, 7);
    }

    public static TransactionInstruction[] ConfidentialBurn(PublicKey tokenAccount, PublicKey mint,
        ReadOnlySpan<byte> newDecryptableAvailableBalance, ReadOnlySpan<byte> burnAmountAuditorCiphertextLo,
        ReadOnlySpan<byte> burnAmountAuditorCiphertextHi, PublicKey authority,
        Token2022ProofInput equalityProof, Token2022ProofInput ciphertextValidityProof,
        Token2022ProofInput rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var equality = equalityProof.Resolve(ref next);
        var validity = ciphertextValidityProof.Resolve(ref next);
        var range = rangeProof.Resolve(ref next);
        var instruction = ConfidentialBurn(tokenAccount, mint, newDecryptableAvailableBalance, burnAmountAuditorCiphertextLo,
            burnAmountAuditorCiphertextHi, authority, equality, validity, range, multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 3, ciphertextValidityProof, 12, rangeProof, 7);
    }

    public static TransactionInstruction[] PermissionedConfidentialBurn(PublicKey tokenAccount, PublicKey mint,
        PublicKey permissionedBurnAuthority, ReadOnlySpan<byte> newDecryptableAvailableBalance,
        ReadOnlySpan<byte> burnAmountAuditorCiphertextLo, ReadOnlySpan<byte> burnAmountAuditorCiphertextHi,
        PublicKey authority, Token2022ProofInput equalityProof, Token2022ProofInput ciphertextValidityProof,
        Token2022ProofInput rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        sbyte next = 1;
        var equality = equalityProof.Resolve(ref next);
        var validity = ciphertextValidityProof.Resolve(ref next);
        var range = rangeProof.Resolve(ref next);
        var instruction = PermissionedConfidentialBurn(tokenAccount, mint, permissionedBurnAuthority,
            newDecryptableAvailableBalance, burnAmountAuditorCiphertextLo, burnAmountAuditorCiphertextHi,
            authority, equality, validity, range, multisigSigners);
        return ConfidentialBundle(instruction, equalityProof, 3, ciphertextValidityProof, 12, rangeProof, 7);
    }

    private static TransactionInstruction[] ConfidentialBundle(TransactionInstruction instruction,
        Token2022ProofInput first, byte firstType, Token2022ProofInput second = default, byte secondType = 0,
        Token2022ProofInput third = default, byte thirdType = 0, Token2022ProofInput fourth = default,
        byte fourthType = 0, Token2022ProofInput fifth = default, byte fifthType = 0)
    {
        var count = 1 + (first.IsContext ? 0 : 1) + (secondType != 0 && !second.IsContext ? 1 : 0)
            + (thirdType != 0 && !third.IsContext ? 1 : 0) + (fourthType != 0 && !fourth.IsContext ? 1 : 0)
            + (fifthType != 0 && !fifth.IsContext ? 1 : 0);
        var result = new TransactionInstruction[count];
        var next = 0;
        if (first.IsRecord) ConfidentialAppendProof(result, ref next, first, firstType);
        if (secondType != 0 && second.IsRecord) ConfidentialAppendProof(result, ref next, second, secondType);
        if (thirdType != 0 && third.IsRecord) ConfidentialAppendProof(result, ref next, third, thirdType);
        if (fourthType != 0 && fourth.IsRecord) ConfidentialAppendProof(result, ref next, fourth, fourthType);
        if (fifthType != 0 && fifth.IsRecord) ConfidentialAppendProof(result, ref next, fifth, fifthType);
        result[next++] = instruction;
        if (!first.IsRecord) ConfidentialAppendProof(result, ref next, first, firstType);
        if (secondType != 0 && !second.IsRecord) ConfidentialAppendProof(result, ref next, second, secondType);
        if (thirdType != 0 && !third.IsRecord) ConfidentialAppendProof(result, ref next, third, thirdType);
        if (fourthType != 0 && !fourth.IsRecord) ConfidentialAppendProof(result, ref next, fourth, fourthType);
        if (fifthType != 0 && !fifth.IsRecord) ConfidentialAppendProof(result, ref next, fifth, fifthType);
        return result;
    }

    private static void ConfidentialAppendProof(TransactionInstruction[] instructions, ref int next,
        Token2022ProofInput proof, byte discriminator)
    {
        if (proof.IsContext) return;
        var length = discriminator switch
        {
            1 => 192,
            2 => 416,
            3 => 320,
            4 => 96,
            5 => 360,
            6 => 936,
            7 => 1000,
            8 => 1064,
            10 => 416,
            12 => 544,
            _ => throw new ArgumentOutOfRangeException(nameof(discriminator))
        };
        instructions[next++] = proof.Record is { } record
            ? ConfidentialVerifyRecord(discriminator, record, proof.RecordOffset, proof.OutputContext)
            : ConfidentialVerifyProof(discriminator, length, proof.Data, proof.OutputContext);
    }
}
