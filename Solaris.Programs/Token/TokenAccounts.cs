using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Programs.Token;

public class Mint : IAccountData, IBorshDeserializable<Mint>
{
    public const int Size = 82; // 4 + 32 + 8 + 1 + 1 + 4 + 32
    
    public PublicKey? PublicKey { get; set; }
    
    public PublicKey? MintAuthority; // 0
    public ulong Supply; // 36
    public byte Decimals; // 44
    public bool IsInitialized; // 45
    public PublicKey? FreezeAuthority; // 46
    
    public static Mint Deserialize(ref BorshDeserializer des)
    {
        var mint = new Mint();

        if (des.Integer<uint>() != 0) mint.MintAuthority = des.PublicKey();
        else des.Skip(32);

        mint.Supply = des.Integer<ulong>();
        mint.Decimals = des.Byte();
        mint.IsInitialized = des.Bool();

        if (des.Integer<uint>() != 0) mint.FreezeAuthority = des.PublicKey();

        return mint;
    }
}

public class TokenAccount : IAccountData, IBorshDeserializable<TokenAccount>
{
    public const int Size = 165; // 32 + 32 + 8 + 4 + 32 + 1 + 4 + 8 + 8 + 4 + 32

    public PublicKey? PublicKey { get; set; }
    
    public PublicKey Mint; // 0
    public PublicKey Owner; // 32
    public ulong Amount; // 64
    public PublicKey? Delegate;
    public TokenAccountState State;
    public ulong? IsNative;
    public ulong DelegatedAmount;
    public PublicKey? CloseAuthority;

    public static TokenAccount Deserialize(ref BorshDeserializer des)
    {
        var ret = new TokenAccount
        {
            Mint = des.PublicKey(),
            Owner = des.PublicKey(),
            Amount = des.Integer<ulong>()
        };

        if (des.Integer<uint>() != 0) ret.Delegate = des.PublicKey();
        else des.Skip(32);

        ret.State = des.Integer<TokenAccountState>();

        if (des.Integer<uint>() != 0) ret.IsNative = des.Integer<ulong>();
        else des.Skip(8);

        ret.DelegatedAmount = des.Integer<ulong>();

        if (des.Integer<uint>() != 0) ret.CloseAuthority = des.PublicKey();

        return ret;
    }
}

public enum TokenAccountState : byte
{
    Uninitialized,
    Initialized,
    Frozen
}