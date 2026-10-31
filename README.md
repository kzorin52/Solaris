# Solaris

Solaris is a low-level, performance-focused Solana SDK for .NET. It provides public/private key primitives, program-derived addresses (PDAs), binary serialization, transaction construction and signing, program instruction builders, account decoders, and an HTTP JSON-RPC client. The projects target **.NET 9 (`net9.0`)**. Solaris is an early, evolving project intended for developers who want explicit control over data, buffers, and transaction assembly rather than a high-level wallet framework.

**Solaris is not Solnet, and it is not a drop-in replacement for Solnet.** Treat it as a different API and architecture, not a namespace substitution. In particular, its key representation is built around `PublicKeyValue`, and its PDA engine has been completely rewritten. Porting an application means adapting its key handling, instruction construction, transaction flow, and RPC calls to Solaris's APIs.

## Performance and architecture

Performance is a design priority, expressed through concrete choices rather than a promise that every operation is allocation-free or faster than another SDK:

- **Inline key values.** [`PublicKeyValue`](Solaris.Base/Account/PublicKeyValue.cs) is a readonly, fixed-size 32-byte struct backed by two `UInt128` fields. It supports span-based access and copying, vector-based equality, and hardware-aware hashing. This provides a binary value representation without requiring a separate byte-array object for every key value.
- **A wrapper when convenience is useful.** [`PublicKey`](Solaris.Base/Account/PublicKey.cs) is a reference type, not an alias for `PublicKeyValue`. It wraps the binary value, lazily decodes base58 input, caches its encoded string, and exposes `KeyValue` and `KeySpan`. Most public instruction and transaction APIs use this wrapper; using Solaris does not mean every key-bearing object is a struct.
- **A completely rewritten PDA engine.** [`PublicKey.PDA.cs`](Solaris.Base/Account/PublicKey.PDA.cs) exposes span-based seed overloads for `FindProgramAddress` and `TryCreateProgramAddress`. Seed material is assembled in stack buffers; bump search reuses a stack-backed SHA-256 output buffer and creates the result wrapper when a candidate is accepted. The engine uses SHA-256 and the curve-check helper backed by Bouncy Castle, rather than implementing those cryptographic primitives itself. This is a different implementation and API surface, not a compatibility layer over Solnet.
- **Control over serialization buffers.** `BorshSerializer` writes to a caller-provided `Span<byte>`. Transactions can be compiled with `BuildMessage()` and then written with `CompiledMessage.Serialize(Span<byte>)` or `SerializeTransaction(Span<byte>, ...)`; `Build()` remains the convenient allocating path. Compilation still allocates its account map and output arrays, and compiled instructions retain their original data buffers.
- **Borrowed binary transaction views.** [`DecodedTransaction`](Solaris.Transactions/LowLevel/DecodedTransaction.cs) parses legacy/v0 packets from `ReadOnlyMemory<byte>` using the shared `BorshDeserializer`, including its canonical `CompactU16` reader. It does not copy keys, signatures, instruction data, or lookup indices into separate arrays. Static keys are exposed as `PublicKeyValue` spans, and instruction/lookup enumeration uses value types. RPC decodes base64 once into an owned packet buffer and reuses this decoder; HTTP, JSON metadata, and that packet buffer still allocate.

These choices aim to reduce avoidable allocations, encoding work, and intermediate copies. They are architectural characteristics, **not comparative benchmark results**. Measure your own workload, including key decoding, compilation, signing, and RPC costs.

## Modules and capabilities

| Project | Role |
| --- | --- |
| [`Solaris.Base`](Solaris.Base) | `PublicKeyValue`, `PublicKey`, `PrivateKey`, Ed25519 signing/verification, base58 helpers, and PDA derivation. |
| [`Solaris.Borsh`](Solaris.Borsh) | Span-based binary readers/writers, serialization interfaces, and account-data deserialization helpers. |
| [`Solaris.Transactions`](Solaris.Transactions) | Account metas, instruction models, legacy and version-0 message compilation, address lookup table references, signing, serialization, and zero-copy binary decoding. |
| [`Solaris.Programs`](Solaris.Programs) | Instruction builders for System, Compute Budget, Memo, Associated Token Account, legacy Token, Token-2022, and address lookup tables; token and lookup-table account decoders. |
| [`Solaris.Rpc`](Solaris.Rpc) | `SolanaRpcClient`: HTTP JSON-RPC methods for accounts, blockhashes, slots, transaction history, simulation, submission, and signature status queries. |
| [`Solaris.Base.Tests`](Solaris.Base.Tests), [`Solaris.Tests`](Solaris.Tests) | MSTest projects covering base primitives and the higher-level modules. |

