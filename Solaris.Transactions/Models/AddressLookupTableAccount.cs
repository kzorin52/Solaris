using Solaris.Base.Account;

namespace Solaris.Transactions.Models;

public sealed class AddressLookupTableAccount(PublicKey key, PublicKey[] addresses)
{
    public PublicKey Key { get; } = key;
    public PublicKey[] Addresses { get; } = addresses;
}
