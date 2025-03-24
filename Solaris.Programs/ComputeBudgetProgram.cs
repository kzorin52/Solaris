using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public class ComputeBudgetProgram
{
    public static readonly PublicKey ProgramId = "ComputeBudget111111111111111111111111111111";

    public static TransactionInstruction SetComputeUnitLimit(uint units)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [],
            Data = new FluentSerializer(5).Write(2).WriteInteger(units).Build()
        };
    }

    public static TransactionInstruction SetComputeUnitPrice(ulong priorityRate)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [],
            Data = new FluentSerializer(9).Write(3).WriteInteger(priorityRate).Build()
        };
    }
}