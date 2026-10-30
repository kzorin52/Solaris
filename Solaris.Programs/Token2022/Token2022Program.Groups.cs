using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeTokenGroup(PublicKey group, PublicKey mint,
        PublicKey mintAuthority, PublicKey? updateAuthority, ulong maxSize)
    {
        var data = new FluentSerializer(48)
            .Write([121, 113, 108, 39, 54, 51, 0, 4])
            .Write(updateAuthority ?? SystemProgram.ProgramId)
            .WriteInteger(maxSize)
            .Build();
        return Instruction(data,
        [
            AccountMeta.Writable(group),
            AccountMeta.ReadOnly(mint),
            AccountMeta.ReadOnly(mintAuthority, true)
        ]);
    }

    public static TransactionInstruction UpdateTokenGroupMaxSize(PublicKey group, PublicKey updateAuthority,
        ulong maxSize)
    {
        var data = new FluentSerializer(16)
            .Write([108, 37, 171, 143, 248, 30, 18, 110])
            .WriteInteger(maxSize)
            .Build();
        return Instruction(data,
        [
            AccountMeta.Writable(group),
            AccountMeta.ReadOnly(updateAuthority, true)
        ]);
    }

    public static TransactionInstruction UpdateTokenGroupUpdateAuthority(PublicKey group, PublicKey updateAuthority,
        PublicKey? newUpdateAuthority)
    {
        var data = new FluentSerializer(40)
            .Write([161, 105, 88, 1, 237, 221, 216, 203])
            .Write(newUpdateAuthority ?? SystemProgram.ProgramId)
            .Build();
        return Instruction(data,
        [
            AccountMeta.Writable(group),
            AccountMeta.ReadOnly(updateAuthority, true)
        ]);
    }

    public static TransactionInstruction InitializeTokenGroupMember(PublicKey member, PublicKey memberMint,
        PublicKey memberMintAuthority, PublicKey group, PublicKey groupUpdateAuthority)
    {
        var data = new FluentSerializer(8)
            .Write([152, 32, 222, 176, 223, 237, 116, 134])
            .Build();
        return Instruction(data,
        [
            AccountMeta.Writable(member),
            AccountMeta.ReadOnly(memberMint),
            AccountMeta.ReadOnly(memberMintAuthority, true),
            AccountMeta.Writable(group),
            AccountMeta.ReadOnly(groupUpdateAuthority, true)
        ]);
    }
}
