using Solaris.Base.Account;
using Solaris.Transactions.LowLevel;

namespace Solaris.Transactions.Models;

public class Transaction
{
    public PublicKey? BlockHash;
    public PrivateKey? ExternalFeePayer;
    public TransactionInstruction[]? Instructions;
    public PrivateKey[]? Signers;

    public CompiledMessage BuildMessage()
    {
        if (BlockHash == null) throw new Exception("Recent blockhash should not be null");
        if (Instructions == null || Instructions.Length == 0) throw new Exception("Instructions array sould not be null nor empty");
        if (Signers == null || Signers.Length == 0) throw new Exception("Signers sould not be null nor empty");

        var msg = new CompiledMessage();

        var keys = CollectKeys();
        FillKeys(keys, ref msg);
        msg.RecentBlockHash = BlockHash;

        var idxMap = new Dictionary<PublicKey, byte>(msg.Accounts.Length);
        for (byte i = 0; i < msg.Accounts.Length; i++)
            idxMap[msg.Accounts[i]] = i;

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

        if (ExternalFeePayer != null && !Signers.Contains(ExternalFeePayer))
            Signers = [ExternalFeePayer, ..Signers];

        return msg;
    }

    private Dictionary<PublicKey, CompiledAccountMeta> CollectKeys()
    {
        var accountMetaMap = new Dictionary<PublicKey, CompiledAccountMeta>(Instructions!.Sum(x => x.Keys.Length + 1));
        foreach (var ix in Instructions!)
        {
            if (accountMetaMap.TryGetValue(ix.ProgramId, out var meta))
                meta.IsInvoked = true;
            else
                accountMetaMap[ix.ProgramId] = new CompiledAccountMeta { IsInvoked = true };

            foreach (var key in ix.Keys)
                if (accountMetaMap.TryGetValue(key.Account, out meta))
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

        var sorted = new PublicKey[writableSigners.Count + readonlySigners.Count + writableNonSigners.Count + readonlyNonSigners.Count];
        var sortedSpan = sorted.AsSpan();

        writableSigners.CopyTo(sortedSpan);
        readonlySigners.CopyTo(sortedSpan.Slice(writableSigners.Count, readonlySigners.Count));
        writableNonSigners.CopyTo(sortedSpan.Slice(readonlySigners.Count + writableSigners.Count, writableNonSigners.Count));
        readonlyNonSigners.CopyTo(sortedSpan.Slice(writableNonSigners.Count + readonlySigners.Count + writableSigners.Count, readonlyNonSigners.Count));

        msg.Accounts = sorted;
    }
}