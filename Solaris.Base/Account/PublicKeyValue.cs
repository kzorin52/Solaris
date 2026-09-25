using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Solaris.Base.Account;

[StructLayout(LayoutKind.Sequential, Size = 32)]
public readonly struct PublicKeyValue
    : IEquatable<PublicKeyValue>
{
    private readonly UInt128 _lower, _upper;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PublicKeyValue Create(ReadOnlySpan<byte> source)
    {
        return 32 > source.Length
            ? throw new ArgumentOutOfRangeException(nameof(source))
            : Unsafe.ReadUnaligned<PublicKeyValue>(ref MemoryMarshal.GetReference(source));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentOutOfRangeException(nameof(destination));

        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), this);
    }

    [UnscopedRef]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsSpan()
    {
        return MemoryMarshal.AsBytes(new ReadOnlySpan<PublicKeyValue>(in this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is PublicKeyValue other && Equals(in other);
    }

    [OverloadResolutionPriority(1)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ref readonly PublicKeyValue other)
    {
        return this == other;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PublicKeyValue other)
    {
        return this == other;
    }

    private const uint HashSeed = 2098026241U; // just a random prime number

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return GetHashCode(AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetHashCode(ReadOnlySpan<byte> source)
    {
        if (source.Length < 32)
            throw new ArgumentOutOfRangeException(nameof(source));

        ref byte b = ref MemoryMarshal.GetReference(source);

        if (Aes.IsSupported)
        {
            var key = Unsafe.ReadUnaligned<Vector128<byte>>(ref b);
            var data = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref b, 16));
            key ^= Vector128.CreateScalar(HashSeed).AsByte();
            var mixed = Aes.Encrypt(data, key);
            var compressed = mixed.AsUInt64().GetElement(0) ^ mixed.AsUInt64().GetElement(1);
            return (int)(uint)(compressed ^ (compressed >> 32));
        }

        if (System.Runtime.Intrinsics.Arm.Aes.IsSupported)
        {
            var key = Unsafe.ReadUnaligned<Vector128<byte>>(ref b);
            var data = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref b, 16));
            key ^= Vector128.CreateScalar(HashSeed).AsByte();
            var mixed = System.Runtime.Intrinsics.Arm.Aes.MixColumns(
                System.Runtime.Intrinsics.Arm.Aes.Encrypt(data, key));
            var compressed = mixed.AsUInt64().GetElement(0) ^ mixed.AsUInt64().GetElement(1);
            return (int)(uint)(compressed ^ (compressed >> 32));
        }

        var crc = HashSeed;
        crc = BitOperations.Crc32C(crc, Unsafe.ReadUnaligned<ulong>(ref b));
        crc = BitOperations.Crc32C(crc, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 8)));
        crc = BitOperations.Crc32C(crc, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 16)));
        crc = BitOperations.Crc32C(crc, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 24)));

        return (int)crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(ReadOnlySpan<byte> source, ref readonly PublicKeyValue other)
    {
        if (source.Length < 32)
            throw new ArgumentOutOfRangeException(nameof(source));

        var left = Unsafe.ReadUnaligned<Vector256<ulong>>(ref MemoryMarshal.GetReference(source));
        var right = Unsafe.As<PublicKeyValue, Vector256<ulong>>(ref Unsafe.AsRef(in other));
        return left == right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in PublicKeyValue left, in PublicKeyValue right)
    {
        var v1 = Unsafe.As<PublicKeyValue, Vector256<ulong>>(ref Unsafe.AsRef(in left));
        var v2 = Unsafe.As<PublicKeyValue, Vector256<ulong>>(ref Unsafe.AsRef(in right));
        return v1 == v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in PublicKeyValue left, in PublicKeyValue right)
    {
        var v1 = Unsafe.As<PublicKeyValue, Vector256<ulong>>(ref Unsafe.AsRef(in left));
        var v2 = Unsafe.As<PublicKeyValue, Vector256<ulong>>(ref Unsafe.AsRef(in right));
        return v1 != v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PublicKeyValue(ReadOnlySpan<byte> rawKey)
    {
        return Create(rawKey);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(in PublicKeyValue key)
    {
        return key.AsSpan();
    }
}

public static class PublicKeyValueExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PublicKeyValue AsPublicKeyValue(this in ReadOnlySpan<byte> key)
    {
        return PublicKeyValue.Create(key);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PublicKeyValue AsPublicKeyValue(this in Span<byte> key)
    {
        return PublicKeyValue.Create(key);
    }
}