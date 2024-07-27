using System.Collections.Frozen;
using SimpleBase;

namespace Solaris.Base.Account;

public partial class PublicKey
{
    private static ISharedDictionary? _generatedDictionary; 
    public static void LoadCachedDictionary(ISharedDictionary dictionary)
    {
        foreach (var entity in dictionary.Dictionary.Values)
        {
            _ = entity.GetHashCode();
        }
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
}

public interface ISharedDictionary
{
    public FrozenDictionary<string, PublicKey> Dictionary { get; }
}