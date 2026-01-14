using System.Text;
using Solaris.Base.Account;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public static class MemoV1Program
{
    public static readonly PublicKey ProgramId = "Memo1UhkJRfHyvLMcVucJwxXeuD728EqVDDwQDxFMNo";

    public static TransactionInstruction BuildMemo(string memo)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [],
            Data = Encoding.UTF8.GetBytes(memo)
        };
    }
}

public static class MemoV2Program
{
    public static readonly PublicKey ProgramId = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";
}