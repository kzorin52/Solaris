using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Solaris.Base.Crypto;

namespace Solaris.Base.Account;

public partial class PublicKey
{
    // class of pure insanity and chaos
    
    private const int MaxSeedsCount = 16;
    private static ReadOnlySpan<byte> ProgramDerivedAddressBytes => "ProgramDerivedAddress"u8;

    #region TryCreateProgramAddress

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryCreateProgramAddressCore(
        ReadOnlySpan<byte> programId,
        Span<byte> buffer,
        int seedsLength,
        out PublicKey publicKey)
    {
        // buffer layout: [seeds...][programId][ProgramDerivedAddress]
        programId.CopyTo(buffer[seedsLength..]);
        ProgramDerivedAddressBytes.CopyTo(buffer[(seedsLength + PublicKeyLength)..]);

        var hash = SHA256.HashData(buffer);
        publicKey = new PublicKey(hash);

        return !hash.IsOnCurve();
    }

    public static bool TryCreateProgramAddress(
        PublicKey programId,
        out PublicKey publicKey,
        ReadOnlySpan<byte> seed0)
    {
        var seedsLen = seed0.Length;
        var len = seedsLen + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        seed0.CopyTo(buffer);

        return TryCreateProgramAddressCore(programId.KeySpan, buffer, seedsLen, out publicKey);
    }

    public static bool TryCreateProgramAddress(
        PublicKey programId,
        out PublicKey publicKey,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1)
    {
        var seedsLen = seed0.Length + seed1.Length;
        var len = seedsLen + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        seed0.CopyTo(buffer);
        seed1.CopyTo(buffer[seed0.Length..]);

        return TryCreateProgramAddressCore(programId.KeySpan, buffer, seedsLen, out publicKey);
    }

    public static bool TryCreateProgramAddress(
        PublicKey programId,
        out PublicKey publicKey,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1,
        ReadOnlySpan<byte> seed2)
    {
        var seedsLen = seed0.Length + seed1.Length + seed2.Length;
        var len = seedsLen + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        var off = 0;
        seed0.CopyTo(buffer[off..]); off += seed0.Length;
        seed1.CopyTo(buffer[off..]); off += seed1.Length;
        seed2.CopyTo(buffer[off..]);

        return TryCreateProgramAddressCore(programId.KeySpan, buffer, seedsLen, out publicKey);
    }

    public static bool TryCreateProgramAddress(
        PublicKey programId,
        out PublicKey publicKey,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1,
        ReadOnlySpan<byte> seed2,
        ReadOnlySpan<byte> seed3)
    {
        var seedsLen = seed0.Length + seed1.Length + seed2.Length + seed3.Length;
        var len = seedsLen + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        var off = 0;
        seed0.CopyTo(buffer[off..]); off += seed0.Length;
        seed1.CopyTo(buffer[off..]); off += seed1.Length;
        seed2.CopyTo(buffer[off..]); off += seed2.Length;
        seed3.CopyTo(buffer[off..]);

        return TryCreateProgramAddressCore(programId.KeySpan, buffer, seedsLen, out publicKey);
    }

    public static bool TryCreateProgramAddress(
        PublicKey programId,
        out PublicKey publicKey,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1,
        ReadOnlySpan<byte> seed2,
        ReadOnlySpan<byte> seed3,
        ReadOnlySpan<byte> seed4)
    {
        var seedsLen = seed0.Length + seed1.Length + seed2.Length + seed3.Length + seed4.Length;
        var len = seedsLen + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        var off = 0;
        seed0.CopyTo(buffer[off..]); off += seed0.Length;
        seed1.CopyTo(buffer[off..]); off += seed1.Length;
        seed2.CopyTo(buffer[off..]); off += seed2.Length;
        seed3.CopyTo(buffer[off..]); off += seed3.Length;
        seed4.CopyTo(buffer[off..]);

        return TryCreateProgramAddressCore(programId.KeySpan, buffer, seedsLen, out publicKey);
    }

    #endregion

