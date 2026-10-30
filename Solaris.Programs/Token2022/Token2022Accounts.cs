using System.IO;
using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Programs.Token2022;

public sealed class Token2022Mint : IAccountData, IBorshDeserializable<Token2022Mint>
{
    public const int Size = 82;
    public PublicKey? PublicKey { get; set; }
    public PublicKey? MintAuthority { get; private init; }
    public ulong Supply { get; private init; }
    public byte Decimals { get; private init; }
    public bool IsInitialized { get; private init; }
    public PublicKey? FreezeAuthority { get; private init; }
    public Token2022Extensions Extensions { get; private init; } = Token2022Extensions.Empty;

    public static Token2022Mint Deserialize(ref BorshDeserializer des)
    {
        var length = des.Remaining;
        Token2022AccountReader.ValidateLength(length, Size);
        var result = new Token2022Mint
        {
            MintAuthority = Token2022AccountReader.OptionalKey(ref des),
            Supply = des.Integer<ulong>(),
            Decimals = des.Byte(),
            IsInitialized = Token2022AccountReader.Boolean(ref des),
            FreezeAuthority = Token2022AccountReader.OptionalKey(ref des),
            Extensions = Token2022AccountReader.Extensions(ref des, length, Size, Token2022AccountType.Mint)
        };
        return result;
    }
}

public sealed class Token2022Account : IAccountData, IBorshDeserializable<Token2022Account>
{
    public const int Size = 165;
    public PublicKey? PublicKey { get; set; }
    public PublicKey Mint { get; private init; } = null!;
    public PublicKey Owner { get; private init; } = null!;
    public ulong Amount { get; private init; }
    public PublicKey? Delegate { get; private init; }
    public Token2022AccountState State { get; private init; }
    public ulong? NativeReserve { get; private init; }
    public bool IsNative => NativeReserve.HasValue;
    public ulong DelegatedAmount { get; private init; }
    public PublicKey? CloseAuthority { get; private init; }
    public Token2022Extensions Extensions { get; private init; } = Token2022Extensions.Empty;

    public static Token2022Account Deserialize(ref BorshDeserializer des)
    {
        var length = des.Remaining;
        Token2022AccountReader.ValidateLength(length, Size);
        return new Token2022Account
        {
            Mint = des.PublicKey(),
            Owner = des.PublicKey(),
            Amount = des.Integer<ulong>(),
            Delegate = Token2022AccountReader.OptionalKey(ref des),
            State = Token2022AccountReader.AccountState(ref des),
            NativeReserve = Token2022AccountReader.OptionalU64(ref des),
            DelegatedAmount = des.Integer<ulong>(),
            CloseAuthority = Token2022AccountReader.OptionalKey(ref des),
            Extensions = Token2022AccountReader.Extensions(ref des, length, Size, Token2022AccountType.Account)
        };
    }
}

public sealed class Token2022Multisig : IAccountData, IBorshDeserializable<Token2022Multisig>
{
    public const int Size = 355;
    public const int MaximumSigners = 11;
    public PublicKey? PublicKey { get; set; }
    public byte RequiredSigners { get; private init; }
    public byte SignerCount { get; private init; }
    public bool IsInitialized { get; private init; }
    public ReadOnlyMemory<PublicKey> Signers { get; private init; }

    public static Token2022Multisig Deserialize(ref BorshDeserializer des)
    {
        if (des.Remaining != Size) throw new InvalidDataException("Invalid multisig length.");
        var required = des.Byte();
        var count = des.Byte();
        var initialized = Token2022AccountReader.Boolean(ref des);
        if (count > MaximumSigners || required > count || (initialized && required == 0))
            throw new InvalidDataException("Invalid multisig signer counts.");
        var signers = new PublicKey[MaximumSigners];
        for (var i = 0; i < MaximumSigners; i++) signers[i] = des.PublicKey();
        return new Token2022Multisig { RequiredSigners = required, SignerCount = count, IsInitialized = initialized, Signers = signers };
    }
}

