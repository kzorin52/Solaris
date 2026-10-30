using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Programs;
using Solaris.Programs.Token2022;

namespace Solaris.Tests;

[TestClass]
public class Token2022ConfidentialTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, 32).ToArray());

    [TestMethod]
    public void MixedProofLocationsUseOneSysvarAndOnlyRequiredContexts()
    {
        var context = Token2022ProofLocation.ContextStateAccount(Key(8));
        var instruction = Token2022Program.ConfidentialTransfer(Key(1), Key(2), Key(3), new byte[36], new byte[64], new byte[64], Key(4),
            Token2022ProofLocation.InstructionOffset(-3), context, Token2022ProofLocation.InstructionOffset(2), [Key(5), Key(6)]);
        CollectionAssert.AreEqual(new[] { Key(1), Key(2), Key(3), SysVar.Instructions, Key(8), Key(4), Key(5), Key(6) }, instruction.Keys.Select(k => k.Account).ToArray());
        CollectionAssert.AreEqual(new byte[] { 253, 0, 2 }, instruction.Data[^3..]);
        Assert.IsFalse(instruction.Keys[5].IsSigner);
        Assert.IsTrue(instruction.Keys[6].IsSigner);
        Assert.IsTrue(instruction.Keys[7].IsSigner);
    }

    [TestMethod]
    public void InlineProofBundleAssignsOffsetsRelativeToTokenInstruction()
    {
        var instructions = Token2022Program.ConfidentialTransfer(Key(1), Key(2), Key(3), new byte[36], new byte[64], new byte[64], Key(4),
            Token2022ProofInput.Inline(new byte[320]), Token2022ProofInput.ContextStateAccount(Key(8)), Token2022ProofInput.Inline(new byte[1000]));
        Assert.HasCount(3, instructions);
        Assert.AreEqual(Token2022Program.ProgramId, instructions[0].ProgramId);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 2 }, instructions[0].Data[^3..]);
        Assert.AreEqual(Token2022Program.ZkElGamalProofProgramId, instructions[1].ProgramId);
        Assert.AreEqual((byte)3, instructions[1].Data[0]);
        Assert.AreEqual((byte)7, instructions[2].Data[0]);
        Assert.HasCount(321, instructions[1].Data);
        Assert.HasCount(1001, instructions[2].Data);
    }

    [TestMethod]
    public void ProofRecordBundleVerifiesBeforeTokenAndUsesOutputContext()
    {
        var context = new Token2022ProofContext(Key(8), Key(9));
        var instructions = Token2022Program.ConfigureConfidentialTransferAccount(Key(1), Key(2), new byte[36], 10, Key(3),
            Token2022ProofInput.RecordAccount(Key(7), 0x04030201, context));
        Assert.HasCount(2, instructions);
        CollectionAssert.AreEqual(new byte[] { 4, 1, 2, 3, 4 }, instructions[0].Data);
        CollectionAssert.AreEqual(new[] { Key(7), Key(8), Key(9) }, instructions[0].Keys.Select(k => k.Account).ToArray());
        Assert.IsTrue(instructions[0].Keys[1].IsWritable);
        Assert.IsFalse(instructions[0].Keys[2].IsSigner);
        Assert.AreEqual(Token2022Program.ProgramId, instructions[1].ProgramId);
        Assert.AreEqual((byte)0, instructions[1].Data[^1]);
        CollectionAssert.AreEqual(new[] { Key(1), Key(2), Key(8), Key(3) }, instructions[1].Keys.Select(k => k.Account).ToArray());
    }

    [TestMethod]
    public void ConfidentialPayloadSizesAndUnspecifiedProofLocationsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Token2022Program.VerifyPubkeyValidityProof(new byte[95]));
        Assert.ThrowsExactly<ArgumentException>(() => Token2022Program.ConfigureConfidentialTransferAccount(Key(1), Key(2), new byte[35], 1, Key(3), Token2022ProofLocation.ContextStateAccount(Key(4))));
        Assert.ThrowsExactly<ArgumentException>(() => Token2022Program.EmptyConfidentialTransferAccount(Key(1), Key(2), default(Token2022ProofLocation)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Token2022ProofLocation.InstructionOffset(0));
    }
}
