using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeConfidentialTransferFeeConfig(PublicKey mint, PublicKey? authority,
        ReadOnlySpan<byte> withdrawWithheldAuthorityElGamalPubkey)
    {
        ConfidentialLength(withdrawWithheldAuthorityElGamalPubkey, 32, nameof(withdrawWithheldAuthorityElGamalPubkey));
        return Instruction(new FluentSerializer(66).Write(37).Write(0).Write(authority ?? SystemProgram.ProgramId)
            .Write(withdrawWithheldAuthorityElGamalPubkey).Build(), [AccountMeta.Writable(mint)]);
    }

    public static TransactionInstruction WithdrawConfidentialWithheldTokensFromMint(PublicKey mint, PublicKey destination,
        ReadOnlySpan<byte> newDecryptableAvailableBalance, PublicKey authority, Token2022ProofLocation equalityProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(newDecryptableAvailableBalance, 36, nameof(newDecryptableAvailableBalance));
        var keys = ConfidentialKeys(2, authority, multisigSigners, new ConfidentialProofSet(1, equalityProof));
        keys[0] = AccountMeta.Writable(mint);
        keys[1] = AccountMeta.Writable(destination);
        return Instruction(new FluentSerializer(39).Write(37).Write(1).WriteInteger(equalityProof.Offset)
            .Write(newDecryptableAvailableBalance).Build(), keys);
    }

    public static TransactionInstruction WithdrawConfidentialWithheldTokensFromAccounts(PublicKey mint,
        PublicKey destination, ReadOnlySpan<byte> newDecryptableAvailableBalance, PublicKey authority,
        ReadOnlySpan<PublicKey> sources, Token2022ProofLocation equalityProof,
        ReadOnlySpan<PublicKey> multisigSigners = default)
    {
        ConfidentialLength(newDecryptableAvailableBalance, 36, nameof(newDecryptableAvailableBalance));
        if (sources.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(sources));
        equalityProof.Validate();
        var keys = new AccountMeta[4 + multisigSigners.Length + sources.Length];
        keys[0] = AccountMeta.Writable(mint);
        keys[1] = AccountMeta.Writable(destination);
        keys[2] = AccountMeta.ReadOnly(equalityProof.Offset == 0 ? equalityProof.ContextState! : SysVar.Instructions);
        keys[3] = AccountMeta.ReadOnly(authority, multisigSigners.IsEmpty);
        var next = 4;
        foreach (var signer in multisigSigners) keys[next++] = AccountMeta.ReadOnly(signer, true);
        foreach (var source in sources) keys[next++] = AccountMeta.Writable(source);
        return Instruction(new FluentSerializer(40).Write(37).Write(2).Write((byte)sources.Length)
            .WriteInteger(equalityProof.Offset).Write(newDecryptableAvailableBalance).Build(), keys);
    }

    public static TransactionInstruction HarvestConfidentialWithheldTokensToMint(PublicKey mint,
        ReadOnlySpan<PublicKey> sources)
    {
        var keys = new AccountMeta[1 + sources.Length];
        keys[0] = AccountMeta.Writable(mint);
        for (var i = 0; i < sources.Length; i++) keys[i + 1] = AccountMeta.Writable(sources[i]);
        return Instruction([37, 3], keys);
    }

    public static TransactionInstruction EnableConfidentialHarvestToMint(PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(37, 4, mint, authority, multisigSigners);

    public static TransactionInstruction DisableConfidentialHarvestToMint(PublicKey mint, PublicKey authority,
        ReadOnlySpan<PublicKey> multisigSigners = default) => ConfidentialToggle(37, 5, mint, authority, multisigSigners);
}