Token-2022 includes extension decoding and sizing, transfer-fee helpers, transfer-hook account resolution, and confidential-operation instruction/proof assembly. Confidential cryptographic proof generation and confidential key management are **not** provided by this Programs layer. See the [Token-2022 examples](EXAMPLES.md#token-2022-account-sizing-and-fee-aware-transfers) for usage and constraints rather than assuming every extension is a simple transfer flag.

## Getting started

### Build from the repository

Use source/project references to consume this checkout. You need a recent .NET SDK capable of compiling the repository's preview C# features (`LangVersion=preview` in several projects), plus a .NET 9 runtime to run its tests.

From the repository root, build the RPC project to include all five SDK libraries through project references. The solution also includes sibling projects outside this checkout; they are not required to use Solaris.

```sh
dotnet restore Solaris.Rpc/Solaris.Rpc.csproj
dotnet build Solaris.Rpc/Solaris.Rpc.csproj -c Release --no-restore -p:UseAppHost=false
```

For an existing application, reference the modules you need. For example, the RPC project brings its project dependencies transitively:

```sh
dotnet add path/to/YourApp.csproj reference path/to/Solaris/Solaris.Rpc/Solaris.Rpc.csproj
```

For offline transaction work, reference `Solaris.Programs/Solaris.Programs.csproj` instead; for key primitives alone, reference `Solaris.Base/Solaris.Base.csproj`. These are project paths within your Solaris checkout, not package installation commands. Ensure your application's target framework is compatible with the libraries' `net9.0` target.

**SDK-specific build note:** `Directory.Build.targets` currently pins the .NET 9 reference packs to `9.0.14` for SDK `11.0.100`, whose requested `9.0.23` packs are unavailable from the configured feeds. This does not change the target framework. The commands here disable apphost generation to avoid the corresponding unavailable apphost pack.

### Learn the APIs

Start with [EXAMPLES.md](EXAMPLES.md) for C# examples: [keys and PDAs](EXAMPLES.md#public-keys-and-deterministic-derivation), [signed transfers](EXAMPLES.md#build-a-signed-sol-transfer), [reusable serialization buffers](EXAMPLES.md#compile-once-and-reuse-serialization-buffers), [v0 transactions and lookup tables](EXAMPLES.md#decode-an-alt-and-build-a-v0-transaction), [Token-2022](EXAMPLES.md#token-2022-account-sizing-and-fee-aware-transfers), and [RPC simulation and submission](EXAMPLES.md#rpc-account-checks-simulation-and-submission).

All asynchronous RPC methods accept an optional `CancellationToken` and forward it through HTTP requests and response reads.

`GetMultipleAccounts` supports commitment, data slicing, a minimum context slot, and cancellation; `GetMultipleAccountsWithContext` also retains the evaluation slot and API version. Account data stays an encoded `[payload, encoding]` pair, with `DataSpan` decoding `base58`, `base64`, or `base64+zstd`. Parsed account data is intentionally unsupported.

`GetTransaction` requests **base64 only** and returns a decoded `Solaris.Transactions.LowLevel.DecodedTransaction` alongside the full RPC metadata. Commitment, maximum transaction version, and cancellation remain configurable; JSON/parsed transaction payloads and legacy base58/`binary` encoding choices are removed. See [account batches and transaction history](EXAMPLES.md#rpc-account-batches-and-transaction-history) and [binary transaction decoding](EXAMPLES.md#decode-transaction-packets-without-copying) for examples and buffer-lifetime rules.

### Run the tests

```sh
dotnet test Solaris.Base.Tests/Solaris.Base.Tests.csproj -c Release -p:UseAppHost=false
dotnet test Solaris.Tests/Solaris.Tests.csproj -c Release -p:UseAppHost=false
```

These commands run the repository's tests; they are not a substitute for simulation and integration testing against your intended cluster.

## Practical expectations

- **Low-level, evolving APIs:** expect to inspect the models and program builders when integrating. Do not assume Solnet API compatibility or complete Solana RPC/program coverage.
- **The caller supplies chain context:** provide a recent blockhash, all required signers, correct account ownership and state, and usable lookup-table contents. Compilation does not query the cluster or check lookup-table warm-up/deactivation against the execution slot. Keep instruction data and table address arrays stable while compiling or using compiled messages.
- **Protocol limits still apply:** transaction construction and binary decoding support legacy and v0 messages, not arbitrary future versions. Signed packets are limited to 1,232 bytes and messages to 256 referenced accounts. Supplying a buffer does not bypass these constraints.
- **Submission is not confirmation:** `SolanaRpcClient.SendTransaction` sets `skipPreflight = true`. Simulate explicitly when needed, then track signature status and blockhash validity; receiving a signature does not establish successful execution.
- **Token-2022 needs on-chain context:** check account owners before decoding and respect extension initialization/order requirements. Transfer-hook resolution needs already-fetched account data where referenced. Confidential operations need caller-generated encrypted data/proofs and depend on the cluster's deployed programs and enabled features.
- **Protect secrets:** private-key encodings and key buffers contain secret material. Do not log them or use example keys for real funds.

## License

Solaris is licensed under the [Apache License 2.0](LICENSE.txt).
