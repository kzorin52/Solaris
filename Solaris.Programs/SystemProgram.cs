using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public static class SystemProgram
{
    public static readonly PublicKey ProgramId = "11111111111111111111111111111111";

    public static TransactionInstruction Transfer(PublicKey from, PublicKey to, ulong lamports)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys =
            [
                AccountMeta.Writable(from, true),
                AccountMeta.Writable(to)
            ],
            Data = new FluentSerializer(12).WriteInteger(2u).WriteInteger(lamports).Build()
        };
    }
}