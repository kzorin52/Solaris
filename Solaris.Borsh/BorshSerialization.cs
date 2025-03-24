using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Solaris.Borsh;

public ref struct BorshSerializer(Span<byte> buffer)
{
    public readonly Span<byte> Buffer = buffer;
    public int Offset = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        Buffer[Offset++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteZero(int cnt = 1)
    {
        Offset += cnt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Write(bool value)
    {
        Buffer[Offset++] = *(byte*)&value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void WriteInteger<T>(T value) where T : unmanaged
    {
        MemoryMarshal.Write(Buffer[Offset..], value);
        Offset += sizeof(T);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Write<T>(ref T value) where T : unmanaged
    {
        MemoryMarshal.Write(Buffer[Offset..], value);
        Offset += sizeof(T);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<byte> value)
    {
        value.CopyTo(Buffer[Offset..]);
        Offset += value.Length;
    }

    public void WriteRLE(int len)
    {
        var remLen = len;

        for (;;)
        {
            var elem = remLen & 0x7f;
            remLen >>= 7;
            if (remLen == 0)
            {
                Buffer[Offset] = (byte)elem;
                break;
            }

            elem |= 0x80;
            Buffer[Offset] = (byte)elem;
            Offset += 1;
        }
    }
}

public ref struct FluentSerializer
{
    private readonly byte[] _buffer;
    private BorshSerializer _serializer;

    public FluentSerializer(int size)
    {
        _buffer = new byte[size];
        _serializer = new BorshSerializer(_buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluentSerializer Write(ReadOnlySpan<byte> value)
    {
        _serializer.Write(value);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluentSerializer Write(byte value)
    {
        _serializer.Write(value);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluentSerializer Write(bool value)
    {
        _serializer.Write(value);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluentSerializer WriteInteger<T>(T value) where T : unmanaged
    {
        _serializer.WriteInteger(value);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluentSerializer Skip(int cnt)
    {
        _serializer.WriteZero(cnt);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Build()
    {
        return _buffer;
    }
}