namespace Elk.Vm;

static class Extensions
{
    public static (byte, byte) ToBytePair(this ushort value)
        => (
            (byte)((value >> 8) & 0xff),
            (byte)(value & 0xff)
        );

    public static ushort ToUshort(this byte left, byte right)
        => (ushort)((ushort)((left << 8) & 0xff) | (ushort)(right & 0xff));
}