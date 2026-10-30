using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeConfidentialMintBurn(PublicKey mint,
        ReadOnlySpan<byte> supplyElGamalPubkey, ReadOnlySpan<byte> decryptableSupply)
    {
        ConfidentialLength(supplyElGamalPubkey, 32, nameof(supplyElGamalPubkey));
        ConfidentialLength(decryptableSupply, 36, nameof(decryptableSupply));
        return Instruction(new FluentSerializer(70).Write(42).Write(0).Write(supplyElGamalPubkey)
            .Write(decryptableSupply).Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction RotateConfidentialSupplyElGamalPubkey(PublicKey mint, PublicKey authority,
        ReadOnlySpan<byte> newSupplyElGamalPubkey, Token2022ProofLocation equalityProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(newSupplyElGamalPubkey, 32, nameof(newSupplyElGamalPubkey));
        var keys = ConfidentialKeys(1, authority, multisigSigners, new ConfidentialProofSet(1, equalityProof));
        keys[0] = AccountMeta.Writable(mint);
        return Instruction(new FluentSerializer(35).Write(42).Write(1).Write(newSupplyElGamalPubkey)
            .WriteInteger(equalityProof.Offset).Build(), keys);
    }

    public static TransactionInstruction UpdateConfidentialDecryptableSupply(PublicKey mint, PublicKey authority,
        ReadOnlySpan<byte> newDecryptableSupply, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(newDecryptableSupply, 36, nameof(newDecryptableSupply));
        var keys = AuthorityKeys(1, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(mint);
        return Instruction(new FluentSerializer(38).Write(42).Write(2).Write(newDecryptableSupply).Build(), keys);
    }

    public static TransactionInstruction ConfidentialMint(PublicKey tokenAccount, PublicKey mint,
        ReadOnlySpan<byte> newDecryptableSupply, ReadOnlySpan<byte> mintAmountAuditorCiphertextLo,
        ReadOnlySpan<byte> mintAmountAuditorCiphertextHi, PublicKey authority,
        Token2022ProofLocation equalityProof, Token2022ProofLocation ciphertextValidityProof,
        Token2022ProofLocation rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var data = ConfidentialAmountData(42, 3, newDecryptableSupply, mintAmountAuditorCiphertextLo,
            mintAmountAuditorCiphertextHi, new ConfidentialProofSet(3, equalityProof, ciphertextValidityProof, rangeProof));
        var keys = ConfidentialKeys(2, authority, multisigSigners, new ConfidentialProofSet(3, equalityProof, ciphertextValidityProof, rangeProof));
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.Writable(mint);
        return Instruction(data, keys);
    }

    public static TransactionInstruction ConfidentialBurn(PublicKey tokenAccount, PublicKey mint,
        ReadOnlySpan<byte> newDecryptableAvailableBalance, ReadOnlySpan<byte> burnAmountAuditorCiphertextLo,
        ReadOnlySpan<byte> burnAmountAuditorCiphertextHi, PublicKey authority,
        Token2022ProofLocation equalityProof, Token2022ProofLocation ciphertextValidityProof,
        Token2022ProofLocation rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var data = ConfidentialAmountData(42, 4, newDecryptableAvailableBalance, burnAmountAuditorCiphertextLo,
            burnAmountAuditorCiphertextHi, new ConfidentialProofSet(3, equalityProof, ciphertextValidityProof, rangeProof));
        var keys = ConfidentialKeys(2, authority, multisigSigners, new ConfidentialProofSet(3, equalityProof, ciphertextValidityProof, rangeProof));
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.Writable(mint);
        return Instruction(data, keys);
    }

    public static TransactionInstruction ApplyConfidentialPendingBurn(PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(42, 5, mint, authority, multisigSigners);

    public static TransactionInstruction InitializePermissionedBurn(PublicKey mint, PublicKey authority)
    {
        return Instruction(new FluentSerializer(34).Write(46).Write(0).Write(authority).Build(),
            [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction PermissionedBurn(PublicKey tokenAccount, PublicKey mint,
        PublicKey permissionedBurnAuthority, PublicKey authority, ulong amount,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(3, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.Writable(mint);
        keys[2] = AccountMeta.ReadOnly(permissionedBurnAuthority, true);
        return Instruction(new FluentSerializer(10).Write(46).Write(1).WriteInteger(amount).Build(), keys);
    }

    public static TransactionInstruction PermissionedBurnChecked(PublicKey tokenAccount, PublicKey mint,
        PublicKey permissionedBurnAuthority, PublicKey authority, ulong amount, byte decimals,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var keys = AuthorityKeys(3, authority, multisigSigners);
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.Writable(mint);
        keys[2] = AccountMeta.ReadOnly(permissionedBurnAuthority, true);
        return Instruction(new FluentSerializer(11).Write(46).Write(2).WriteInteger(amount).Write(decimals).Build(), keys);
    }

    public static TransactionInstruction PermissionedConfidentialBurn(PublicKey tokenAccount, PublicKey mint,
        PublicKey permissionedBurnAuthority, ReadOnlySpan<byte> newDecryptableAvailableBalance,
        ReadOnlySpan<byte> burnAmountAuditorCiphertextLo, ReadOnlySpan<byte> burnAmountAuditorCiphertextHi,
        PublicKey authority, Token2022ProofLocation equalityProof, Token2022ProofLocation ciphertextValidityProof,
        Token2022ProofLocation rangeProof, ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        var data = ConfidentialAmountData(46, 3, newDecryptableAvailableBalance, burnAmountAuditorCiphertextLo,
            burnAmountAuditorCiphertextHi, new ConfidentialProofSet(3, equalityProof, ciphertextValidityProof, rangeProof));
        var keys = ConfidentialKeys(2, authority, multisigSigners, new ConfidentialProofSet(3, equalityProof, ciphertextValidityProof, rangeProof),
            permissionedBurnAuthority: permissionedBurnAuthority);
        keys[0] = AccountMeta.Writable(tokenAccount);
        keys[1] = AccountMeta.Writable(mint);
        return Instruction(data, keys);
    }
}
