namespace Solaris.Base.Crypto;

/// <summary>
///     Basic operations with Base58 encoding
///     Proxy to SimpleBase library, which have best performance
/// </summary>
public static class Base58
{
    /// <summary>
    ///     Encode bytes span into base58-encoded <see cref="string" />
    /// </summary>
    /// <param name="data">Raw bytes</param>
    /// <returns>Base58-encoded <see cref="string" /></returns>
    public static string EncodeData(ReadOnlySpan<byte> data)
    {
        return SimpleBase.Base58.Bitcoin.Encode(data);
    }

    /// <summary>
    ///     Decode base58-encoded <see cref="string" /> to raw bytes
    /// </summary>
    /// <param name="data">Base58-encoded <see cref="string" /></param>
    /// <returns>Raw bytes</returns>
    public static byte[] DecodeData(ReadOnlySpan<char> data)
    {
        return SimpleBase.Base58.Bitcoin.Decode(data);
    }

    public static void TryEncodeData(ReadOnlySpan<byte> data, Span<char> result, out int bytesWritten)
    {
        var success = SimpleBase.Base58.Bitcoin.TryEncode(data, result, out bytesWritten);

        if (!success)
            throw new EncodingException(true);
    }

    /// <summary>
    ///     Decode base58-encoded <see cref="string" /> to preallocated span
    /// </summary>
    /// <param name="data">Base58-encoded <see cref="string" /></param>
    /// <param name="result">Raw bytes</param>
    /// <param name="bytesWritten">Count of written to <paramref name="result" /> bytes</param>
    /// <exception cref="EncodingException">Failed decoding</exception>
    public static void
        TryDecodeData(ReadOnlySpan<char> data, Span<byte> result,
            out int bytesWritten) // why exception? maybe bool-based TRY methods?
    {
        var success = SimpleBase.Base58.Bitcoin.TryDecode(data, result, out bytesWritten);

        if (!success)
            throw new EncodingException(false, data.ToString());
    }

    public static int GetSafeByteCountForDecoding(ReadOnlySpan<char> text)
    {
        return SimpleBase.Base58.Bitcoin.GetSafeByteCountForDecoding(text);
    }

    public static int GetSafeCharCountForEncoding(ReadOnlySpan<byte> data)
    {
        return SimpleBase.Base58.Bitcoin.GetSafeCharCountForEncoding(data);
    }
}

public class EncodingException(bool encoding, string? data = null)
    : Exception($"Failed while {(encoding ? "encoding bytes" : $"decoding string: {data}")}");