using Solaris.Base.Account;
using Solaris.Programs.Token;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public static class AssociatedTokenAccountProgram
{
    public static readonly PublicKey ProgramId = "ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL";

    public static TransactionInstruction CreateAssociatedTokenAccount(PublicKey payer, PublicKey owner, PublicKey mint,
        PublicKey derivedAccount, bool idempotent = false)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys =
            [
                AccountMeta.Writable(payer, true),
                AccountMeta.Writable(derivedAccount),
                AccountMeta.ReadOnly(owner),
                AccountMeta.ReadOnly(mint),
                AccountMeta.ReadOnly(SystemProgram.ProgramId),
                AccountMeta.ReadOnly(TokenProgram.ProgramId),
                AccountMeta.ReadOnly(SysVars.Rent)
            ],
            Data = idempotent ? [0x01] : []
        };
    }

    public static PublicKey DeriveAssociatedTokenAccount(PublicKey owner, PublicKey mint, out byte bump)
    {
        var result = PublicKey.FindProgramAddress(ProgramId, owner, TokenProgram.ProgramId, mint);

        bump = result.Bump;
        return result.Key!;
    }
}