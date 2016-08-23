namespace z100emu.Ram
{
    public interface IRamBank
    {
        bool TryGet(int pos, out byte value);
        bool TrySet(int pos, byte value);
        int Length { get; }
    }
}
