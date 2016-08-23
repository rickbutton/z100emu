namespace z100emu.Ram
{
    public interface IRam
    {
        byte this[int pos] { get; set; }
        int Length { get; }

        void MapBank(IRamBank bank);

        RamConfig RamConfig { get; set; }

        bool ZeroParity { get; set; }
        bool KillParity { get; set; }
        void ClearParityError();
    }
}
