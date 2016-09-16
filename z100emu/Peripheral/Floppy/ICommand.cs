namespace z100emu.Peripheral.Floppy
{
    internal interface ICommand
    {
        CommandType Type { get; }
        void Init();
        bool Step(double us);
    }
}