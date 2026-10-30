using Solaris.Base.Account;

namespace Solaris.Transactions.LowLevel;

public readonly record struct MessageAddressTableLookup(PublicKey AccountKey, byte[] WritableIndices, byte[] ReadOnlyIndices);
