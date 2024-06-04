using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Solaris.Base.Crypto;

namespace Solaris.Base.Account;

public partial class PublicKey
{
    private static readonly byte[] ProgramDerivedAddressBytes = "ProgramDerivedAddress"u8.ToArray();
    
    /// <summary>
    /// Derives a program address
    /// </summary>
    /// <param name="seeds">The address seeds</param>
    /// <param name="programId">The program ID</param>
    /// <param name="publicKey">The derived public key, returned as inline out</param>
    /// <returns>true if it could derive the program address for the given seeds, otherwise false</returns>
    /// <exception cref="ArgumentException">Throws exception when one of the seeds has an invalid length</exception>
    public static bool TryCreateProgramAddress(ICollection<ReadOnlyMemory<byte>> seeds, PublicKey programId, out PublicKey publicKey)
    {
        using var buffer = new MemoryStream(PublicKeyLength * seeds.Count + ProgramDerivedAddressBytes.Length + PublicKeyLength /* programId */);

        foreach (var seed in seeds)
        {
            if (seed.Length > PublicKeyLength) throw new ArgumentOutOfRangeException(nameof(seeds), "Max seed length exceeded");
            buffer.Write(seed.Span);
        }

        buffer.Write(programId.KeyMemory.Span);
        buffer.Write(ProgramDerivedAddressBytes);

        buffer.Position = 0;
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
    /// Attempts to find a program address for the passed seeds and program ID
    /// </summary>
    /// <param name="seeds">The address seeds</param>
    /// <param name="programId">The program ID</param>
    /// <param name="address">The derived address, returned as inline out</param>
    /// <param name="bump">The bump used to derive the address, returned as inline out</param>
    /// <returns>True whenever the address for a nonce was found, otherwise false</returns>
    public static bool TryFindProgramAddress(ICollection<ReadOnlyMemory<byte>> seeds, PublicKey programId, [NotNullWhen(true)] out PublicKey? address, out byte bump)
    {
        Memory<byte> seedBump = new byte[] { 255 };
        var span = seedBump.Span;

        while (span[0] != 0)
        {
            var success = TryCreateProgramAddress([..seeds, seedBump], programId, out var derivedAddress);

            if (success)
            {
                address = derivedAddress;
                bump = span[0];
                return true;
            }

            span[0]--;
        }

        address = null;
        bump = 0;

        return false;
    }

    public static (PublicKey? Key, byte Bump) FindProgramAddress(PublicKey programId, params ReadOnlyMemory<byte>[] seeds)
    {
        Memory<byte> seedBump = new byte[] { 255 };
        var span = seedBump.Span;

        while (span[0] != 0)
        {
            var success = TryCreateProgramAddress([..seeds, seedBump], programId, out var derivedAddress);

            if (success)
            {
                return (derivedAddress, span[0]);
            }

            span[0]--;
        }

        return (null, 0);
    }

    /// <summary>
    /// Derives a new public key from an existing public key and seed
    /// </summary>
    /// <param name="fromPublicKey"></param>
    /// <param name="seed"></param>
    /// <param name="programId"></param>
    /// <returns>Derived public key</returns>
    public static PublicKey CreateWithSeed(PublicKey fromPublicKey, ReadOnlySpan<byte> seed, PublicKey programId)
    {
        using var buffer = new MemoryStream(PublicKeyLength * 2 + seed.Length);

        buffer.Write(fromPublicKey.KeyMemory.Span);
        buffer.Write(seed);
        buffer.Write(programId.KeyMemory.Span);

        var hash = SHA256.HashData(buffer);
        return new PublicKey(hash);
    }
}