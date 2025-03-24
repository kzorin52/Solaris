using System.Runtime.InteropServices;

namespace Solaris.Transactions.LowLevel;

[StructLayout(LayoutKind.Sequential)]
public struct CompiledInstruction
{
    public byte ProgramIdIndex;
    public byte[] KeyIndices;
    public byte[] Data;
}