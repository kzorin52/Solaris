using Solaris.Base.Account;
using Solaris.Transactions.LowLevel;

namespace Solaris.Transactions.Models;

/// <summary>
///     Represents a transaction to be submitted to the Solana cluster.
///     A transaction consists of a set of instructions and a list of signatures.
/// </summary>
public class Transaction
{
    /// <summary>
    ///     The recent blockhash to be used for the transaction.
    ///     This serves as a nonce and a time-bound for the transaction validity.
    /// </summary>
    public PublicKey? BlockHash;

    /// <summary>
    ///     Optional external fee payer for the transaction.
    ///     If not specified, the first account in the signers list is treated as the fee payer.
    /// </summary>
    public PrivateKey? ExternalFeePayer;

    /// <summary>
    ///     The list of instructions that make up the transaction.
    ///     These are executed sequentially and atomically.
    /// </summary>
    public TransactionInstruction[]? Instructions;

    /// <summary>
    ///     The list of private keys that will sign the transaction.
    ///     Must include the fee payer and any accounts required to sign by the instructions.
    /// </summary>
    public PrivateKey[]? Signers;

    /// <summary>
    ///     Builds the <see cref="CompiledMessage" /> from the transaction details.
    ///     This process involves collecting all unique accounts, ordering them, and compiling instructions.
    /// </summary>
    /// <returns>The compiled message ready for serialization.</returns>
    /// <exception cref="Exception">Thrown if BlockHash, Instructions, or Signers are null or empty.</exception>
    public CompiledMessage BuildMessage()
    {
        if (BlockHash == null) throw new Exception("Recent blockhash should not be null");
        if (Instructions == null || Instructions.Length == 0)
            throw new Exception("Instructions array sould not be null nor empty");
        if (Signers == null || Signers.Length == 0) throw new Exception("Signers sould not be null nor empty");

        var msg = new CompiledMessage();

        // Collect and organize account keys from all instructions
        var keys = CollectKeys();
        FillKeys(keys, ref msg);
        msg.RecentBlockHash = BlockHash;

        // Map public keys to their indices in the message account list
        var idxMap = new Dictionary<PublicKey, byte>(msg.Accounts.Length);
        for (byte i = 0; i < msg.Accounts.Length; i++)
            idxMap[msg.Accounts[i]] = i;

        // Compile instructions by replacing public keys with their indices
        var instructions = new CompiledInstruction[Instructions.Length];
        for (var i = 0; i < instructions.Length; i++)
        {
            var dynamicInstr = Instructions[i];

            var keysIdx = new byte[dynamicInstr.Keys.Length];
            for (var j = 0; j < dynamicInstr.Keys.Length; j++)
                keysIdx[j] = idxMap[dynamicInstr.Keys[j].Account];

            instructions[i] = new CompiledInstruction
            {
                ProgramIdIndex = idxMap[dynamicInstr.ProgramId],
                KeyIndices = keysIdx,
                Data = dynamicInstr.Data
            };
        }

        msg.Instructions = instructions;

        // Ensure the external fee payer is included in the signers list if specified
        if (ExternalFeePayer != null && !Signers.Contains(ExternalFeePayer))
            Signers = [ExternalFeePayer, ..Signers];

        return msg;
    }

    /// <summary>
    ///     Collects all unique accounts from the instructions and aggregates their metadata (signer/writable/invoked).
    /// </summary>
    /// <returns>A dictionary mapping public keys to their compiled metadata.</returns>
    private Dictionary<PublicKey, CompiledAccountMeta> CollectKeys()
    {
        var accountMetaMap = new Dictionary<PublicKey, CompiledAccountMeta>(Instructions!.Sum(x => x.Keys.Length + 1));
        foreach (var ix in Instructions!)
        {
            // Handle Program ID
            if (!accountMetaMap.ContainsKey(ix.ProgramId))
                accountMetaMap[ix.ProgramId] = new CompiledAccountMeta();

            // Handle accounts involved in the instruction
            foreach (var key in ix.Keys)
                if (accountMetaMap.TryGetValue(key.Account, out var meta))
                {
                    meta.IsSigner |= key.IsSigner;
                    meta.IsWritable |= key.IsWritable;
                }
                else
                {
                    accountMetaMap[key.Account] = CompiledAccountMeta.FromAccountMeta(key);
                }
        }

        return accountMetaMap;
    }

    /// <summary>
    ///     Sorts the accounts according to Solana's rules and populates the message header and account list.
    ///     Ordering: Writable Signers, Read-only Signers, Writable Non-Signers, Read-only Non-Signers.
    /// </summary>
    /// <param name="map">The map of accounts to their metadata.</param>
    /// <param name="msg">The compiled message to update.</param>
    private void FillKeys(Dictionary<PublicKey, CompiledAccountMeta> map, ref CompiledMessage msg)
    {
        var writableSigners = new List<PublicKey>(map.Count);
        var readonlySigners = new List<PublicKey>(map.Count);
        var writableNonSigners = new List<PublicKey>(map.Count);
        var readonlyNonSigners = new List<PublicKey>(map.Count);

        foreach (var pair in map)
            if (pair.Value.IsSigner && pair.Value.IsWritable) writableSigners.Add(pair.Key);
            else if (pair.Value.IsSigner && !pair.Value.IsWritable) readonlySigners.Add(pair.Key);
            else if (!pair.Value.IsSigner && pair.Value.IsWritable) writableNonSigners.Add(pair.Key);
            else if (!pair.Value.IsSigner && !pair.Value.IsWritable) readonlyNonSigners.Add(pair.Key);

        // Ensure ExternalFeePayer is the first signer (index 0)
        if (ExternalFeePayer != null)
        {
            if (writableSigners.IndexOf(ExternalFeePayer) is var idx && idx != -1)
                writableSigners.RemoveAt(idx);
            writableSigners.Insert(0, ExternalFeePayer);
        }

        msg.Header = new CompiledMessageHeader
        {
            RequiredSignatures = (byte)(writableSigners.Count + readonlySigners.Count),
            ReadOnlySignedAccounts = (byte)readonlySigners.Count,
            ReadOnlyUnsignedAccounts = (byte)readonlyNonSigners.Count
        };

        var sorted = new PublicKey[writableSigners.Count + readonlySigners.Count + writableNonSigners.Count +
                                   readonlyNonSigners.Count];
        var sortedSpan = sorted.AsSpan();

        writableSigners.CopyTo(sortedSpan);
        readonlySigners.CopyTo(sortedSpan.Slice(writableSigners.Count, readonlySigners.Count));
        writableNonSigners.CopyTo(sortedSpan.Slice(readonlySigners.Count + writableSigners.Count,
            writableNonSigners.Count));
        readonlyNonSigners.CopyTo(sortedSpan.Slice(
            writableNonSigners.Count + readonlySigners.Count + writableSigners.Count, readonlyNonSigners.Count));

        msg.Accounts = sorted;
    }
}