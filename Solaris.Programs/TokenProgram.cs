using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public static class TokenProgram
{
    public static readonly PublicKey ProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    public static TransactionInstruction Transfer(PublicKey sourceTokenAccount, PublicKey destinationTokenAccount, ulong amount, PublicKey authority)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys =
            [
                AccountMeta.Writable(sourceTokenAccount),
                AccountMeta.Writable(destinationTokenAccount),
                AccountMeta.ReadOnly(authority, true)
            ],
            Data = new FluentSerializer(9).Write(3).WriteInteger(amount).Build()
        };
    }

    public static TransactionInstruction CloseAccount(PublicKey sourceTokenAccount, PublicKey feeDestination, PublicKey authority)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys =
            [
                AccountMeta.Writable(sourceTokenAccount),
                AccountMeta.Writable(feeDestination),
                AccountMeta.ReadOnly(authority, true)
            ],
            Data = [0x09]
        };
    }
}