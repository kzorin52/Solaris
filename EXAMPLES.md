# Solaris examples

[README](README.md)

## Contents

- [Keys and PDAs](#public-keys-and-deterministic-derivation)
- [Signed SOL transfer](#build-a-signed-sol-transfer)
- [Serialization buffers](#compile-once-and-reuse-serialization-buffers)
- [Address lookup tables](#decode-an-alt-and-build-a-v0-transaction)
- [Token-2022](#token-2022-account-sizing-and-fee-aware-transfers)
- [Simulate and submit](#rpc-account-checks-simulation-and-submission)
- [Account batches and transaction history](#rpc-account-batches-and-transaction-history)
- [Binary transaction decoding](#decode-transaction-packets-without-copying)

## Using the snippets

Use a `net9.0` project with a recent SDK; see [setup](README.md#getting-started). Reference `Solaris.Rpc/Solaris.Rpc.csproj` for all examples, or `Solaris.Programs/Solaris.Programs.csproj` for offline work.

The keys and SOL transfer snippets are separate top-level programs. Append the buffer snippet to the transfer; the remaining snippets are helper classes. Only the RPC sections contact a cluster, and submission is opt-in.

## Public keys and deterministic derivation

Use `PublicKey` for base58 and instruction APIs; `PublicKeyValue` is a 32-byte value key. Its `AsSpan()` borrows storage; `CopyTo` writes to caller-owned memory.

The System Program address below is a public demonstration seed, not a wallet. Deriving an ATA does not create an account.

```csharp
using System;
using System.Collections.Generic;
using Solaris.Base.Account;
using Solaris.Programs;
using Solaris.Programs.Token;

PublicKey owner = SystemProgram.ProgramId;
PublicKey mint = new("So11111111111111111111111111111111111111112");

PublicKeyValue ownerValue = owner.KeyValue;
var labels = new Dictionary<PublicKeyValue, string>
{
    [ownerValue] = "demonstration owner seed"
};
Span<byte> bytes = stackalloc byte[PublicKey.PublicKeyLength];
ownerValue.CopyTo(bytes);
PublicKey roundTrip = new(bytes);
if (roundTrip != owner)
    throw new InvalidOperationException("Public-key round trip failed.");

PublicKey ata = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
    owner, mint, out byte bump, TokenProgram.ProgramId);

// An ATA is a PDA under the associated-token program, with these three seeds.
var derived = PublicKey.FindProgramAddress(
    AssociatedTokenAccountProgram.ProgramId,
    owner.KeySpan, TokenProgram.ProgramId.KeySpan, mint.KeySpan);
if (derived.Key != ata || derived.Bump != bump)
    throw new InvalidOperationException("ATA derivations disagree.");

Console.WriteLine(labels[ownerValue]);
Console.WriteLine($"ATA: {ata}; bump: {bump}");
```

For other PDAs, use the program's documented seed order and handle a null `Key` result. For Token-2022 ATAs, pass `Token2022Program.ProgramId`: the token program ID is one of the seeds.

## Build a signed SOL transfer

This signs an offline legacy packet with an unfunded key and a dummy blockhash. Do not submit it. A real transfer needs a funded signer, a recent cluster blockhash, and an intended recipient. One SOL is 1,000,000,000 lamports.

```csharp
using System;
using Solaris.Base.Account;
using Solaris.Programs;
using Solaris.Transactions.Models;

var payer = new PrivateKey();
PublicKey recipient = new PrivateKey().PublicKey;
PublicKey offlineBlockhash = new(new byte[PublicKey.PublicKeyLength]);

var transaction = new Transaction
{
    Version = null, // Legacy; use 0 for a v0 message.
    BlockHash = offlineBlockhash,
    ExternalFeePayer = payer,
    Signers = [],
    Instructions =
    [
        SystemProgram.Transfer(payer.PublicKey, recipient, 1_000UL)
    ]
};
byte[] packet = transaction.Build();
Console.WriteLine($"Locally signed packet: {packet.Length} bytes; not submitted.");
```

`ExternalFeePayer` signs separately. Without it, the first `Signers` entry pays the fee. Include all other required keys in `Signers`; wire signature order is determined by the message, not the array.

`PrivateKey` can retain input memory and internal secret-key copies. Keep backing memory unchanged while in use; clearing the input does not erase every copy. Never log secrets.

## Compile once and reuse serialization buffers

`BuildMessage()` separates compilation from serialization and signing. The compiled message contains this transaction's blockhash and instructions. Append this to the transfer above:

```csharp
var message = transaction.BuildMessage();
byte[] buffer = new byte[message.TransactionSize()];
int written = message.SerializeTransaction(buffer, transaction.Signers!, payer);
ReadOnlyMemory<byte> signedPacket = buffer.AsMemory(0, written);

byte[] unsignedMessage = new byte[message.Size()];
message.Serialize(unsignedMessage);
Console.WriteLine($"Message: {unsignedMessage.Length} bytes; packet: {signedPacket.Length} bytes.");
```

`Size()`/`Serialize` cover the unsigned message; `TransactionSize()`/`SerializeTransaction` include signatures. `signedPacket` borrows `buffer`: wait for every consumer, including asynchronous senders, before reusing it.

Compilation retains instruction data and lookup-table arrays. Keep them unchanged; rebuild and re-sign after changing inputs or refreshing an expired blockhash.

## Decode an ALT and build a v0 transaction

Pass full decoded account bytes and the RPC-reported owner. The address passed to deserialization populates `state.PublicKey`, which `ToAccount()` needs. This only builds a signed packet; fetching and managing the table are left to the caller.

```csharp
using System;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Programs;
using Solaris.Programs.ALT;
using Solaris.Transactions.Models;

public static class AltTransferExample
{
    public static byte[] Build(
        PublicKey tableAddress,
        PublicKey accountOwner,
        ReadOnlySpan<byte> accountData,
        PrivateKey payer,
        PublicKey recipient,
        PublicKey recentBlockhash)
    {
        if (accountOwner != AddressLookupTableProgram.ProgramId)
            throw new ArgumentException("Account is not owned by the ALT program.", nameof(accountOwner));

        var state = accountData.Deserialize<AddressLookupTableState>(tableAddress);
        var table = state.ToAccount();
        var transaction = new Transaction
        {
            Version = 0,
            BlockHash = recentBlockhash,
            ExternalFeePayer = payer,
            Signers = [],
            AddressLookupTables = [table],
            Instructions = [SystemProgram.Transfer(payer.PublicKey, recipient, 1_000UL)]
        };
        return transaction.Build();
    }
}
```

- Fetch full `base64` data with `GetAccountInfo`; reject a null result and pass `new PublicKey(account.Owner)` with `account.DataSpan`. `DataSpan` decodes anew on each access.
- Signers and program IDs remain static. Eligible addresses use the first matching table; unused tables are omitted. This transfer uses a lookup only if the table contains the recipient.
- Check table warm-up and deactivation against the execution slot; compilation does not query the cluster. `ToAccount()` shares `state.Addresses`, so keep that array unchanged.
- Table management uses `CreateLookupTable`, `ExtendLookupTable`, `FreezeLookupTable`, `DeactivateLookupTable`, and `CloseLookupTable` on `AddressLookupTableProgram`.

## Token-2022 account sizing and fee-aware transfers

This returns token-account space, the sender's ATA, and a checked transfer instruction. `destinationTokenAccount` must be an existing token account; `sender` is the source ATA's single-key authority. `amount` is in base units, and `executionEpoch` selects the transfer-fee schedule.

The helper does not fetch accounts, create the ATA, sign, or submit.

```csharp
using System;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Programs;
using Solaris.Programs.Token2022;
using Solaris.Transactions.Models;

public static class Token2022TransferExample
{
    public static (int AccountSize, PublicKey SourceAta, TransactionInstruction Transfer) Prepare(
        PublicKey mintAddress,
        PublicKey mintProgramOwner,
        ReadOnlySpan<byte> mintData,
        PublicKey sender,
        PublicKey destinationTokenAccount,
        ulong amount,
        ulong executionEpoch)
    {
        if (mintProgramOwner != Token2022Program.ProgramId)
            throw new ArgumentException("Mint is not owned by Token-2022.", nameof(mintProgramOwner));

        var mint = mintData.Deserialize<Token2022Mint>(mintAddress);
        int tokenAccountSize = Token2022ExtensionSizes.GetAccountSizeForMint(
            mint, [Token2022ExtensionType.ImmutableOwner]);
        PublicKey sourceAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
            sender, mintAddress, out _, Token2022Program.ProgramId);

        TransactionInstruction transfer = Token2022Program.TransferChecked(
            sourceAta, mintAddress, destinationTokenAccount, amount, mint.Decimals, sender);
        if (mint.Extensions.TryGet<Token2022TransferFeeConfig>(out var fees))
        {
            ulong fee = fees.CalculateEpochFee(executionEpoch, amount);
            transfer = Token2022Program.TransferCheckedWithFee(
                sourceAta, mintAddress, destinationTokenAccount,
                sender, amount, mint.Decimals, fee);
        }
        return (tokenAccountSize, sourceAta, transfer);
    }
}
```

- Check RPC owners before decoding token accounts, then verify mint, authority, state, and balance. To create the ATA, include `CreateAssociatedTokenAccount` with `idempotent: true` and `tokenProgramId: Token2022Program.ProgramId` in a funded transaction.
- `AccountSize` includes mint-required extensions and immutable ownership; it is not mint space or a rent quote. Mint extension setup has separate ordering rules; `ValidateMintExtensionCombinations` checks supported combinations.
- Fees are deducted from `amount`; refresh mint configuration and epoch near an epoch boundary. Transfer-hook mints also need resolved `remainingAccounts`, and other extension policies still apply. Use `multisigSigners` for multisig authorities.
- `Token2022Mint`, `Token2022Account`, and `Token2022Multisig` use the same decoding pattern. `Extensions` owns a TLV copy; `Borrow()` exposes a borrowed view, and `TryGetRaw` reads unknown extensions.

## RPC account checks, simulation, and submission

This reads the payer, fetches a blockhash, signs, and simulates the packet. `broadcast: false` still makes RPC calls; `true` submits after simulation. Start with a funded test wallet on a development cluster. The payer needs the transfer amount plus fees.

```csharp
using System;
using System.Threading.Tasks;
using Solaris.Base.Account;
using Solaris.Programs;
using Solaris.Rpc;
using Solaris.Rpc.RpcModels;
using Solaris.Transactions.Models;

public static class RpcSolTransferExample
{
    public static async Task<(string? Signature, int LastValidBlockHeight)> RunAsync(
        string rpcUrl,
        PrivateKey payer,
        PublicKey recipient,
        ulong lamports,
        bool broadcast = false)
    {
        using var rpc = new SolanaRpcClient(rpcUrl);
        var account = await rpc.GetAccountInfo(
            payer.PublicKey, commitment: "confirmed", encoding: "base64")
            ?? throw new InvalidOperationException("Payer account does not exist on this cluster.");
        if (account.Owner != SystemProgram.ProgramId.Key || account.Executable || account.Space != 0)
            throw new InvalidOperationException("Use an ordinary system-owned wallet as the payer.");
        if (account.Lamports <= lamports)
            throw new InvalidOperationException("Payer needs the amount plus transaction fees.");

        var latest = await rpc.GetLatestBlockhash(commitment: "confirmed");
        var transaction = new Transaction
        {
            BlockHash = new PublicKey(latest.Blockhash),
            ExternalFeePayer = payer,
            Signers = [],
            Instructions = [SystemProgram.Transfer(payer.PublicKey, recipient, lamports)]
        };
        string encoded = Convert.ToBase64String(transaction.Build());
        var simulation = await rpc.SimulateTransaction(encoded, new SimulateTransactionConfig
        {
            Commitment = "confirmed",
            Encoding = "base64",
            ReplaceRecentBlockhash = false,
            SigVerify = true
        });
        if (simulation.Err is not null)
            throw new InvalidOperationException($"Simulation failed: {simulation.Err}");

        if (!broadcast)
            return (null, latest.LastValidBlockHeight);

        ulong height = await rpc.GetBlockHeight(commitment: "confirmed");
        if (height > (ulong)latest.LastValidBlockHeight)
            throw new InvalidOperationException("Blockhash expired; obtain a new one and rebuild/re-sign.");
        string signature = await rpc.SendTransaction(encoded, encoding: "base64");
        return (signature, latest.LastValidBlockHeight);
    }
}
```

`SendTransaction` sets `skipPreflight = true`, so simulation is explicit here. The balance check is not a fee quote, and state can change after simulation.

Track `GetSignatureStatuses`, including `Err` and `ConfirmationStatus`, and block height against `LastValidBlockHeight`. A signature is not confirmation; a null status is not success. Reconcile the original signature before rebuilding a payment to avoid paying twice.

Disposing `SolanaRpcClient` also disposes an injected `HttpClient`; do not pass a shared client that must outlive it.

## RPC account batches and transaction history

Read-only calls for up to 100 addresses and a signature from the same cluster. Account results preserve order, duplicates, and null entries for missing accounts. The context includes the evaluation slot and optional API version.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Solaris.Base.Account;
using Solaris.Rpc;

public static class RpcInspectionExample
{
    public static async Task InspectAsync(
        string rpcUrl,
        IReadOnlyList<PublicKey> addresses,
        string signature,
        ulong? minContextSlot = null,
        CancellationToken cancellationToken = default)
    {
        using var rpc = new SolanaRpcClient(rpcUrl);
        var snapshot = await rpc.GetMultipleAccountsWithContext(
            addresses, commitment: "confirmed", encoding: "base64+zstd",
            minContextSlot: minContextSlot, cancellationToken: cancellationToken);
        Console.WriteLine($"Account evaluation slot: {snapshot.Context?.Slot}");

        var accounts = snapshot.Value!;
        for (int i = 0; i < accounts.Length; i++)
        {
            var account = accounts[i];
            Console.WriteLine(account is null
                ? $"{addresses[i]}: missing"
                : $"{addresses[i]}: {account.Lamports} lamports, {account.DataSpan.Length} decoded bytes");
        }

        var result = await rpc.GetTransaction(
            signature, commitment: "confirmed", maxSupportedTransactionVersion: 0,
            cancellationToken: cancellationToken);
        if (result is null)
        {
            Console.WriteLine("Transaction not found at the requested commitment.");
            return;
        }

        var transaction = result.Transaction;
        var message = transaction.Message;
        Console.WriteLine($"Transaction slot: {result.Slot}, fee: {result.Meta?.Fee}");
        Console.WriteLine($"Packet: {transaction.Data.Length} bytes, {transaction.SignatureCount} signatures, {message.Accounts.Length} static accounts");
        foreach (var instruction in message.Instructions)
            Console.WriteLine($"Program index {instruction.ProgramIdIndex}: {instruction.KeyIndices.Length} accounts, {instruction.Data.Length} data bytes");
    }
}
```

- Account encodings are `base58`, `base64` (default), and `base64+zstd`. `Data` holds `[payload, encoding]`; `DataSpan` decodes on each access. Base58 is limited to 128 bytes. `dataSlice: new DataSlice(offset, length)` works with all three encodings; `Space` still reports full size. Do not decode a slice as a complete account.
- `GetMultipleAccounts` returns values without context. Both batch methods default to `"processed"`; this example requests `"confirmed"`. `minContextSlot` is a lower bound, not a historical snapshot selector.
- `GetTransaction` requests base64 only and returns a `DecodedTransaction` with RPC metadata. Commitment defaults to `"confirmed"`; `"finalized"` is supported, `"processed"` is not. `maxSupportedTransactionVersion: 0` supports legacy/v0; `null` requests legacy only.
- `Message.Accounts` contains static `PublicKeyValue` keys. Instruction indices address those keys, then `Meta.LoadedAddresses.Writable`, then `Readonly`. The packet holds lookup references, not resolved addresses. Inner-instruction metadata retains base58 data strings.
- A null transaction result means unavailable at the requested commitment; `Meta.Error` indicates execution failure in a present result. Metadata and recording fields may be absent. `Version` distinguishes omitted, legacy, and numeric versions; token amount strings retain exact precision.
- RPC owns the decoded packet buffer. Response JSON converters are deserialization-only. HTTP/RPC errors and cancellation propagate to the caller.

## Decode transaction packets without copying

Use `DecodedTransaction.Parse(ReadOnlyMemory<byte>)` for a signed packet or `DecodedMessage.Parse` for a bare message. Both support legacy/v0 and borrow the input without copying individual fields. RPC uses the same decoder.

```csharp
using System;
using Solaris.Base.Account;
using Solaris.Transactions.LowLevel;

public static class BinaryTransactionExample
{
    public static void Inspect(ReadOnlyMemory<byte> packet)
    {
        var transaction = DecodedTransaction.Parse(packet);
        var message = transaction.Message;
        Console.WriteLine($"Signatures: {transaction.SignatureCount}; static accounts: {message.Accounts.Length}");

        // Parsing does not verify signatures. Do this separately when required.
        for (int i = 0; i < transaction.SignatureCount; i++)
        {
            var signer = new PublicKey(in message.Accounts[i]);
            bool valid = signer.Verify(message.Data.Span, transaction.GetSignature(i));
            Console.WriteLine($"Signature {i}: {valid}");
        }

        foreach (var instruction in message.Instructions)
            Console.WriteLine($"Program index {instruction.ProgramIdIndex}: {instruction.KeyIndices.Length} accounts, {instruction.Data.Length} data bytes");
        foreach (var lookup in message.AddressTableLookups)
            Console.WriteLine($"Lookup: {lookup.WritableIndices.Length} writable, {lookup.ReadOnlyIndices.Length} read-only indices");
    }
}
```

Keep the underlying memory alive and unchanged while using decoded views; do not return a pooled buffer early. `Data` is the signed packet; `Message.Data` excludes signatures and includes the v0 prefix. `GetSignature(i)` returns a 64-byte span.

Malformed packets throw `FormatException`; unsupported versions throw `NotSupportedException`. Signed packets are limited to 1,232 bytes. Parsing checks binary structure, not signatures or chain state; the helper verifies signatures explicitly.