public interface IToken2022Extension<TSelf> where TSelf : IToken2022Extension<TSelf>
{
    static abstract Token2022ExtensionType Type { get; }
    static abstract TSelf Decode(ReadOnlyMemory<byte> data);
}

public sealed class Token2022Extensions
{
    public static Token2022Extensions Empty { get; } = new(ReadOnlyMemory<byte>.Empty, Token2022AccountType.Uninitialized);
    private readonly ReadOnlyMemory<byte> _data;
    public Token2022AccountType AccountType { get; }
    public ReadOnlyMemory<byte> Data => _data;

    private Token2022Extensions(ReadOnlyMemory<byte> data, Token2022AccountType accountType)
    {
        _data = data;
        AccountType = accountType;
    }

    public static Token2022Extensions CopyFrom(ReadOnlySpan<byte> tlvData, Token2022AccountType accountType)
    {
        _ = new Token2022ExtensionView(tlvData, accountType);
        return new Token2022Extensions(tlvData.ToArray(), accountType);
    }

    public Token2022ExtensionView Borrow() => new(_data.Span, AccountType, false);
    public bool Contains(Token2022ExtensionType type) => TryGetRaw((ushort)type, out _);
    public bool TryGetRaw(Token2022ExtensionType type, out ReadOnlyMemory<byte> data) => TryGetRaw((ushort)type, out data);

    public bool TryGetRaw(ushort type, out ReadOnlyMemory<byte> data)
    {
        foreach (var extension in Borrow())
        {
            if (extension.RawType != type) continue;
            data = _data.Slice(extension.ValueOffset, extension.Data.Length);
            return true;
        }
        data = default;
        return false;
    }

    public T Get<T>() where T : IToken2022Extension<T>
    {
        if (!TryGetRaw(T.Type, out var data)) throw new KeyNotFoundException($"Extension {T.Type} was not found.");
        return T.Decode(data);
    }

    public bool TryGet<T>(out T value) where T : IToken2022Extension<T>
    {
        if (TryGetRaw(T.Type, out var data))
        {
            value = T.Decode(data);
            return true;
        }
        value = default!;
        return false;
    }
}

public readonly ref struct Token2022BorrowedExtension
{
    public ushort RawType { get; }
    public Token2022ExtensionType Type => (Token2022ExtensionType)RawType;
    public ReadOnlySpan<byte> Data { get; }
    internal int ValueOffset { get; }

    internal Token2022BorrowedExtension(ushort type, ReadOnlySpan<byte> data, int valueOffset)
    {
        RawType = type;
        Data = data;
        ValueOffset = valueOffset;
    }
}

