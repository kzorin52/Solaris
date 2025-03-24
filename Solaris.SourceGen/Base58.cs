using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Solaris.SourceGen;

internal static class Base58
{
    public const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly FrozenDictionary<char, int> ALPHABET_DIC = Enumerable.Range(0, ALPHABET.Length).ToFrozenDictionary(t => ALPHABET[t], t => t);
    public static readonly BigInteger Base58BI = new BigInteger(58);

    public static byte[] DecodePlain(string data)
    {
        BigInteger result;
        {
            result = BigInteger.Zero;

            foreach (var c in data)
            {
                var digit = ALPHABET_DIC.ContainsKey(c) ? ALPHABET_DIC[c] : -1;
                if (digit == -1)
                {
                    throw new FormatException(string.Format("Invalid Base58 character `{0}`", c));
                }

                result = result * Base58BI + digit;
            }
        }

        // Faster than TakeWhile
        int prefixZeroCount;
        for (prefixZeroCount = 0;
             (prefixZeroCount < data.Length) && (data[prefixZeroCount] == '1');
             prefixZeroCount++)
        {
        }

        var resultReversed = Enumerable.Reverse<byte>(result
            .ToByteArray()).ToArray();

        int firstNonZero;
        for (firstNonZero = 0;
             (firstNonZero < resultReversed.Length) && (resultReversed[firstNonZero] == 0);
             firstNonZero++)
        {
        }

        var revValueLength = resultReversed.Length - firstNonZero;
        var realOutput = new byte[prefixZeroCount + revValueLength];
        Array.Copy(resultReversed, firstNonZero, realOutput, prefixZeroCount, revValueLength);

        return realOutput;
    }
}