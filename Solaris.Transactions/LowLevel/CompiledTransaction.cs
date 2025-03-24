using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Solaris.Transactions.LowLevel;

// [StructLayout(LayoutKind.Sequential)]
// public ref struct CompiledTransaction
// {
//     public Signature[] Signatures;
//     public ref CompiledMessage Message;
// }
// not yet used.

// [StructLayout(LayoutKind.Sequential, Size = 64)]
// public struct Signature
// {
//     public static readonly Signature Empty = default;
// }

public static class Ext
{
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static unsafe Span<byte> AsSpan<T>(this T structure) where T : unmanaged
    // {
    //     return new Span<byte>(Unsafe.AsPointer(ref structure), sizeof(T));
    // }
}