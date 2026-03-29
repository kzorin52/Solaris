using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Solaris.Base.Account;

namespace Solaris.Borsh;

public ref struct BorshDeserializer(ReadOnlySpan<byte> data)
{
    public int Offset;
    private readonly ReadOnlySpan<byte> _data = data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T Integer<T>() where T : unmanaged
    {
        var size = sizeof(T);

        var num = MemoryMarshal.Read<T>(_data.Slice(Offset, size));
        Offset += size;

        return num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> Span(int size)
    {
        var span = _data.Slice(Offset, size);
        Offset += size;

        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int size)
    {
        Offset += size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PublicKey PublicKey()
    {
        return Span(32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Byte()
    {
        return _data[Offset++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Bool()
    {
        return _data[Offset++] == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string String()
    {
        var valueByteCount = Integer<int>();
        return Encoding.UTF8.GetString(Span(valueByteCount).Trim((byte)0));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Deserialize<T>() where T : IBorshDeserializable<T>
    {
        return T.Deserialize(ref this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ReadOnlySpan<T> Integers<T>(int length) where T : unmanaged
    {
        var size = sizeof(T) * length;
        var arr = MemoryMarshal.Cast<byte, T>(_data.Slice(Offset, size));
        Offset += size;
        return arr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] ArrayOf<T>(int length) where T : IBorshDeserializable<T>
    {
        var arr = new T[length];
        for (var i = 0; i < length; i++)
            arr[i] = T.Deserialize(ref this);
        return arr;
    }
}

public static class BorshDeserializationExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Deserialize<T>(this ReadOnlySpan<byte> data) where T : IBorshDeserializable<T>
    {
        var des = new BorshDeserializer(data);
        return T.Deserialize(ref des);
    } // for nested structs

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Deserialize<T>(this ReadOnlySpan<byte> data, PublicKey? publicKey)
        where T : IBorshDeserializable<T>, IAccountData
    {
        var des = new BorshDeserializer(data);
        var account = T.Deserialize(ref des);
        account.PublicKey = publicKey;
        return account;
    } // for top-level structs

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T Integer<T>(this ReadOnlySpan<byte> data, int offset = 0) where T : unmanaged
    {
        return MemoryMarshal.Read<T>(data.Slice(offset, sizeof(T)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PublicKey PublicKey(this ReadOnlySpan<byte> data, int offset = 0)
    {
        return new PublicKey(data.Slice(offset, 32).ToArray());
    }
}

public interface IBorshDeserializable<out TSelf> where TSelf : IBorshDeserializable<TSelf>
{
    static abstract TSelf Deserialize(ref BorshDeserializer des);
}

public interface IAccountData
{
    PublicKey? PublicKey { get; set; }
}