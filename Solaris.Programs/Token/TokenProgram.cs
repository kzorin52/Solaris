using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token;

public static class TokenProgram
{
    public static readonly PublicKey ProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    public static TransactionInstruction Transfer(PublicKey sourceTokenAccount, PublicKey destinationTokenAccount,
        ulong amount, PublicKey authority)
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

    public static TransactionInstruction CloseAccount(PublicKey sourceTokenAccount, PublicKey feeDestination,
        PublicKey authority)
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

    public static TransactionInstruction SetAuthority(PublicKey account, PublicKey currentAuthority, 
        AuthorityType authorityType, PublicKey? newAuthority) // 6
    {
        var data = new FluentSerializer(1 + 1 + 1 + 32);
        data.Write(6)
            .Write((byte)authorityType)
            .Write(newAuthority != null ? (byte)1 : (byte)0)
            .Write(newAuthority ?? SystemProgram.ProgramId);
        
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = 
            [
                AccountMeta.Writable(account),
                AccountMeta.ReadOnly(currentAuthority, true)
            ],
            Data = data.Build()
        };
    }
}
public enum AuthorityType : byte {
    /// Authority to mint new tokens
    MintTokens,
    /// Authority to freeze any account associated with the Mint
    FreezeAccount,
    /// Owner of a given token account
    AccountOwner,
    /// Authority to close a token account
    CloseAccount,
}