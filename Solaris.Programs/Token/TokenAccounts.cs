using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Programs.Token;

public class Mint
{
    public const int Size = 82; // 4 + 32 + 8 + 1 + 1 + 4 + 32

    public PublicKey? PublicKey; // meta property

    public PublicKey? MintAuthority;
    public ulong Supply;
    public byte Decimals;
    public bool IsInitialized;
    public PublicKey? FreezeAuthority;

    public Mint(ReadOnlySpan<byte> data, PublicKey? publicKey = null)
    {
        Update(data, publicKey);
    }

    public void Update(ReadOnlySpan<byte> raw, PublicKey? publicKey = null)
    {
        PublicKey = publicKey;
        var des = new BorshDeserializer(raw);

        if (des.Integer<uint>() != 0)
            MintAuthority = des.PublicKey();

        Supply = des.Integer<ulong>();
        Decimals = des.Byte();
        IsInitialized = des.Bool();

        if (des.Integer<uint>() != 0)
            FreezeAuthority = des.PublicKey();
    }
}

public class TokenAccount
{
    public const int Size = 165; // 32 + 32 + 8 + 4 + 32 + 1 + 4 + 8 + 8 + 4 + 32

    public PublicKey? PublicKey; // meta property

    public PublicKey Mint;
    public PublicKey Owner;
    public ulong Amount;
    public PublicKey? Delegate;
    public TokenAccountState State;
    public ulong? IsNative;
    public ulong DelegatedAmount;
    public PublicKey? CloseAuthority;

    public TokenAccount(ReadOnlySpan<byte> data)
    {
        Update(data);
    }

    public void Update(ReadOnlySpan<byte> raw)
    {
        var des = new BorshDeserializer(raw);

        Mint = des.PublicKey();
        Owner = des.PublicKey();
        Amount = des.Integer<ulong>();

        if (des.Integer<uint>() != 0)
            Delegate = des.PublicKey();

        State = (TokenAccountState)des.Byte();

        if (des.Integer<uint>() != 0)
            IsNative = des.Integer<ulong>();

        DelegatedAmount = des.Integer<ulong>();

        if (des.Integer<uint>() != 0)
            CloseAuthority = des.PublicKey();
    }
}

public enum TokenAccountState : byte
{
    Uninitialized,
    Initialized,
    Frozen
}