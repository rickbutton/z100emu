using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using z100emu.Core;

namespace z100emu.Peripheral.Floppy
{
    public class ControlLatch : IPortDevice
    {
        public enum DriveSelect { Drive0, Drive1, Drive2, Drive3 }

        public enum DriveType { Type525, Type8 }

        private static readonly int CTL_LAT_D_SEL_MASK = 0x3;
        private static readonly int CTL_LAT_D_SEL_SHFT = 0x0;
        private static readonly int CTL_LAT_D_TYP_MASK = 0x1;
        private static readonly int CTL_LAT_D_TYP_SHFT = 0x2;
        private static readonly int CTL_LAT_D_SEN_MASK = 0x1;
        private static readonly int CTL_LAT_D_SEN_SHFT = 0x3;
        private static readonly int CTL_LAT_PRECO_MASK = 0x1;
        private static readonly int CTL_LAT_PRECO_SHFT = 0x4;
        private static readonly int CTL_LAT_OVER8_MASK = 0x1;
        private static readonly int CTL_LAT_OVER8_SHFT = 0x5;
        private static readonly int CTL_LAT_WAITS_MASK = 0x1;
        private static readonly int CTL_LAT_WAITS_SHFT = 0x6;
        private static readonly int CTL_LAT_DOUBD_MASK = 0x1;
        private static readonly int CTL_LAT_DOUBD_SHFT = 0x7;

        public byte Value { get; set; }
        public DriveSelect Select => (DriveSelect)((Value >> CTL_LAT_D_SEL_SHFT) & CTL_LAT_D_SEL_MASK);
        public DriveType Type => (DriveType)((Value >> CTL_LAT_D_TYP_SHFT) & CTL_LAT_D_TYP_MASK);
        public bool SelectDrives => ((Value >> CTL_LAT_D_SEN_SHFT) & CTL_LAT_D_SEN_MASK) != 0;
        public bool Precomp => ((Value >> CTL_LAT_PRECO_SHFT) & CTL_LAT_PRECO_MASK) == 0;
        public bool Override8 => ((Value >> CTL_LAT_OVER8_SHFT) & CTL_LAT_OVER8_MASK) != 0;
        public bool WaitState => ((Value >> CTL_LAT_WAITS_SHFT) & CTL_LAT_WAITS_MASK) != 0;
        public bool SingleDensity => ((Value >> CTL_LAT_DOUBD_SHFT) & CTL_LAT_DOUBD_MASK) != 0;

        private int _port;

        public ControlLatch(int port)
        {
            _port = port;
        }

        public byte Read(int port)
        {
            return Value;
        }

        public void Write(int port, byte value)
        {
            Value = value;
        }

        public ushort Read16(int port) { return Read(port); }
        public void Write16(int port, ushort value) { }

        public int[] Ports => new [] { _port };
    }
}
