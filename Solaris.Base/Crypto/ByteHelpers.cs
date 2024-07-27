namespace Solaris.Base.Crypto;

public static class ByteHelpers
{
    public static int FastHashCode(this ReadOnlySpan<byte> array)
    {
        unchecked
        {
            var result = 0;
            foreach (var b in array)
                result = (result * 31) ^ b;
            return result;
        }
    }
}