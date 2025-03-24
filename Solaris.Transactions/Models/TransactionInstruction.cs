using Solaris.Base.Account;

namespace Solaris.Transactions.Models;

public class TransactionInstruction
{
    public byte[] Data;
    public AccountMeta[] Keys;
    public PublicKey ProgramId;
}