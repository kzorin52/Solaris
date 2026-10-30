using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public readonly record struct Token2022ProofContext(PublicKey Account, PublicKey Authority);

public static partial class Token2022Program
{
    public static readonly PublicKey ZkElGamalProofProgramId = "ZkE1Gama1Proof11111111111111111111111111111";

    public static TransactionInstruction VerifyZeroCiphertextProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(1, 192, proofData, context);

    public static TransactionInstruction VerifyZeroCiphertextProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(1, proofRecord, offset, context);

    public static TransactionInstruction VerifyCiphertextCiphertextEqualityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(2, 416, proofData, context);

    public static TransactionInstruction VerifyCiphertextCiphertextEqualityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(2, proofRecord, offset, context);

    public static TransactionInstruction VerifyCiphertextCommitmentEqualityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(3, 320, proofData, context);

    public static TransactionInstruction VerifyCiphertextCommitmentEqualityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(3, proofRecord, offset, context);

    public static TransactionInstruction VerifyPubkeyValidityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(4, 96, proofData, context);

    public static TransactionInstruction VerifyPubkeyValidityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(4, proofRecord, offset, context);

    public static TransactionInstruction VerifyPercentageWithCapProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(5, 360, proofData, context);

    public static TransactionInstruction VerifyPercentageWithCapProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(5, proofRecord, offset, context);

    public static TransactionInstruction VerifyBatchedRangeProofU64(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(6, 936, proofData, context);

    public static TransactionInstruction VerifyBatchedRangeProofU64(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(6, proofRecord, offset, context);

    public static TransactionInstruction VerifyBatchedRangeProofU128(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(7, 1000, proofData, context);

    public static TransactionInstruction VerifyBatchedRangeProofU128(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(7, proofRecord, offset, context);

    public static TransactionInstruction VerifyBatchedRangeProofU256(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(8, 1064, proofData, context);

    public static TransactionInstruction VerifyBatchedRangeProofU256(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(8, proofRecord, offset, context);

    public static TransactionInstruction VerifyGroupedCiphertext2HandlesValidityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(9, 320, proofData, context);

    public static TransactionInstruction VerifyGroupedCiphertext2HandlesValidityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(9, proofRecord, offset, context);

    public static TransactionInstruction VerifyBatchedGroupedCiphertext2HandlesValidityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(10, 416, proofData, context);

    public static TransactionInstruction VerifyBatchedGroupedCiphertext2HandlesValidityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(10, proofRecord, offset, context);

    public static TransactionInstruction VerifyGroupedCiphertext3HandlesValidityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(11, 416, proofData, context);

    public static TransactionInstruction VerifyGroupedCiphertext3HandlesValidityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(11, proofRecord, offset, context);

    public static TransactionInstruction VerifyBatchedGroupedCiphertext3HandlesValidityProof(ReadOnlySpan<byte> proofData,
        Token2022ProofContext? context = null) => ConfidentialVerifyProof(12, 544, proofData, context);

    public static TransactionInstruction VerifyBatchedGroupedCiphertext3HandlesValidityProof(PublicKey proofRecord, uint offset,
        Token2022ProofContext? context = null) => ConfidentialVerifyRecord(12, proofRecord, offset, context);

    public static TransactionInstruction CloseConfidentialProofContext(PublicKey contextState, PublicKey destination,
        PublicKey authority)
    {
        return new TransactionInstruction
        {
            ProgramId = ZkElGamalProofProgramId,
            Data = [0],
            Keys = [AccountMeta.Writable(contextState), AccountMeta.Writable(destination), AccountMeta.ReadOnly(authority, true)]
        };
    }

    private static TransactionInstruction ConfidentialVerifyProof(byte discriminator, int length,
        ReadOnlySpan<byte> proofData, Token2022ProofContext? context)
    {
        ConfidentialLength(proofData, length, nameof(proofData));
        return new TransactionInstruction
        {
            ProgramId = ZkElGamalProofProgramId,
            Data = new FluentSerializer(1 + length).Write(discriminator).Write(proofData).Build(),
            Keys = ConfidentialProofKeys(null, context)
        };
    }

    private static TransactionInstruction ConfidentialVerifyRecord(byte discriminator, PublicKey proofRecord,
        uint offset, Token2022ProofContext? context)
    {
        ArgumentNullException.ThrowIfNull(proofRecord);
        return new TransactionInstruction
        {
            ProgramId = ZkElGamalProofProgramId,
            Data = new FluentSerializer(5).Write(discriminator).WriteInteger(offset).Build(),
            Keys = ConfidentialProofKeys(proofRecord, context)
        };
    }

    private static AccountMeta[] ConfidentialProofKeys(PublicKey? record, Token2022ProofContext? context)
    {
        if (record is null && context is null) return [];
        var keys = new AccountMeta[(record is null ? 0 : 1) + (context is null ? 0 : 2)];
        var next = 0;
        if (record is not null) keys[next++] = AccountMeta.ReadOnly(record);
        if (context is { } state)
        {
            ArgumentNullException.ThrowIfNull(state.Account);
            ArgumentNullException.ThrowIfNull(state.Authority);
            keys[next++] = AccountMeta.Writable(state.Account);
            keys[next] = AccountMeta.ReadOnly(state.Authority);
        }
        return keys;
    }
}
