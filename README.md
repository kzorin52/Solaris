# Solaris
Low-level, performance-oriented light Solana SDK for .NET <br>

Very early WIP. Base concept (usable).

## Restore with .NET SDK 11.0.100

Run `dotnet restore Solaris.slnx`. `Directory.Build.targets` pins the .NET 9
reference packs to `9.0.14`, because SDK `11.0.100` requests `9.0.23`, which is
not available from the configured feeds. The projects continue to target `net9.0`.
Remove the reference-pack override once the SDK-requested version is available.

## Versioned transactions and address lookup tables

`Transaction.Version` is `null` for legacy messages and `0` for Solana v0.
Both formats use compact-u16 lengths. Only v0 supports address lookup tables.

```csharp
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Programs;
using Solaris.Transactions.Models;

var state = accountData.Deserialize<AddressLookupTableState>(tableAddress);
var table = state.ToAccount();

var transaction = new Transaction
{
    Version = 0,
    BlockHash = recentBlockhash,
    ExternalFeePayer = payer,
    Signers = [],
    AddressLookupTables = [table],
    Instructions = [SystemProgram.Transfer(payer, recipient, 1_000)]
};
byte[] packet = transaction.Build();
```

`payer` is a `PrivateKey`; `recipient`, `recentBlockhash`, and `tableAddress` are
`PublicKey` values. `accountData` is the decoded binary lookup-table account data
as a `ReadOnlySpan<byte>`.
Only pass accounts owned by `AddressLookupTableProgram.ProgramId`, and use tables
whose addresses are usable at the transaction's execution slot. Newly extended
addresses require a slot to warm up; compilation does not query cluster state.

The fee payer is `ExternalFeePayer`, or the first entry of `Signers`. All other
required private keys must be in `Signers`; their order does not determine signature
order. Building never modifies that array. Signers and invoked program IDs always
remain static. Supplied tables are considered in order, and the first table
containing an eligible address wins. Unused tables are omitted.

For reusable buffers, compile once and serialize directly:

```csharp
var message = transaction.BuildMessage();
byte[] buffer = new byte[message.TransactionSize()];
int written = message.SerializeTransaction(buffer, transaction.Signers, payer);
```

`message.Size()` and `message.Serialize(Span<byte>)` operate on the unsigned message.
Message serialization allocates no managed memory. Compilation allocates the
account map and output arrays, reuses instruction data, and does not expand tables
into intermediate key collections. Signed packets larger than 1,232 bytes,
more than 256 referenced accounts, missing signers, and unsupported versions fail.
Keep instruction data and table address arrays unchanged while compiling or using
a compiled message.

`AddressLookupTableProgram` provides `CreateLookupTable` (returning the derived
address through an `out` parameter), `ExtendLookupTable` (with an optional funding
payer), `FreezeLookupTable`, `DeactivateLookupTable`, and `CloseLookupTable`.
`AddressLookupTableState` implements `IAccountData` and
`IBorshDeserializable<AddressLookupTableState>`, matching the other program account
models. Deserialize it through `Solaris.Borsh` to populate its account public key,
metadata, authority, and address array. `ToAccount()` reuses that array without
copying it for transaction compilation. All ALT instruction encoding also uses
`Solaris.Borsh`.

## Signature statuses

```csharp
using Solaris.Rpc.RpcModels;

var response = await rpc.GetSignatureStatuses(
    new[] { signature },
    new SignatureStatusesConfig { SearchTransactionHistory = true });

SignatureStatus? status = response.Value![0];
```

The result preserves RPC context and input order. A missing signature has a null
status; rooted transactions have null `Confirmations`. `Err` is a nullable
`JsonElement`, preserving string and structured transaction errors.
Omit the configuration to use the node's default recent-status-cache search.
Solana accepts up to 256 signatures per request.

## Verification

Run `dotnet test Solaris.Tests/Solaris.Tests.csproj -c Release -p:UseAppHost=false`
and `dotnet test Solaris.Base.Tests/Solaris.Base.Tests.csproj -c Release -p:UseAppHost=false`.
The apphost override avoids the unavailable SDK-requested .NET 9 apphost pack.
Transaction interoperability was exercised against `@solana/web3.js` 1.98.4:
100 legacy/v0 message and signed-packet comparisons, all lookup-table instruction
forms, and mutable/frozen lookup-table account decoding.