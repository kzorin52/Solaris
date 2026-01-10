using System.Runtime.CompilerServices;
using Solaris.Base.Account;

namespace Solaris.Transactions.Models;

public class AccountMeta
{
    public required PublicKey Account;
    public required bool IsSigner;
    public required bool IsWritable;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AccountMeta Writable(PublicKey account, bool isSigner = false)
    {
        return new AccountMeta
        {
            Account = account,
            IsSigner = isSigner,
            IsWritable = true
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AccountMeta ReadOnly(PublicKey account, bool isSigner = false)
    {
        return new AccountMeta
        {
            Account = account,
            IsSigner = isSigner,
            IsWritable = false
        };
    }
}

public class CompiledAccountMeta
{
    public bool IsSigner;
    public bool IsWritable;

    public static CompiledAccountMeta FromAccountMeta(AccountMeta accountMeta)
    {
        return new CompiledAccountMeta
        {
            IsSigner = accountMeta.IsSigner,
            IsWritable = accountMeta.IsWritable
        };
    }
}