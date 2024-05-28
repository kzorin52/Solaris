namespace Solaris.Base.Crypto;

public static class ByteHelpers
{
    public static int FastHashCode(this byte[] array)
    {
        unchecked
        {
            return array.Aggregate(0, (current, b) => (current * 31) ^ b);
        }
    }

    public static int FastHashCode(ReadOnlySpan<byte> array)
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