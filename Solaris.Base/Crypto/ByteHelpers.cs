using System.Runtime.CompilerServices;

namespace Solaris.Base.Crypto;

public static class ByteHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FastHashCode(this ReadOnlySpan<byte> array)
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(array);
        return hashCode.ToHashCode();
    }
}