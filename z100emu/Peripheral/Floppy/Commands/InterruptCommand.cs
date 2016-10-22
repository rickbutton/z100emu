namespace z100emu.Peripheral.Floppy.Commands
{
    internal class InterruptCommand : ICommand
    {
        private WD1797 _w;

        public InterruptCommand(WD1797 w)
        {
            _w = w;
        }

        public CommandType Type => CommandType.Type4;

        public void Init()
        {
        }

        public bool Step(double us)
        {
            return true;
        }
    }
}