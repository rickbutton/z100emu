namespace z100emu.CPU
{
    public enum Register : uint
    {
        AX = 0,
        CX = 1,
        DX = 2,
        BX = 3,

        SP = 4,
        BP = 5,
        SI = 6,
        DI = 7,

        ES = 8,
        CS = 9,
        SS = 10,
        DS = 11,

        IP = 12,

        AL = 0x80000000 | 0,
        CL = 0x80000000 | 1,
        DL = 0x80000000 | 2,
        BL = 0x80000000 | 3,
        AH = 0x80000000 | 4,
        CH = 0x80000000 | 5,
        DH = 0x80000000 | 6,
        BH = 0x80000000 | 7,

        FLAGS = 0x80000000 | 0xFF,

        Invalid = 0xFFFFFFFF
    }
}