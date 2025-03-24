using System.Security.Cryptography;
using Solaris.Base.Crypto;

namespace Solaris.Base.Account;

public partial class PublicKey
{
    private const int MaxSeedsCount = 16;
    private static readonly ReadOnlyMemory<byte> ProgramDerivedAddressBytes = "ProgramDerivedAddress"u8.ToArray();

    /// <summary>
    ///     Derives a program address
    /// </summary>
    /// <param name="seeds">The address seeds</param>
    /// <param name="programId">The program ID</param>
    /// <param name="publicKey">The derived public key, returned as inline out</param>
    /// <returns>true if it could derive the program address for the given seeds, otherwise false</returns>
    /// <exception cref="ArgumentOutOfRangeException">Throws exception when one of the seeds has an invalid length</exception>
    public static bool TryCreateProgramAddress(ICollection<byte[]> seeds, PublicKey programId, out PublicKey publicKey)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seeds.Count, MaxSeedsCount, nameof(seeds));

        var len = ProgramDerivedAddressBytes.Length + PublicKeyLength + seeds.Sum(x => x.Length); // pda header + programId + seeds len
        var buffer = len <= 1024 ? stackalloc byte[len] : new byte[len];

        var offset = 0;
        foreach (var seed in seeds)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(seed.Length, PublicKeyLength, nameof(seed));
            seed.AsSpan().CopyTo(buffer.Slice(offset, seed.Length));
            offset += seed.Length;
        }

        programId.KeyMemory.Span.CopyTo(buffer.Slice(offset, PublicKeyLength));
        offset += PublicKeyLength;

        ProgramDerivedAddressBytes.Span.CopyTo(buffer.Slice(offset, ProgramDerivedAddressBytes.Length));

        var hash = SHA256.HashData(buffer);
        if (hash.IsOnCurve())
        {
            publicKey = new PublicKey(hash);
            return false;
        }

        publicKey = new PublicKey(hash);
        return true;
    }

    /// <summary>
    ///     Attempts to find a program address for the passed seeds and program ID
    /// </summary>
    /// <param name="programId"></param>
    /// <param name="seeds"></param>
    /// <returns></returns>
    public static (PublicKey? Key, byte Bump) FindProgramAddress(PublicKey programId, params ReadOnlySpan<byte[]> seeds)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seeds.Length, MaxSeedsCount, nameof(seeds));

        var len = ProgramDerivedAddressBytes.Length + PublicKeyLength + 1;
        foreach (var seed in seeds) len += seed.Length;

        var buffer = len <= 1024 ? stackalloc byte[len] : new byte[len];

        var offset = 0;
        foreach (var seed in seeds)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(seed.Length, PublicKeyLength, nameof(seed));
            seed.AsSpan().CopyTo(buffer.Slice(offset, seed.Length));
            offset += seed.Length;
        }

        ref var bump = ref buffer[offset++];
        bump = 255;

        programId.KeyMemory.Span.CopyTo(buffer.Slice(offset, PublicKeyLength));
        offset += PublicKeyLength;
        ProgramDerivedAddressBytes.Span.CopyTo(buffer.Slice(offset, ProgramDerivedAddressBytes.Length));

        var hash = new byte[32];
        var hashSpan = hash.AsSpan();

        while (bump != 0)
        {
            SHA256.TryHashData(buffer, hashSpan, out _);
            if (!hash.IsOnCurve()) return (hash, bump);

            --bump;
        }

        return (null, bump);
    }

    /// <summary>
    ///     Derives a new public key from an existing public key and seed
    /// </summary>
    /// <param name="fromPublicKey"></param>
    /// <param name="seed"></param>
    /// <param name="programId"></param>
    /// <returns>Derived public key</returns>
    public static PublicKey CreateWithSeed(PublicKey fromPublicKey, ReadOnlySpan<byte> seed, PublicKey programId)
    {
        var hash = SHA256.HashData([..fromPublicKey.KeyMemory.Span, ..seed, ..programId.KeyMemory.Span]);
        return new PublicKey(hash);
    }
}