using System.Runtime.InteropServices;
using Solaris.Base.Account;
using Solaris.Transactions.LowLevel;

namespace Solaris.Transactions.Models;

public class Transaction
{
    public PublicKey? BlockHash;
    public PrivateKey? ExternalFeePayer;
    public TransactionInstruction[]? Instructions;
    public PrivateKey[]? Signers;
    public byte? Version;
    public AddressLookupTableAccount[] AddressLookupTables = [];

    public CompiledMessage BuildMessage()
    {
        ArgumentNullException.ThrowIfNull(BlockHash);
        ArgumentNullException.ThrowIfNull(Instructions);
        if (Version is not null and not 0)
            throw new NotSupportedException($"Transaction version {Version} is not supported.");
        if (Version is null && AddressLookupTables.Length != 0)
            throw new InvalidOperationException("Address lookups require a version 0 message.");
        var payer = ExternalFeePayer?.PublicKey ?? (Signers is { Length: > 0 } ? Signers[0].PublicKey : null);
        if (payer is null) throw new InvalidOperationException("A fee payer is required.");

        var capacity = 1;
        foreach (var instruction in Instructions) capacity += instruction.Keys.Length + 1;
        var accounts = new Dictionary<PublicKey, AccountState>(Math.Min(capacity, 256));
        accounts.Add(payer, new AccountState { IsSigner = true, IsWritable = true });
        foreach (var instruction in Instructions)
        {
            ref var program = ref CollectionsMarshal.GetValueRefOrAddDefault(accounts, instruction.ProgramId, out _);
            program.IsInvoked = true;
            foreach (var key in instruction.Keys)
            {
                ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(accounts, key.Account, out _);
                state.IsSigner |= key.IsSigner;
                state.IsWritable |= key.IsWritable;
            }
        }
        if (accounts.Count > 256) throw new InvalidOperationException("A message cannot reference more than 256 accounts.");

        var lookups = AddressLookupTables.Length == 0 ? [] : new MessageAddressTableLookup[AddressLookupTables.Length];
        var lookupCount = 0;
        var writableLoaded = 0;
        var readonlyLoaded = 0;
        Span<byte> writable = stackalloc byte[256];
        Span<byte> readOnly = stackalloc byte[256];
        for (var t = 0; t < AddressLookupTables.Length; t++)
        {
            var table = AddressLookupTables[t];
            if (table.Addresses.Length > 256) throw new ArgumentException("A lookup table cannot contain more than 256 addresses.");
            for (var i = 0; i < table.Addresses.Length; i++)
            {
                ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(accounts, table.Addresses[i]);
                if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref state) || state.IsSigner || state.IsInvoked || state.LookupTable != 0)
                    continue;
                state.LookupTable = t + 1;
                state.TableIndex = (byte)i;
            }
            var writableCount = 0;
            var readonlyCount = 0;
            foreach (var pair in accounts)
            {
                if (pair.Value.LookupTable != t + 1) continue;
                ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(accounts, pair.Key);
                if (state.IsWritable)
                {
                    writable[writableCount++] = state.TableIndex;
                    state.Index = writableLoaded++;
                }
                else
                {
                    readOnly[readonlyCount++] = state.TableIndex;
                    state.Index = readonlyLoaded++;
                }
            }
            if (writableCount + readonlyCount != 0)
                lookups[lookupCount++] = new MessageAddressTableLookup(table.Key, writable[..writableCount].ToArray(), readOnly[..readonlyCount].ToArray());
        }

        Span<int> counts = stackalloc int[4];
        counts.Clear();
        foreach (var state in accounts.Values)
            if (state.LookupTable == 0) counts[Category(state)]++;
        var requiredSignatures = counts[0] + counts[1];
        if (requiredSignatures > 127) throw new InvalidOperationException("A message cannot require more than 127 signatures.");
        var staticCount = accounts.Count - writableLoaded - readonlyLoaded;
        var staticKeys = new PublicKey[staticCount];
        Span<int> offsets = stackalloc int[4] { 0, counts[0], requiredSignatures, requiredSignatures + counts[2] };
        foreach (var pair in accounts)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(accounts, pair.Key);
            if (state.LookupTable == 0)
            {
                state.Index = offsets[Category(state)]++;
                staticKeys[state.Index] = pair.Key;
            }
            else state.Index += staticCount + (state.IsWritable ? 0 : writableLoaded);
        }

        var compiled = new CompiledInstruction[Instructions.Length];
        for (var i = 0; i < compiled.Length; i++)
        {
            var instruction = Instructions[i];
            var indices = new byte[instruction.Keys.Length];
            for (var j = 0; j < indices.Length; j++) indices[j] = (byte)accounts[instruction.Keys[j].Account].Index;
            compiled[i] = new CompiledInstruction
            {
                ProgramIdIndex = (byte)accounts[instruction.ProgramId].Index,
                KeyIndices = indices,
                Data = instruction.Data
            };
        }
        if (lookupCount != lookups.Length) Array.Resize(ref lookups, lookupCount);
        return new CompiledMessage
        {
            Version = Version,
            Header = new CompiledMessageHeader
            {
                RequiredSignatures = (byte)requiredSignatures,
                ReadOnlySignedAccounts = (byte)counts[1],
                ReadOnlyUnsignedAccounts = checked((byte)counts[3])
            },
            Accounts = staticKeys,
            RecentBlockHash = BlockHash,
            Instructions = compiled,
            AddressTableLookups = lookups
        };
    }

    public byte[] Build()
    {
        var message = BuildMessage();
        return message.BuildTransaction(Signers ?? [], ExternalFeePayer);
    }

    private static int Category(AccountState state) => state.IsSigner ? (state.IsWritable ? 0 : 1) : (state.IsWritable ? 2 : 3);

    private struct AccountState
    {
        public bool IsSigner;
        public bool IsWritable;
        public bool IsInvoked;
        public int LookupTable;
        public byte TableIndex;
        public int Index;
    }
}