    #region FindProgramAddress

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (PublicKey? Key, byte Bump) FindProgramAddressCore(
        ReadOnlySpan<byte> programId,
        Span<byte> buffer,
        int seedsLength)
    {
        // buffer layout: [seeds...][bump][programId][ProgramDerivedAddress]
        ref var bump = ref buffer[seedsLength];

        programId.CopyTo(buffer[(seedsLength + 1)..]);
        ProgramDerivedAddressBytes.CopyTo(buffer[(seedsLength + 1 + PublicKeyLength)..]);

        Span<byte> hash = stackalloc byte[32];

        bump = 255;
        while (bump != 0)
        {
            SHA256.TryHashData(buffer, hash, out _);
            if (!hash.IsOnCurve()) return (new PublicKey(hash), bump);
            --bump;
        }

        return (null, 0);
    }

    public static (PublicKey? Key, byte Bump) FindProgramAddress(
        PublicKey programId,
        ReadOnlySpan<byte> seed0)
    {
        var seedsLen = seed0.Length;
        var len = seedsLen + 1 + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        seed0.CopyTo(buffer);

        return FindProgramAddressCore(programId.KeySpan, buffer, seedsLen);
    }

    public static (PublicKey? Key, byte Bump) FindProgramAddress(
        PublicKey programId,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1)
    {
        var seedsLen = seed0.Length + seed1.Length;
        var len = seedsLen + 1 + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        seed0.CopyTo(buffer);
        seed1.CopyTo(buffer[seed0.Length..]);

        return FindProgramAddressCore(programId.KeySpan, buffer, seedsLen);
    }

    public static (PublicKey? Key, byte Bump) FindProgramAddress(
        PublicKey programId,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1,
        ReadOnlySpan<byte> seed2)
    {
        var seedsLen = seed0.Length + seed1.Length + seed2.Length;
        var len = seedsLen + 1 + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        var off = 0;
        seed0.CopyTo(buffer[off..]); off += seed0.Length;
        seed1.CopyTo(buffer[off..]); off += seed1.Length;
        seed2.CopyTo(buffer[off..]);

        return FindProgramAddressCore(programId.KeySpan, buffer, seedsLen);
    }

    public static (PublicKey? Key, byte Bump) FindProgramAddress(
        PublicKey programId,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1,
        ReadOnlySpan<byte> seed2,
        ReadOnlySpan<byte> seed3)
    {
        var seedsLen = seed0.Length + seed1.Length + seed2.Length + seed3.Length;
        var len = seedsLen + 1 + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        var off = 0;
        seed0.CopyTo(buffer[off..]); off += seed0.Length;
        seed1.CopyTo(buffer[off..]); off += seed1.Length;
        seed2.CopyTo(buffer[off..]); off += seed2.Length;
        seed3.CopyTo(buffer[off..]);

        return FindProgramAddressCore(programId.KeySpan, buffer, seedsLen);
    }

    public static (PublicKey? Key, byte Bump) FindProgramAddress(
        PublicKey programId,
        ReadOnlySpan<byte> seed0,
        ReadOnlySpan<byte> seed1,
        ReadOnlySpan<byte> seed2,
        ReadOnlySpan<byte> seed3,
        ReadOnlySpan<byte> seed4)
    {
        var seedsLen = seed0.Length + seed1.Length + seed2.Length + seed3.Length + seed4.Length;
        var len = seedsLen + 1 + PublicKeyLength + ProgramDerivedAddressBytes.Length;
        Span<byte> buffer = stackalloc byte[len];

        var off = 0;
        seed0.CopyTo(buffer[off..]); off += seed0.Length;
        seed1.CopyTo(buffer[off..]); off += seed1.Length;
        seed2.CopyTo(buffer[off..]); off += seed2.Length;
        seed3.CopyTo(buffer[off..]); off += seed3.Length;
        seed4.CopyTo(buffer[off..]);

        return FindProgramAddressCore(programId.KeySpan, buffer, seedsLen);
    }

    #endregion

    #region CreateWithSeed

    /// <summary>
    ///     Derives a new public key from an existing public key and seed
    /// </summary>
    public static PublicKey CreateWithSeed(PublicKey fromPublicKey, ReadOnlySpan<byte> seed, PublicKey programId)
    {
        var len = PublicKeyLength + seed.Length + PublicKeyLength;
        Span<byte> buffer = stackalloc byte[len];

        fromPublicKey.KeySpan.CopyTo(buffer);
        seed.CopyTo(buffer[PublicKeyLength..]);
        programId.KeySpan.CopyTo(buffer[(PublicKeyLength + seed.Length)..]);

        Span<byte> hash = stackalloc byte[32];
        SHA256.TryHashData(buffer, hash, out _);

        return new PublicKey(hash);
    }

    #endregion
}