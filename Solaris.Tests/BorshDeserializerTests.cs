using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Borsh;

namespace Solaris.Tests;

[TestClass]
public class BorshDeserializerTests
{
    [TestMethod]
    [DataRow(0, new byte[] { 0 })]
    [DataRow(1, new byte[] { 1 })]
    [DataRow(127, new byte[] { 127 })]
    [DataRow(128, new byte[] { 128, 1 })]
    [DataRow(16383, new byte[] { 255, 127 })]
    [DataRow(16384, new byte[] { 128, 128, 1 })]
    [DataRow(65535, new byte[] { 255, 255, 3 })]
    public void CompactU16ConsumesOnlyItsCanonicalEncoding(int expected, byte[] encoded)
    {
        var reader = new BorshDeserializer([42, .. encoded, 99]);
        reader.Skip(1);

        Assert.AreEqual(expected, reader.CompactU16());
        Assert.AreEqual(1, reader.Remaining);
        Assert.AreEqual((byte)99, reader.Byte());
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 128 })]
    [DataRow(new byte[] { 128, 128 })]
    [DataRow(new byte[] { 128, 0 })]
    [DataRow(new byte[] { 129, 0 })]
    [DataRow(new byte[] { 128, 128, 0 })]
    [DataRow(new byte[] { 255, 255, 4 })]
    [DataRow(new byte[] { 255, 255, 128 })]
    [DataRow(new byte[] { 128, 128, 128, 0 })]
    public void CompactU16RejectsTruncationAliasesAndOverflow(byte[] encoded)
    {
        Assert.ThrowsExactly<FormatException>(() => ReadCompactU16(encoded));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TruncatedSpansAndSkipsDoNotConsumeTheRemainingByte(bool skip)
    {
        var reader = new BorshDeserializer(new byte[] { 42 });
        var rejected = false;
        try
        {
            if (skip) reader.Skip(2);
            else reader.Span(2);
        }
        catch (FormatException)
        {
            rejected = true;
        }

        Assert.IsTrue(rejected);
        Assert.AreEqual((byte)42, reader.Byte());
    }

    private static int ReadCompactU16(byte[] encoded)
    {
        var reader = new BorshDeserializer(encoded);
        return reader.CompactU16();
    }
}
