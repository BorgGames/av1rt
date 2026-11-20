// Port of libaom aom_ports/mem_ops.h

global using static Av1rt.Aom.mem_ops;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] C renames every function with mem_ops_wrap_symbol (mem_get_le32 -> mem_get_le32_as_int)
// and callers use the unwrapped macro name; the port uses the name callers write. MAU_T is
// unsigned char (Ptr<byte>), MEM_VALUE_T is int.
// [subset] Only the functions that run in the subset are ported (mem_get_le32, mem_put_le16,
// mem_put_le24, mem_put_le32); the big-endian and signed variants never run.
internal static partial class mem_ops
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint mem_get_le32(Ptr<byte> vmem)
    {
        uint val;
        Ptr<byte> mem = vmem;

        val = ((uint)mem[3]) << 24;
        val |= (uint)(mem[2] << 16);
        val |= (uint)(mem[1] << 8);
        val |= mem[0];
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void mem_put_le16(Ptr<byte> vmem, int val)
    {
        Ptr<byte> mem = vmem;

        mem[0] = (byte)((val >> 0) & 0xff);
        mem[1] = (byte)((val >> 8) & 0xff);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void mem_put_le24(Ptr<byte> vmem, int val)
    {
        Ptr<byte> mem = vmem;

        mem[0] = (byte)((val >> 0) & 0xff);
        mem[1] = (byte)((val >> 8) & 0xff);
        mem[2] = (byte)((val >> 16) & 0xff);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void mem_put_le32(Ptr<byte> vmem, int val)
    {
        Ptr<byte> mem = vmem;

        mem[0] = (byte)((val >> 0) & 0xff);
        mem[1] = (byte)((val >> 8) & 0xff);
        mem[2] = (byte)((val >> 16) & 0xff);
        mem[3] = (byte)((val >> 24) & 0xff);
    }
}
