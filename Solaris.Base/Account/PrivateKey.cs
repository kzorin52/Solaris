﻿using System.Diagnostics;
using Solaris.Base.Crypto;

namespace Solaris.Base.Account
{
    /// <summary>
    /// Implements the private key functionality
    /// </summary>
    [DebuggerDisplay("PrivateKey = {ToString()}")]
    public class PrivateKey
    {
        /// <summary>
        /// Private key length
        /// </summary>
        public const int PrivateKeyLength = 64;
        
        /// <summary>
        /// Secret key length
        /// </summary>
        public const int SecretKeyLength = 32;

        #region Encodings

        private string? _keyEncoded;
        private ReadOnlyMemory<byte>? _keyMemory;
        private byte[]? _keyBytes;
        private byte[]? _secretKeyBytes;
        private PublicKey? _publicKey;

        /// <summary>
        /// Corresponding <see cref="PublicKey"/>
        /// </summary>
        public PublicKey PublicKey => _publicKey ??= KeyMemory[32..];

        /// <summary>
        /// Private key represented as base58-encoded string
        /// </summary>
        public string Key => _keyEncoded ??= Base58.EncodeData(KeyMemory.Span);

        /// <summary>
        /// Private key represented as <see cref="ReadOnlyMemory{T}"/>
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
                    Memory<byte> memory = new byte[PrivateKeyLength];
                    Base58.TryDecodeData(_keyEncoded, memory.Span, out _);
                    _keyMemory = memory;

                    return _keyMemory.Value;
                }

                return null;
            }
        }

        /// <summary>
        /// Private key represented as byte[]
        /// </summary>
        public byte[] KeyBytes => _keyBytes ??= KeyMemory.ToArray();
        private byte[] SecretKeyBytes => _secretKeyBytes ??= KeyMemory[..32].ToArray();

        #endregion

        #region Constructors

        /// <summary>
        /// Initialize the private key from the given byte array
        /// </summary>
        /// <param name="key">The private or secret key as byte array</param>
        public PrivateKey(byte[] key)
        {
            _keyMemory = key.Length switch
            {
                PrivateKeyLength => key,
                SecretKeyLength => ExpandSecretKey(key),
                _ => throw new ArgumentOutOfRangeException(nameof(key), key.Length, "Private key should be 32 or 64 bytes")
            };
        }

        /// <summary>
        /// Initialize the private key from the given <see cref="ReadOnlyMemory{T}"/>
        /// </summary>
        /// <param name="key">The private or secret key as <see cref="ReadOnlyMemory{T}"/></param>
        public PrivateKey(ReadOnlyMemory<byte> key)
        {
            _keyMemory = key.Length switch
            {
                PrivateKeyLength => key,
                SecretKeyLength => ExpandSecretKey(key.Span),
                _ => throw new ArgumentOutOfRangeException(nameof(key), key.Length, "Private key should be 32 or 64 bytes")
            };
        }

        /// <summary>
        /// Initialize the private key from the given base58-encoded <see cref="string"/>
        /// </summary>
        /// <param name="key">The private key as base58-encoded <see cref="string"/></param>
        public PrivateKey(string key)
        {
            _keyEncoded = key;
        }

        #endregion
        
        #region Implict casts

        public static implicit operator PrivateKey(string encodedKey) => new(encodedKey);
        public static implicit operator PrivateKey(byte[] rawKey) => new(rawKey);
        public static implicit operator PrivateKey(ReadOnlyMemory<byte> rawKey) => new(rawKey);
        public static implicit operator PublicKey(PrivateKey key) => key.PublicKey;

        public static implicit operator string(PrivateKey key) => key.Key;
        public static implicit operator byte[](PrivateKey key) => key.KeyBytes;
        public static implicit operator ReadOnlyMemory<byte>(PrivateKey key) => key.KeyMemory;

        #endregion

        #region Overrides

        /// <inheritdoc cref="Equals(object)"/>
        public override bool Equals(object? obj)
        {
            if (obj is PrivateKey pk) return Equals(pk);
            return false;
        }

        /// <inheritdoc cref="GetHashCode()"/>
        public override int GetHashCode()
        {
            return ByteHelpers.FastHashCode(KeyMemory.Span);
        }

        protected bool Equals(PrivateKey other)
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

        public static bool operator ==(PrivateKey? lhs, PrivateKey? rhs)
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

        public static bool operator !=(PrivateKey? lhs, PrivateKey? rhs) => !(lhs == rhs);

        #endregion

        /// <summary>
        /// Sign the message
        /// </summary>
        /// <param name="message">The data to sign</param>
        /// <param name="messageOffset">Message offset, when null sets to zero</param>
        /// <param name="messageLength">Message length, when null sets to <paramref name="message"/>.Length</param>
        /// <returns>The signature of the data</returns>
        public byte[] Sign(byte[] message, int? messageOffset, int? messageLength)
        {
            var sig = new byte[64];
            Org.BouncyCastle.Math.EC.Rfc8032.Ed25519.Sign(SecretKeyBytes, 0, PublicKey.KeyBytes, 0, message, messageOffset ?? 0, messageLength ?? message.Length, sig, 0);

            return sig;
        }

        /// <summary>
        /// Validates private key
        /// </summary>
        /// <returns>Is current public key equal derived key</returns>
        public bool Validate()
        {
            var derivedPublicKey = GetPublicKey(SecretKeyBytes);
            return PublicKey.KeyMemory.Span.SequenceEqual(derivedPublicKey.KeyMemory.Span);
        }

        /// <summary>
        /// Derives Ed25519 public key from 32-byte secret key
        /// </summary>
        /// <param name="secretKey">32-byte Ed25519 secret key</param>
        /// <returns>Derived public key</returns>
        public static PublicKey GetPublicKey(ReadOnlySpan<byte> secretKey)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(secretKey.Length, SecretKeyLength, nameof(secretKey));

            Memory<byte> derivedPublicKey = new byte[PublicKey.PublicKeyLength];
            Org.BouncyCastle.Math.EC.Rfc8032.Ed25519.GeneratePublicKey(secretKey, derivedPublicKey.Span);

            return new PublicKey(derivedPublicKey);
        }

        private static Memory<byte> ExpandSecretKey(ReadOnlySpan<byte> secretKey)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(secretKey.Length, SecretKeyLength, nameof(secretKey));

            Memory<byte> expanded = new byte[PrivateKeyLength];
            var span = expanded.Span;

            secretKey.CopyTo(span[..32]);
            Org.BouncyCastle.Math.EC.Rfc8032.Ed25519.GeneratePublicKey(secretKey, span[32..]);

            return expanded;
        }
    }
}
