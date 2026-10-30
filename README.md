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
using Solaris.Programs.ALT;
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

## Token-2022

Token-2022 lives in `Solaris.Programs.Token2022`; ALT lives in
`Solaris.Programs.ALT`. The legacy token program remains in `Solaris.Programs.Token`.

`Token2022Program` targets `TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb`.
It provides the complete base instruction set, including multisig initialization
and authorities, checked operations, native-token operations, reallocation, UI
conversion instructions, and batches. Authority methods accept optional
`ReadOnlySpan<PublicKey>` multisig signers. Without those signers, the authority
itself is marked as a signer.

| Extension family | Support |
| --- | --- |
| Transfer fees | Initialize/update, checked transfers with fees, harvest and withdrawals, epoch-aware fee and inverse-fee calculations |
| Account policies | Default state, immutable owner, memo requirements, CPI guard, non-transferability |
| Mint controls | Close authority, permanent delegate, interest-bearing, scaled UI amounts, pause/resume, permissioned burns |
| Transfer hooks | Initialize/update, explicit remaining accounts, validation-account decoding and PDA/account-data resolution |
| Metadata and groups | All pointer operations, metadata initialize/update/remove/emit, groups and group members |
| Confidential transfers | Configure/approve accounts, deposit/withdraw/transfer, pending balances, credit controls, registry configuration |
| Confidential fees and mint/burn | Harvest/withdraw controls, supply-key rotation, mint/burn, pending burns, permissioned confidential burns |
| Proof instruction assembly | Inline verification, record-backed proofs, preverified context states, context closure, ordered proof/token instruction bundles |

All token instruction payloads and account decoding use `Solaris.Borsh`.
Instruction builders allocate the final payload and account-meta arrays directly.
Caller-provided remaining account metas retain their order and privileges.

```csharp
using Solaris.Borsh;
using Solaris.Programs;
using Solaris.Programs.Token2022;

var mint = mintData.Deserialize<Token2022Mint>(mintAddress);
int tokenAccountSize = Token2022ExtensionSizes.GetAccountSizeForMint(
    mint, [Token2022ExtensionType.ImmutableOwner]);

var associatedAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
    owner, mintAddress, out _, Token2022Program.ProgramId);

var transfer = Token2022Program.TransferChecked(
    source, mintAddress, destination, amount, mint.Decimals, owner);

if (mint.Extensions.TryGet<Token2022TransferFeeConfig>(out var fees))
{
    ulong fee = fees.CalculateEpochFee(epoch, amount);
    transfer = Token2022Program.TransferCheckedWithFee(
        source, mintAddress, destination, owner, amount, mint.Decimals, fee);
}
```

`mintData` is a `ReadOnlySpan<byte>` of decoded account data; addresses are
`PublicKey` values. Verify the RPC account owner is `Token2022Program.ProgramId`
before decoding. Mint extensions must be allocated and initialized before
`InitializeMint`/`InitializeMint2`; their on-chain ordering and compatibility
requirements still apply. `Token2022ExtensionSizes.ValidateMintExtensionCombinations`
checks the interface's mint-extension compatibility rules.

`Token2022Mint`, `Token2022Account`, and `Token2022Multisig` implement `IAccountData`
and `IBorshDeserializable<T>`. Extended accounts validate the marker, TLV boundaries,
known payload sizes, and duplicate extensions. All 28 current extension types
have typed decoders. Unknown TLV types remain available through `TryGetRaw`.
`Token2022Extensions` owns one copy of the TLV data; ciphertext fields are slices
of that backing memory, not further copies. Cache typed decoded values when reused.
`Token2022ExtensionView` borrows an existing TLV span without allocating; the owned
collection's `Borrow()` exposes the same allocation-free lookup/enumeration API.

Size helpers support fixed extensions and explicit `Token2022ExtensionSize`
values for variable-length token metadata, deduplicate extensions, derive required
account extensions from a mint, and avoid the 355-byte multisig length collision.
`Token2022TokenMetadata.GetPayloadSize()` calculates its UTF-8 payload size.

For a transfer-hook mint, decode its validation account as
`TransferHookExtraAccountMetaList` and call `TransferHookExtraAccounts.Resolve`.
The result is ready to pass as `remainingAccounts` to a checked transfer.
The resolver supports literals, instruction/account data, account keys, and
hook/external-program PDAs; account-data references require a caller-supplied
`Func<PublicKey, ReadOnlyMemory<byte>>`. It resolves accounts synchronously from
already-fetched data and de-escalates duplicate privileges. A span overload writes
resolved metas into a caller-provided buffer.

Confidential operations accept caller-generated encrypted balances and proofs.
This Programs layer does not implement cryptographic proof generation or key
management. Use `Token2022ProofLocation` for an existing context or a nonzero
relative proof-instruction offset. Overloads accepting `Token2022ProofInput`
return ordered instruction bundles: record verifiers before the token instruction,
inline verifiers after it, and context accounts for preverified proofs.
Record-backed bundle inputs require an output context account.
The caller must create/fund context accounts and retain the returned instruction
order. Proof buffers are exact-size checked, not cryptographically validated locally;
the proof program validates them on-chain. Availability of confidential operations
depends on the cluster's deployed program and enabled features.

The wire contract follows the official
[Token-2022 interface](https://github.com/solana-program/token-2022/tree/main/interface/src),
including current unwrap-lamports, permissioned-burn, and batch instructions.

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

Token-2022 interoperability was exercised against `@solana-program/token-2022`
0.20.0 and `@solana/spl-token`: 196 instruction payloads, 100 account lists,
all 28 extension account layouts and size calculations, six populated extension
payloads, 60 transfer-fee boundary cases, and transfer-hook resolution using all
four PDA seed sources. Borrowed TLV lookup allocated zero managed bytes across
10,000 calls. These are local wire-format and instruction-assembly checks, not
live-cluster execution or cryptographic proof-generation tests.