public readonly ref struct Token2022ExtensionView
{
    private readonly ReadOnlySpan<byte> _data;
    public Token2022AccountType AccountType { get; }

    public Token2022ExtensionView(ReadOnlySpan<byte> tlvData, Token2022AccountType accountType) : this(tlvData, accountType, true) { }

    internal Token2022ExtensionView(ReadOnlySpan<byte> tlvData, Token2022AccountType accountType, bool validate)
    {
        if (accountType is not (Token2022AccountType.Mint or Token2022AccountType.Account) && (validate || !tlvData.IsEmpty))
            throw new InvalidDataException("Invalid extension account type.");
        _data = tlvData;
        AccountType = accountType;
        if (validate) Validate(tlvData, accountType);
    }

    public Enumerator GetEnumerator() => new(_data);

    public bool TryGetRaw(Token2022ExtensionType type, out ReadOnlySpan<byte> data) => TryGetRaw((ushort)type, out data);

    public bool TryGetRaw(ushort type, out ReadOnlySpan<byte> data)
    {
        foreach (var extension in this)
        {
            if (extension.RawType != type) continue;
            data = extension.Data;
            return true;
        }
        data = default;
        return false;
    }

    private static void Validate(ReadOnlySpan<byte> data, Token2022AccountType accountType)
    {
        var offset = 0;
        ulong seen = 0;
        while (offset < data.Length)
        {
            var remaining = data[offset..];
            if (remaining.Length == 1)
            {
                if (remaining[0] != 0) throw new InvalidDataException("Truncated extension type.");
                return;
            }
            var des = new BorshDeserializer(remaining);
            var type = des.Integer<ushort>();
            if (type == 0)
            {
                if (remaining.IndexOfAnyExcept((byte)0) >= 0) throw new InvalidDataException("Nonzero data follows uninitialized TLV padding.");
                return;
            }
            if (des.Remaining < 2) throw new InvalidDataException("Truncated extension length.");
            var length = des.Integer<ushort>();
            if (length > des.Remaining) throw new InvalidDataException("Truncated extension payload.");
            if (type <= (ushort)Token2022ExtensionType.PermissionedBurn)
            {
                var bit = 1UL << type;
                if ((seen & bit) != 0) throw new InvalidDataException("Duplicate extension.");
                seen |= bit;
                if (Token2022ExtensionSizes.GetAccountType((Token2022ExtensionType)type) != accountType)
                    throw new InvalidDataException("Extension account type mismatch.");
                Token2022ExtensionData.Validate((Token2022ExtensionType)type, des.Span(length));
            }
            else
            {
                var prior = new Enumerator(data[..offset]);
                while (prior.MoveNext())
                    if (prior.Current.RawType == type) throw new InvalidDataException("Duplicate extension.");
            }
            offset += 4 + length;
        }
    }

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;
        public Token2022BorrowedExtension Current { get; private set; }

        internal Enumerator(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
            Current = default;
        }

        public bool MoveNext()
        {
            if (_data.Length - _offset < 2) return false;
            var des = new BorshDeserializer(_data[_offset..]);
            var type = des.Integer<ushort>();
            if (type == 0) return false;
            var length = des.Integer<ushort>();
            Current = new Token2022BorrowedExtension(type, des.Span(length), _offset + 4);
            _offset += 4 + length;
            return true;
        }
    }
}

internal static class Token2022AccountReader
{
    internal static void ValidateLength(int length, int baseSize)
    {
        if (length < baseSize || length == Token2022Multisig.Size || (length != baseSize && length < 166))
            throw new InvalidDataException("Invalid Token-2022 account length.");
    }

    internal static Token2022Extensions Extensions(ref BorshDeserializer des, int length, int baseSize, Token2022AccountType type)
    {
        if (length == baseSize) return Token2022Extensions.Empty;
        if (des.Span(165 - baseSize).IndexOfAnyExcept((byte)0) >= 0) throw new InvalidDataException("Invalid mint padding.");
        if (des.Byte() != (byte)type) throw new InvalidDataException("Invalid account type marker.");
        return Token2022Extensions.CopyFrom(des.Span(length - 166), type);
    }

    internal static bool Boolean(ref BorshDeserializer des)
    {
        var value = des.Byte();
        if (value > 1) throw new InvalidDataException("Invalid boolean.");
        return value == 1;
    }

    internal static Token2022AccountState AccountState(ref BorshDeserializer des)
    {
        var value = des.Byte();
        if (value > (byte)Token2022AccountState.Frozen) throw new InvalidDataException("Invalid token account state.");
        return (Token2022AccountState)value;
    }

    internal static PublicKey? OptionalKey(ref BorshDeserializer des)
    {
        var tag = des.Integer<uint>();
        var value = des.Span(32);
        return tag switch { 0 => null, 1 => new PublicKey(value), _ => throw new InvalidDataException("Invalid COption tag.") };
    }

    internal static ulong? OptionalU64(ref BorshDeserializer des)
    {
        var tag = des.Integer<uint>();
        var value = des.Integer<ulong>();
        return tag switch { 0 => null, 1 => value, _ => throw new InvalidDataException("Invalid COption tag.") };
    }
}
