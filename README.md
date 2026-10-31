# Solaris

Low-level Solana SDK for .NET 9: keys, PDAs, binary serialization, transactions, program instructions, account decoding, and HTTP JSON-RPC.

The API is evolving. Solaris is not a drop-in replacement for Solnet.

## Modules

| Project | Purpose |
| --- | --- |
| [Solaris.Base](Solaris.Base) | Keys, Ed25519 signing, base58, and PDA derivation. |
| [Solaris.Borsh](Solaris.Borsh) | Span-based binary readers, writers, and account deserialization. |
| [Solaris.Transactions](Solaris.Transactions) | Legacy/v0 message compilation, lookup tables, signing, serialization, and binary decoding. |
| [Solaris.Programs](Solaris.Programs) | System, Compute Budget, Memo, ATA, Token, Token-2022, and address lookup table builders and account decoders. |
| [Solaris.Rpc](Solaris.Rpc) | Account queries, blockhashes, transaction history, simulation, submission, and signature status. |

## Performance and architecture

- `PublicKeyValue` stores a 32-byte key inline. `PublicKey` is a reference wrapper with lazy base58 decoding and cached encoding; most instruction APIs use it.
- PDA derivation accepts span-based seeds and uses stack buffers for seed assembly and bump search.
- Borsh and compiled messages can write to caller-owned spans. `Build()` is the allocating convenience path; message compilation still allocates.
- `DecodedTransaction` borrows a legacy/v0 packet without copying individual fields. RPC decodes base64 into one owned packet buffer; HTTP and JSON processing still allocate.

## Getting started

The libraries target `net9.0` and use preview C# syntax. Use a recent .NET SDK; running the tests requires the .NET 9 runtime.

Build the RPC project to include all five libraries. The solution also references sibling repositories that are not needed for this build.

```sh
dotnet restore Solaris.Rpc/Solaris.Rpc.csproj
dotnet build Solaris.Rpc/Solaris.Rpc.csproj -c Release --no-restore -p:UseAppHost=false
```

Use project references to consume this checkout:

```sh
dotnet add path/to/YourApp.csproj reference path/to/Solaris/Solaris.Rpc/Solaris.Rpc.csproj
```

Reference `Solaris.Programs` for offline transaction work or `Solaris.Base` for keys alone. Dependencies are transitive.

For SDK `11.0.100`, `Directory.Build.targets` pins .NET 9 reference packs to `9.0.14`; `UseAppHost=false` avoids the unavailable apphost pack.

See [EXAMPLES.md](EXAMPLES.md) for keys, signed transfers, reusable buffers, lookup tables, Token-2022, and RPC.

## Tests

```sh
dotnet test Solaris.Base.Tests/Solaris.Base.Tests.csproj -c Release -p:UseAppHost=false
dotnet test Solaris.Tests/Solaris.Tests.csproj -c Release -p:UseAppHost=false
```

## Constraints

- Supply a recent blockhash, required signers, and valid account state. Keep instruction and lookup-table arrays unchanged while using compiled messages. Lookup-table warm-up and deactivation checks are the caller's responsibility.
- Legacy and v0 transactions are supported: at most 1,232 bytes per signed packet and 256 referenced accounts.
- `SendTransaction` uses `skipPreflight = true`. Simulate explicitly, then track signature status and blockhash expiry. Submission is not confirmation.
- Account RPC methods accept encoded data, not parsed objects. `GetTransaction` requests base64 and returns a `DecodedTransaction` with RPC metadata. Async calls accept cancellation tokens.
- Token-2022 operations must satisfy each extension's requirements. Transfer hooks need resolved extra accounts; confidential operations need caller-generated encrypted data and proofs. This SDK does not generate confidential proofs or manage confidential keys.
- Do not log private keys or assume clearing an input buffer erases every secret-key copy.

## License

[Apache 2.0](LICENSE.txt).
