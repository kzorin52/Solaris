using System.Diagnostics;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Solaris.Base.Crypto;

namespace Solaris.Base.Account;

/// <summary>
///     Implements the public key functionality
/// </summary>
[DebuggerDisplay("PublicKey = {ToString()}")]
public partial class PublicKey
{
    /// <summary>
    ///     Public key length
    /// </summary>
    public const int PublicKeyLength = 32;

    /// <summary>
    ///     Verify the signed message
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        return Ed25519.Verify(signature, KeySpan, message);
    }

    /// <summary>
    ///     Checks if this object is a valid Ed25519 PublicKey
    /// </summary>
    /// <returns>Returns true if it is a valid key, false otherwise</returns>
    public bool IsOnCurve()
    {
        return KeySpan.IsOnCurve();
    }

    #region Encodings

    private string? _keyEncoded;

    private PublicKeyValue _keyDecoded;
    private bool _isDecoded;

    /// <summary>
    ///     Public key represented as base58-encoded string
    /// </summary>
    public string Key => _keyEncoded ??= Base58.EncodeData(KeySpan);

    /// <summary>
    ///     Public key represented as <see cref="ReadOnlySpan{byte}" />
    /// </summary>
    public ReadOnlySpan<byte> KeySpan => KeyValue.AsSpan();

    public ref readonly PublicKeyValue KeyValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!_isDecoded)
            {
                if (_keyEncoded == null) throw new InvalidOperationException();
                Span<byte> tmp = stackalloc byte[PublicKeyLength];
                Base58.TryDecodeData(_keyEncoded, tmp, out _);
                _keyDecoded = PublicKeyValue.Create(tmp);
                _isDecoded = true;
            }

            return ref _keyDecoded;
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    ///     Initialize the public key from the given <see cref="ReadOnlySpan{T}" />
    /// </summary>
    /// <param name="key">The public key as <see cref="ReadOnlySpan{T}" /></param>
    public PublicKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != PublicKeyLength)
            throw new ArgumentOutOfRangeException(nameof(key), "Invalid key length");
        _keyDecoded = PublicKeyValue.Create(key);
        _isDecoded = true;
    }

    /// <summary>
    ///     Initialize the public key from the given <see cref="PublicKeyValue" />
    /// </summary>
    public PublicKey(ref readonly PublicKeyValue key)
    {
        _keyDecoded = key;
        _isDecoded = true;
    }

    /// <summary>
    ///     Initialize the public key from the given base58-encoded <see cref="string" />
    /// </summary>
    /// <param name="key">The public key as base58-encoded <see cref="string" /></param>
    public PublicKey(string key)
    {
        _keyEncoded = key;
    }

    #endregion

    #region Implict casts

    public static implicit operator PublicKey(string encodedKey)
    {
        return new PublicKey(encodedKey);
    }

    public static implicit operator PublicKey(ReadOnlySpan<byte> rawKey)
    {
        return new PublicKey(rawKey);
    }

    public static implicit operator string(PublicKey key)
    {
        return key.Key;
    }

    public static implicit operator ReadOnlySpan<byte>(PublicKey key)
    {
        return key.KeySpan;
    }

    #endregion

    #region Overrides

    /// <inheritdoc cref="Equals(object)" />
    public override bool Equals(object? obj)
    {
        if (obj is PublicKey pk) return Equals(pk);
        return false;
    }

    /// <inheritdoc cref="GetHashCode()" />
    public override int GetHashCode()
    {
        return KeyValue.GetHashCode();
    }

    private bool Equals(PublicKey other)
    {
        if (_isDecoded && other._isDecoded)
            return _keyDecoded == other._keyDecoded;

        if (_keyEncoded != null && other._keyEncoded != null && !_isDecoded && !other._isDecoded)
            return _keyEncoded == other._keyEncoded;

        return KeyValue.Equals(in other.KeyValue);
    }

    /// <inheritdoc cref="ToString" />
    public override string ToString()
    {
        return Key;
    }

    #endregion

    #region Operators

    public static bool operator ==(PublicKey? lhs, PublicKey? rhs)
    {
        if (lhs is null && rhs is null) return true;
        if (lhs is null || rhs is null) return false;

        return lhs.Equals(rhs);
    }

    public static bool operator !=(PublicKey? lhs, PublicKey? rhs)
    {
        return !(lhs == rhs);
    }

    #endregion
}