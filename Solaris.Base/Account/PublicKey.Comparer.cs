using System.Runtime.CompilerServices;

namespace Solaris.Base.Account;

public class PublicKeyComparer : 
    IEqualityComparer<PublicKey>,
    IEqualityComparer<PublicKeyValue>,
    IAlternateEqualityComparer<PublicKey, ReadOnlySpan<byte>>,
    IAlternateEqualityComparer<ReadOnlySpan<byte>, PublicKey>,
    IAlternateEqualityComparer<ReadOnlySpan<byte>, PublicKeyValue>,
    IAlternateEqualityComparer<PublicKey, PublicKeyValue>
{
    public static readonly PublicKeyComparer Instance = new();

    public bool Equals(PublicKey alternate, ReadOnlySpan<byte> other)
    {
        return alternate.KeySpan.SequenceEqual(other);
    }

    public ReadOnlySpan<byte> Create(PublicKey alternate)
    {
        return alternate.KeySpan;
    }

    public bool Equals(ReadOnlySpan<byte> alternate, PublicKey other)
    {
        return alternate.SequenceEqual(other.KeySpan);
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        return PublicKeyValue.Create(alternate).GetHashCode();
    }

    PublicKey IAlternateEqualityComparer<ReadOnlySpan<byte>, PublicKey>.Create(ReadOnlySpan<byte> alternate)
    {
        return new PublicKey(alternate);
    }

    public bool Equals(PublicKey? x, PublicKey? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return x.Equals(y);
    }

    public bool Equals(PublicKey alternate, PublicKeyValue other)
    {
        return other.Equals(in alternate.KeyValue);
    }

    public int GetHashCode(PublicKey alternate)
    {
        return alternate.GetHashCode();
    }

    PublicKeyValue IAlternateEqualityComparer<PublicKey, PublicKeyValue>.Create(PublicKey alternate)
    {
        return alternate.KeyValue;
        // should not be used...
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<byte> span, PublicKeyValue key)
    {
        return PublicKeyValue.Create(span).Equals(in key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PublicKeyValue Create(ReadOnlySpan<byte> span)
    {
        return PublicKeyValue.Create(span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PublicKeyValue x, PublicKeyValue y)
    {
        return x == y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(PublicKeyValue obj)
    {
        return obj.GetHashCode();
    }
}