using System.Diagnostics;
using Solaris.Base.Crypto;

namespace Solaris.Base.Account;

/// <summary>
/// Implements the public key functionality
/// </summary>
[DebuggerDisplay("PublicKey = {ToString()}")]
public partial class PublicKey
{
    /// <summary>
    /// Public key length
    /// </summary>
    public const int PublicKeyLength = 32;

    #region Encodings

    private string? _keyEncoded;
    private ReadOnlyMemory<byte>? _keyMemory;
    private byte[]? _keyBytes;

    /// <summary>
    /// Public key represented as base58-encoded string
    /// </summary>
    public string Key => _keyEncoded ??= Base58.EncodeData(KeyMemory.Span);

    /// <summary>
    /// Public key represented as <see cref="ReadOnlyMemory{T}"/>
    /// </summary>
    public ReadOnlyMemory<byte> KeyMemory
    {
        get
        {
            if (_keyMemory != null)
            {
                return _keyMemory.Value;
            }

            if (_keyBytes != null)
            {
                _keyMemory = _keyBytes;
                return _keyMemory!.Value;
            }

            if (_keyEncoded != null)
            {
                Memory<byte> memory = new byte[PublicKeyLength];
                Base58.TryDecodeData(_keyEncoded, memory.Span, out _);
                _keyMemory = memory;

                return _keyMemory.Value;
            }

            return null;
        }
    }

    /// <summary>
    /// Public key represented as byte[]
    /// </summary>
    public byte[] KeyBytes => _keyBytes ??= KeyMemory.ToArray(); // maybe ImmutableArray<byte>?

    #endregion

    #region Constructors

    /// <summary>
    /// Initialize the public key from the given byte array
    /// </summary>
    /// <param name="key">The public key as byte array</param>
    public PublicKey(byte[] key)
    {
        if (key.Length != PublicKeyLength)
            throw new ArgumentOutOfRangeException(nameof(key), "Invalid key length");
        _keyBytes = key;
    }

    /// <summary>
    /// Initialize the public key from the given <see cref="ReadOnlyMemory{T}"/>
    /// </summary>
    /// <param name="key">The public key as <see cref="ReadOnlyMemory{T}"/></param>
    public PublicKey(ReadOnlyMemory<byte> key)
    {
        if (key.Length != PublicKeyLength)
            throw new ArgumentOutOfRangeException(nameof(key), "Invalid key length");
        _keyMemory = key;
    }

    /// <summary>
    /// Initialize the public key from the given base58-encoded <see cref="string"/>
    /// </summary>
    /// <param name="key">The public key as base58-encoded <see cref="string"/></param>
    public PublicKey(string key)
    {
        _keyEncoded = key;
    }

    #endregion

    #region Implict casts

    public static implicit operator PublicKey(string encodedKey) => new(encodedKey);
    public static implicit operator PublicKey(byte[] rawKey) => new(rawKey);
    public static implicit operator PublicKey(ReadOnlyMemory<byte> rawKey) => new(rawKey);

    public static implicit operator string(PublicKey key) => key.Key;
    public static implicit operator byte[](PublicKey key) => key.KeyBytes;
    public static implicit operator ReadOnlyMemory<byte>(PublicKey key) => key.KeyMemory;

    #endregion

    /// <summary>
    /// Verify the signed message
    /// </summary>
    /// <param name="message"></param>
    /// <param name="messageOffset">Message offset, when null sets to zero</param>
    /// <param name="messageLength">Message length, when null sets to <paramref name="message"/>.Length</param>
    /// <param name="signature"></param>
    /// <param name="signatureOffset">Signature offset, when null sets to zero</param>
    /// <returns></returns>
    public bool Verify(byte[] message, int? messageOffset, int? messageLength, byte[] signature, int? signatureOffset)
    {
        return Org.BouncyCastle.Math.EC.Rfc8032.Ed25519.Verify(signature, signatureOffset ?? 0, KeyBytes, 0, message, messageOffset ?? 0, messageLength ?? message.Length);
    }
    
    /// <summary>
    /// Checks if this object is a valid Ed25519 PublicKey.
    /// </summary>
    /// <returns>Returns true if it is a valid key, false otherwise.</returns>
    public bool IsOnCurve()
    {
        return KeyMemory.Span.IsOnCurve();
    }

    #region Overrides

    /// <inheritdoc cref="Equals(object)"/>
    public override bool Equals(object? obj)
    {
        if (obj is PublicKey pk) return Equals(pk);
        return false;
    }

    /// <inheritdoc cref="GetHashCode()"/>
    public override int GetHashCode()
    {
        return ByteHelpers.FastHashCode(KeyMemory.Span);
    }

    protected bool Equals(PublicKey other)
    {
        if (other._keyEncoded != null && _keyEncoded != null)
        {
            return other._keyEncoded == _keyEncoded;
        }

        return KeyMemory.Span.SequenceEqual(other.KeyMemory.Span);
    }

    /// <inheritdoc cref="ToString"/>
    public override string ToString() => Key;

    #endregion

    #region Operators

    public static bool operator ==(PublicKey? lhs, PublicKey? rhs)
    {
        if (lhs is null && rhs is null)
        {
            return true;
        }

        if (lhs is null || rhs is null)
        {
            return false;
        }

        return lhs.Equals(rhs);
    }

    public static bool operator !=(PublicKey? lhs, PublicKey? rhs) => !(lhs == rhs);

    #endregion
}