using System.Diagnostics;
using z100emu.Core;

namespace z100emu.Peripheral
{
    public class Intel8253 : IPortDevice
    {

        private const byte MODE_LATCHCOUNT = 0;
        private const byte MODE_LOBYTE = 1;
        private const byte MODE_HIBYTE = 2;
        private const byte MODE_TOGGLE = 3;

        private const byte USE_LO_BYTE = 0;
        private const byte USE_HI_BYTE = 1;

        private const long OSC_FREQUENCY = 1193182;

        private ushort _channeldata;
        private byte _accessmode;
        private byte _bytetoggle;
        private uint _effdata;
        private double _chanfreq;
        public bool Active { get; set; }

        private ushort _counter;

        private long _tick_gap;
        private long _host_frequency;

        public Intel8253()
        {
            _host_frequency = Stopwatch.Frequency;
            Active = false;
        }

        public byte Read(int port)
        {
            byte current_byte = 0;

            if( ( _accessmode == MODE_LATCHCOUNT ) ||
                ( _accessmode == MODE_LOBYTE ) ||
                ( ( _accessmode == MODE_TOGGLE ) && _bytetoggle == USE_LO_BYTE ) )
            {
                current_byte = USE_LO_BYTE;
            }
            else if ((_accessmode == MODE_HIBYTE) ||
                ((_accessmode == MODE_TOGGLE) && _bytetoggle == USE_HI_BYTE))
            {
                current_byte = USE_HI_BYTE;
            }


            if ( (_accessmode == MODE_LATCHCOUNT) || (_accessmode == MODE_TOGGLE) )
            {
                // toggle between lo and hi
                _bytetoggle = (byte)((~_bytetoggle) & 0x01);
            }

            if( current_byte == USE_LO_BYTE )
            {
                return (byte)_counter;
            }
            else
            {
                return (byte)(_counter >> 8);
            }

        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte data)
        {
            byte current_byte = 0;
            if(port == 0x43) // mode/command register
            {
                _accessmode = (byte)((data >> 4) & 0x03);
                if( _accessmode == MODE_TOGGLE )
                {
                    _bytetoggle = USE_LO_BYTE;
                }
            }
            else
            {
                if( ( _accessmode == MODE_LOBYTE ) ||
                    ( ( _accessmode== MODE_TOGGLE) && ( _bytetoggle == USE_LO_BYTE) ) )
                {
                    current_byte = USE_LO_BYTE;
                }
                else if( ( _accessmode == MODE_HIBYTE ) ||
                    ( ( _accessmode == MODE_TOGGLE ) && ( _bytetoggle == USE_HI_BYTE ) ) )
                {
                    current_byte = USE_HI_BYTE;
                }

                if( current_byte == USE_LO_BYTE)
                {
                    _channeldata = (ushort)((_channeldata & 0xff00) | data);
                }
                else
                {
                    _channeldata = (ushort)((_channeldata & 0x00ff) | data);
                }

                if( _channeldata == 0 )
                {
                    _effdata = 0x10000;
                }
                else
                {
                    _effdata = _channeldata;
                }

                Active = true;

                _tick_gap = _host_frequency / (1193182 / _effdata);
                if( _accessmode == MODE_TOGGLE )
                {
                    // toggle between lo and hi
                    _bytetoggle = (byte)((~_bytetoggle) & 0x01);
                }

                _chanfreq = ((1193182.0 / (double)_effdata) * 1000.0) / 1000.0;
            }
        }

        public void Write16(int port, ushort data)
        {
            Write(port, (byte)data);
        }

        public int[] Ports => new[] {0xFB, 0xE4, 0xE5, 0xE6, 0xE7};
    }
}
