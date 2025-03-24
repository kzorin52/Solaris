using System.Collections.Frozen;

namespace Solaris.Base.Account;

public partial class PublicKey
{
    private static ISharedDictionary? _generatedDictionary;

    public static void LoadCachedDictionary(ISharedDictionary dictionary)
    {
        foreach (var entity in dictionary.Dictionary.Values) _ = entity.GetHashCode();
        _generatedDictionary = dictionary;
    }

    public static PublicKey InitializeFromCache(string encoded, byte[] raw)
    {
        var pub = new PublicKey(encoded)
        {
            _keyMemory = raw,
            _keyBytes = raw
        };

        return pub;
    }

    private void CopyFrom(PublicKey another)
    {
        _keyEncoded = another.Key;
        _keyMemory = another.KeyMemory;
        _keyBytes = another.KeyBytes;
        _hashCode = another._hashCode;
    }
}

public interface ISharedDictionary
{
    public FrozenDictionary<string, PublicKey> Dictionary { get; }
}