using Org.BouncyCastle.Math.EC.Rfc8032;

namespace Solaris.Base.Crypto;

public static class Ed25519Extensions
{
    /// <summary>
    ///     Checks whether the PublicKey bytes are 'On The Curve'
    /// </summary>
    /// <param name="key">PublicKey as byte array</param>
    /// <returns></returns>
    public static bool IsOnCurve(this byte[] key)
    {
        return Ed25519.ValidatePublicKeyPartial(key);
    }

    /// <summary>
    ///     Checks whether the PublicKey bytes are 'On The Curve'
    /// </summary>
    /// <param name="key">PublicKey as <see cref="ReadOnlySpan{T}" /></param>
    /// <returns></returns>
    public static bool IsOnCurve(this ReadOnlySpan<byte> key)
    {
        return Ed25519.ValidatePublicKeyPartial(key);
    }
}