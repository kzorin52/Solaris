# Solaris examples

[Back to the README](README.md#getting-started) · [Performance and architecture](README.md#performance-and-architecture)

These examples use Solaris APIs, not Solnet APIs. They separate local address/instruction/packet construction from operations that contact a cluster or spend funds.

## Contents

- [Public keys and deterministic derivation](#public-keys-and-deterministic-derivation)
- [Build a signed SOL transfer](#build-a-signed-sol-transfer)
- [Compile once and reuse serialization buffers](#compile-once-and-reuse-serialization-buffers)
- [Decode an ALT and build a v0 transaction](#decode-an-alt-and-build-a-v0-transaction)
- [Token-2022 account sizing and fee-aware transfers](#token-2022-account-sizing-and-fee-aware-transfers)
- [RPC account checks, simulation, and submission](#rpc-account-checks-simulation-and-submission)
- [RPC account batches and transaction history](#rpc-account-batches-and-transaction-history)
- [Decode transaction packets without copying](#decode-transaction-packets-without-copying)

## Using the snippets

Use a `net9.0` console project with an SDK that supports the repository's C# syntax. See the [README setup instructions](README.md#getting-started) for the repository's SDK/reference-pack configuration. Add project references to the local `.csproj` files below; these examples do not assume published NuGet packages.

| C# block | Required project references | Execution |
| --- | --- | --- |
| 1: keys and derivation | `Solaris.Base`, `Solaris.Programs` | Standalone top-level program; deterministic, no network |
| 2: signed SOL packet | `Solaris.Base`, `Solaris.Programs`, `Solaris.Transactions` | Standalone top-level program; random ephemeral key, no network |
| 3: reusable serialization | Same as block 2 | Append to block 2 in the same file |
| 4: ALT helper | `Solaris.Base`, `Solaris.Borsh`, `Solaris.Programs`, `Solaris.Transactions` | Standalone helper class; caller supplies account bytes and transaction inputs |
| 5: Token-2022 helper | Same as block 4 | Standalone helper class; caller supplies mint bytes and account addresses |
| 6: RPC workflow | `Solaris.Base`, `Solaris.Programs`, `Solaris.Transactions`, `Solaris.Rpc` | Standalone helper class; caller supplies endpoint and funded signer; sends only when requested |
| 7: RPC inspection | `Solaris.Base`, `Solaris.Rpc` | Standalone read-only helper; caller supplies endpoint, public addresses, and transaction signature |
| 8: binary transaction inspection | `Solaris.Base`, `Solaris.Transactions` | Standalone helper; caller supplies a signed binary packet; no network |

For example, `Solaris.Base` means a project reference to `Solaris.Base/Solaris.Base.csproj`, with the relative path adjusted for your console project's location. Blocks 4–8 belong in their own `.cs` files in a console project or library; invoke their methods from your application. Each has its own usings. Do not concatenate the standalone top-level programs into one file.

## Public keys and deterministic derivation

`PublicKey` is the object-facing API and base58 representation. `PublicKeyValue` is a 32-byte value suitable for value-keyed collections; copying `KeyValue` makes a value snapshot, not a new `PublicKey` object. Its `AsSpan()` view borrows that value's storage; `CopyTo` writes to caller-owned storage.

This program uses the System Program address only as a **public demonstration seed**, not as a wallet you control. The wrapped-SOL mint address is used for legacy-token ATA derivation. Deriving an address neither creates nor fetches an account.

**C# block 1 — standalone, deterministic, no network:**

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

For your own program's PDA, use its program ID and its documented seed layout in `FindProgramAddress`; handle a null `Key` result. Seeds and their order are part of the program's address contract. For Token-2022 ATAs, explicitly pass `Token2022Program.ProgramId` instead of the legacy token program ID; the token program is itself one of the seeds.

## Build a signed SOL transfer

This example builds and signs a legacy transaction **locally**. The payer is freshly generated and unfunded, and the all-zero blockhash is deliberately not a cluster blockhash. The resulting packet is for serialization experiments only: **do not submit it**. For a real transfer, replace both with a securely loaded, funded signer and a recent blockhash from the destination cluster; use a real recipient you intend to pay. One SOL is 1,000,000,000 lamports.

**C# block 2 — standalone, no network or broadcast:**

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

`ExternalFeePayer` pays the fee and is supplied separately to signing. Without it, the first entry of `Signers` becomes the fee payer. Put every other required private key in `Signers`; array order does not dictate wire signature order, and building does not reorder that array.

Keep private keys out of source code, logs, and example output. `PrivateKey` is not a disposable secret vault: a 64-byte input memory buffer can be retained by the object, and signing can retain an internal secret-key copy. Do not mutate backing key memory while it is in use or assume clearing your input buffer erases every copy.

## Compile once and reuse serialization buffers

Use `BuildMessage()` when you need control over the destination buffer. This separates account compilation from serialization/signing. The compiled message contains the blockhash and instruction data for that transaction, not a template that automatically refreshes cluster state.

**C# block 3 — append to block 2, still no network:**

```csharp
var message = transaction.BuildMessage();
byte[] buffer = new byte[message.TransactionSize()];
int written = message.SerializeTransaction(buffer, transaction.Signers!, payer);
ReadOnlyMemory<byte> signedPacket = buffer.AsMemory(0, written);

byte[] unsignedMessage = new byte[message.Size()];
message.Serialize(unsignedMessage);
Console.WriteLine($"Message: {unsignedMessage.Length} bytes; packet: {signedPacket.Length} bytes.");
```

Reuse `buffer` for subsequent serializations of this compiled message only after its previous consumer has finished. `signedPacket` is a view, not a copy: overwriting `buffer` changes that view. An asynchronous sender must finish consuming the bytes before reuse, or take its own copy. `Size()`/`Serialize` describe the unsigned message; `TransactionSize()`/`SerializeTransaction` include signatures.

Compilation retains instruction data arrays; it does not deep-copy them. Treat instruction data, compiled arrays, and supplied lookup-table address arrays as immutable while compiling or using a compiled message. To change instructions/accounts, rebuild the message. To submit after blockhash expiry, obtain a new blockhash and rebuild/re-sign rather than reusing an expired packet. Signed packets above 1,232 bytes, more than 256 referenced accounts, unsupported versions, and missing required signing keys are rejected.

## Decode an ALT and build a v0 transaction

The helper below takes **full binary account data**, not its base64 text, plus the account owner reported by RPC. Verify ownership before interpreting bytes as a lookup table. Supplying `tableAddress` to Borsh decoding populates the state's `PublicKey`, which `ToAccount()` needs.

**Caller inputs:** an existing ALT account's address/owner/data, a payer private key, a recipient, and a recent blockhash. It only constructs a signed packet; it does not fetch, create, extend, or submit a table. For offline use, supply a locally captured account response and treat the packet as non-broadcastable unless all cluster inputs are current.

**C# block 4 — standalone helper:**

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

When using `SolanaRpcClient.GetAccountInfo`, request full `base64` data, reject a null result, construct the owner key from `account.Owner`, and pass `account.DataSpan` to the helper. `DataSpan` decodes base64 into a new byte array on each access; fetch it once when reusing data. Borsh decoding validates the layout, **not** the RPC owner or whether the table is usable at execution time.

A supplied ALT helps this transfer only if it contains the recipient. Signers and invoked program IDs remain static. Tables are considered in order; the first containing an eligible address wins, and unused tables are omitted. Only v0 supports lookups. Newly extended addresses require a later slot to warm up; also account for deactivation and the execution slot. Compilation does not query cluster state. `ToAccount()` shares `state.Addresses` rather than copying it; do not mutate that array while compiling/using the message.

For managing tables, `AddressLookupTableProgram` also exposes `CreateLookupTable` (with an `out` derived address), `ExtendLookupTable`, `FreezeLookupTable`, `DeactivateLookupTable`, and `CloseLookupTable`. Those instructions still require the correct authorities, funding where applicable, and on-chain lifecycle handling.

## Token-2022 account sizing and fee-aware transfers

Use `Solaris.Programs.Token2022`, not the legacy `Solaris.Programs.Token`, for Token-2022 mints. This helper decodes an initialized mint, determines token-account space including mint-required extensions plus immutable ownership, derives the sender's ATA, and constructs a checked transfer. For a transfer-fee mint it uses an expected fee for the caller-supplied execution epoch.

**Caller inputs:** mint address, its RPC-reported program owner, full binary mint data, the sender/authority public key, an existing destination **token account**, raw integer token amount, and expected execution epoch. `sender` must be the ordinary single-key authority for its source ATA. It is not the destination token account's owner. The helper neither fetches accounts nor signs, allocates, funds, creates, or submits anything.

**C# block 5 — standalone helper:**

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

Check the RPC program owner **before decoding** source/destination token accounts too. Then check their decoded mint, token authority, state, and available balance for your operation. Derivation alone does not mean the ATA exists. To create an ATA, use `AssociatedTokenAccountProgram.CreateAssociatedTokenAccount` with a funding payer, the token owner, mint, derived address, `idempotent: true`, and `tokenProgramId: Token2022Program.ProgramId`. The returned instruction must be included in a signed, funded transaction; sizing alone does not allocate an account or calculate rent.

The returned `AccountSize` is useful when planning a token account allocation, not a mint allocation. Mint extension allocation/initialization has its own ordering and compatibility requirements before `InitializeMint`/`InitializeMint2`; `ValidateMintExtensionCombinations` checks the helper's supported mint-extension compatibility rules.

`amount` is in base units, not a UI decimal amount. The calculated transfer fee is deducted from that amount; refresh mint fee configuration and epoch information when needed, especially near an epoch boundary. The checked fee is an expectation, not protection against all other account-policy failures. Transfer-hook mints need their resolved additional accounts passed as `remainingAccounts`; this simple helper does not resolve hooks or satisfy memo/other extension policies. It is intended for mints whose other requirements are already satisfied. For multisig authorities, use the builders' `multisigSigners` overload parameters and supply the required private keys when signing.

`Token2022Mint`, `Token2022Account`, and `Token2022Multisig` support the same Borsh account-decoding pattern. The owned `Extensions` collection keeps a copy of TLV data; cache typed values if reused. Its `Borrow()` API provides a span-based view tied to the underlying memory's lifetime. Unknown extension payloads can be inspected with `TryGetRaw`; decoding is not a substitute for understanding an extension's execution requirements.

## RPC account checks, simulation, and submission

This workflow reads the payer account, obtains a recent blockhash, builds/signs a SOL transfer, and simulates the exact signed packet. It **contacts the supplied endpoint even with `broadcast: false`**. With `broadcast: true`, it submits only after successful simulation. Use a development cluster and a dedicated funded test wallet first; the payer needs the transfer amount **plus transaction fees**. The amount is real lamports on the selected cluster, and the recipient must be deliberate.

**Caller inputs:** a trusted RPC URL for the intended cluster, a securely loaded `PrivateKey`, a real recipient, an amount, and explicit permission to broadcast. No key or endpoint is embedded below. The method owns its internally created RPC client and disposes it. If you instead inject an `HttpClient` into `SolanaRpcClient`, disposing the RPC client also disposes that HTTP client; do not give it a shared client that must outlive it.

**C# block 6 — standalone helper; network and potentially funded submission:**

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

The initial balance check only leaves room for fees; it is not a fee quote or a balance reservation. Simulation is still needed, and state may change between simulation and execution. Solaris currently sends transactions with `skipPreflight = true`, so this example deliberately calls `SimulateTransaction` and checks `Err` before sending. HTTP/RPC errors can throw. Do not log the signer or secret key when handling them.

A returned signature means the RPC accepted submission, **not that the transaction succeeded or reached confirmation**. Use `GetSignatureStatuses` to inspect `Err` and `ConfirmationStatus` at your required commitment, and use block height plus `LastValidBlockHeight` to track expiry. A null status is not success. Reconcile the original signature before deciding to create a new payment: rebuilding and sending another signed payment may transfer twice if the original landed. The height check above reduces obvious expiry failures but cannot guarantee validity at execution time.

## RPC account batches and transaction history

These are read-only calls; they do not build, sign, or submit transactions. Supply a trusted endpoint for the intended cluster, up to **100 addresses per call**, and a transaction signature from that cluster.

`GetMultipleAccounts` returns one entry per requested address, in the same order, including duplicates and `null` for missing accounts. `GetMultipleAccountsWithContext` returns the same values plus `Context.Slot` and the optional `Context.ApiVersion`. The default commitment is `"processed"`; this example requests `"confirmed"`. `minContextSlot` is a lower bound on evaluation, not a request for a historical snapshot at exactly that slot.

Account queries support only `"base58"`, `"base64"` (the default), and `"base64+zstd"` (the Solana wire name for base64-encoded Zstandard data). `SolanaAccount.Data` remains a `string[]` containing `[payload, encoding]`; `DataSpan` decodes all three, including decompression for Zstandard. It decodes afresh on every access. Parsed account objects and the deprecated `"binary"` account encoding are not supported. Base58 account data has the RPC's 128-byte limit. All three supported encodings accept `dataSlice: new DataSlice(offset, length)`; sliced data is not a full account layout, while `Space` still reports the original size.

**C# block 7 — standalone helper; read-only network access:**

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

`GetTransaction` always requests `"base64"`; there is no encoding argument or JSON/parsed/base58/legacy `binary` payload path. It defaults to `"confirmed"` and `maxSupportedTransactionVersion: 0` (legacy and v0). It accepts `"finalized"` but not `"processed"`. Set the maximum version to `null` to omit the option and request legacy transactions only. The shared decoder supports legacy and v0, matching the compiler; unsupported binary versions fail explicitly. Pass cancellation with the named `cancellationToken` argument.

`HistoricalTransaction.Transaction` is a `DecodedTransaction` from `Solaris.Transactions.LowLevel`, not an encoded string or a JSON message model. RPC decodes the base64 token directly into one owned packet buffer, validates it with the shared decoder, and retains no encoded payload string. The JSON-RPC envelope and execution metadata remain JSON as required by the protocol.

`Message.Accounts` contains static keys as a `ReadOnlySpan<PublicKeyValue>`. Instructions expose their program index, `KeyIndices`, and binary `Data` as borrowed views; address-table lookups expose their table key and writable/read-only index slices. Instruction indices address static keys followed by `Meta.LoadedAddresses.Writable` and then `Readonly`. The packet contains lookup references, not the resolved account addresses. `Meta.InnerInstructions` retains the RPC's raw `HistoricalInstruction` metadata shape, including its base58 data string; it does not use parsed instruction models.

`BlockTime`, `Meta`, and optional recording fields can be unavailable. A transaction result of `null` is distinct from a present transaction whose `Meta.Error` reports an execution failure. `Version` is nullable, and distinguishes omitted version information from `"legacy"` and numeric versions. Metadata includes fees, pre/post lamport and token balances, logs, inner instructions, rewards, loaded addresses, return data, compute units, and cost units; token amount strings retain exact precision. RPC/HTTP errors and cancellation propagate to the caller.

The `HistoricalTransaction` and `TransactionVersion` JSON converters are deserialization-only. Their required `Write` overrides throw `NotSupportedException`; RPC response serialization is intentionally unsupported.

## Decode transaction packets without copying

Use `DecodedTransaction.Parse(ReadOnlyMemory<byte>)` for a signed legacy or v0 packet, or `DecodedMessage.Parse(ReadOnlyMemory<byte>)` for a bare serialized message. A `byte[]` converts directly to the required memory view. Parsing and value-type `foreach` traversal over an existing buffer do not allocate per-field arrays or key wrappers. The RPC client uses this same implementation after decoding base64 once.

**C# block 8 — standalone helper; no network or submission:**

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

The decoder borrows storage: keep the underlying memory alive and unchanged while any decoded view is in use. Do not overwrite or return a pooled buffer until all consumers finish. `Data` is the complete signed packet; `Message.Data` excludes the signature count/signatures and includes the v0 version prefix when present. `GetSignature(i)` returns a 64-byte span. Formatting keys, constructing `PublicKey` wrappers, or explicitly copying spans is optional caller work, not part of parsing.

The shared `BorshDeserializer` provides bounded byte/span/skip reads and canonical `CompactU16` decoding. Transaction parsing adds header/signature consistency, static/loaded account-index limits, and trailing-byte checks. Malformed packets throw `FormatException`; unsupported versions throw `NotSupportedException`. Signed packets above 1,232 bytes are rejected. Address-table contents, blockhash validity, account ownership, and transaction execution are not checked. Signature verification in the helper is explicit cryptographic work and is not performed automatically by either parser or RPC.
