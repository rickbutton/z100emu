using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using z100emu.Core;

namespace z100emu.Peripheral
{
    public class Intel8253 : IPortDevice
    {
        public enum CounterMode
        {
            Mode0, Mode1, Mode2, Mode3, Mode4, Mode5
        }

        public enum CounterReadLoad
        {
            Latch, LeastSig, MostSig, LeastMostSig,
        }

        public class Counter
        {
            public bool Active = false;

            public ushort LatchedValue { get; set; }

            public ushort LastWrittenValue { get; set; }
            public ushort Value { get; set; }

            public bool BCD { get; set; }
            public CounterMode Mode  { get; set; }
            public CounterReadLoad ReadLoad { get; set; }

            public bool Output { get; set; } = false;

            private bool _rlType = false;

            public void SetClock(bool tick = true)
            {
                if (!Active)
                    return;

                if (Mode == CounterMode.Mode0)
                {
                    if (tick && Value >= 1) Value--;
                    Output = Value == 0;
                }
                else if (Mode == CounterMode.Mode1)
                {
                    throw new NotImplementedException();
                }
                else if (Mode == CounterMode.Mode2)
                {
                    throw new NotImplementedException();
                }
                else if (Mode == CounterMode.Mode3)
                {
                    if (tick && Value >= 1) Value--;
                    if (Value == 0) Value = LastWrittenValue;
                    Output = Value > (LastWrittenValue/2);
                }
                else if (Mode == CounterMode.Mode4)
                {
                    throw new NotImplementedException();
                }
                else if (Mode == CounterMode.Mode5)
                {
                    throw new NotImplementedException();
                }
            }

            public void Pulse()
            {
                SetClock();
            }

            public byte Read()
            {
                if (ReadLoad == CounterReadLoad.Latch)
                {
                    return (byte) LatchedValue;
                }
                else if (ReadLoad == CounterReadLoad.LeastSig)
                {
                    return (byte) Value;
                }
                else if (ReadLoad == CounterReadLoad.MostSig)
                {
                    return (byte) (Value >> 8);
                }
                else if (ReadLoad == CounterReadLoad.LeastMostSig)
                {
                    if (!_rlType)
                    {
                        _rlType = !_rlType;
                        return (byte) Value;
                    }
                    else
                    {
                        _rlType = !_rlType;
                        return (byte) (Value >> 8);
                    }
                }
                throw new InvalidOperationException();
            }

            public void Write(byte value)
            {
                if (ReadLoad == CounterReadLoad.Latch)
                {
                    throw new InvalidOperationException(); 
                }
                else if (ReadLoad == CounterReadLoad.LeastSig)
                {
                    Value = (ushort) ((Value & 0xFF00) + value);
                }
                else if (ReadLoad == CounterReadLoad.MostSig)
                {
                    Value = (ushort) ((value << 8) + ((byte) (Value)));
                }
                else if (ReadLoad == CounterReadLoad.LeastMostSig)
                {
                    if (!_rlType)
                    {
                        _rlType = !_rlType;
                        Value = (ushort)((Value & 0xFF00) + value);
                    }
                    else
                    {
                        _rlType = !_rlType;
                        Value = (ushort)((value << 8) + ((byte)(Value)));
                        Active = true;
                    }
                }
                LastWrittenValue = Value;
            }
        }

        private static int PORT_STATUS  = 0xFB;
        private static int PORT_COUNT0  = 0xE4;
        private static int PORT_COUNT1  = 0xE5;
        private static int PORT_COUNT2  = 0xE6;
        private static int PORT_CONTROL = 0xE7;

        private static int CTL_BCD = (1 << 0);

        private static int CTL_MODE_MASK = 0x7;
        private static int CTL_MODE_SHIFT = 1;
        private static int CTL_MODE_0 = 0x0;
        private static int CTL_MODE_1 = 0x1;
        private static int CTL_MODE_2 = 0x2;
        private static int CTL_MODE_3 = 0x3;
        private static int CTL_MODE_4 = 0x4;
        private static int CTL_MODE_5 = 0x5;

        private static int CTL_RL_MASK     = 0x3;
        private static int CTL_RL_SHIFT     = 4;
        private static int CTL_RL_LATCH     = 0x0;
        private static int CTL_RL_READ_LO   = 0x1;
        private static int CTL_RL_READ_HI   = 0x2;
        private static int CTL_RL_READ_LOHI = 0x3;
        
        private static int CTL_SEL_MASK = 0x3;
        private static int CTL_SEL_SHIFT = 6;
        private static int CTL_SEL_0 = 0;
        private static int CTL_SEL_1 = 1;
        private static int CTL_SEL_2 = 2;

        private double _us;

        private static long CLK_US = 4;

        public Counter CountZero { get; } = new Counter();
        public Counter CountOne { get; } = new Counter();
        public Counter CountTwo { get; } = new Counter();

        private bool timerZero = false;
        private bool timerOne = false;

        private Queue<byte> _timerHack = new Queue<byte>();

        private Intel8259 _pic;

        public Intel8253(Intel8259 pic)
        {
            _pic = pic;
            _us = 0;

            //for(var i = 0; i < 3; i++) _timerHack.Enqueue(1);
            //_timerHack.Enqueue(2);
            //for(var i = 0; i < 25; i++) _timerHack.Enqueue(0);
        }

        public void Step(double us)
        {
            _us += us;

            while (_us >= CLK_US)
            {
                var beforeZero = CountZero.Output;
                CountZero.Pulse();
                if (!beforeZero && CountZero.Output)
                    CountOne.Pulse();

                CountTwo.Pulse();

                if (CountZero.Output)
                    timerZero = true;
                if (CountTwo.Output)
                    timerOne = true;

                if (timerOne || timerZero)
                    _pic.RequestInterrupt(2);
                else
                    _pic.AckInterrupt(2);

                _us -= CLK_US;
            }
        }

        public byte Read(int port)
        {
            if (port == PORT_CONTROL)
                throw new InvalidOperationException();

            if (port == PORT_COUNT0)
            {
                return CountZero.Read();
            }
            else if (port == PORT_COUNT1)
            {
                return CountOne.Read();
            }
            else if (port == PORT_COUNT2)
            {
                return CountTwo.Read();
            }
            else if (port == PORT_STATUS)
            {
                if (_timerHack.Count > 0)
                    return _timerHack.Dequeue();

                return (byte)((timerZero ? 1 : 0) + ((timerOne ? 1 : 0) << 1));
            }

            throw new InvalidOperationException();
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte data)
        {
            if (port == PORT_CONTROL)
            {
                Counter counter = null;
                var counterNum = (data >> CTL_SEL_SHIFT) & CTL_SEL_MASK;
                var readLoad = (data >> CTL_RL_SHIFT) & CTL_RL_MASK;
                var mode = (data >> CTL_MODE_SHIFT) & CTL_MODE_MASK;
                var bcd = (data & 1) == 1;

                if (counterNum == 0)
                {
                    counter = CountZero;
                }
                else if (counterNum == 1)
                {
                    counter = CountOne;
                }
                else if (counterNum == 2)
                {
                    counter = CountTwo;
                }

                counter.ReadLoad = (CounterReadLoad) readLoad;
                counter.Mode = (CounterMode) mode;
                counter.BCD = bcd;

                if (counter.Mode == CounterMode.Mode0)
                    counter.Value = counter.LastWrittenValue;

                if (bcd)
                    throw new NotImplementedException();

                if (counter.ReadLoad == CounterReadLoad.Latch)
                    counter.LatchedValue = counter.Value;
            }
            else if (port == PORT_COUNT0)
            {
                timerZero = false;
                CountZero.Write(data);
            }
            else if (port == PORT_COUNT1)
            {
                CountOne.Write(data);
            }
            else if (port == PORT_COUNT2)
            {
                timerOne = false;
                CountTwo.Write(data);
            }
            else if (port == PORT_STATUS)
            {
                timerZero = (data & 1) == 1;
                timerOne = (data & 2) == 2;
            }
        }

        public void Write16(int port, ushort data)
        {
            Write(port, (byte)data);
        }

        public int[] Ports => new[] {0xFB, 0xE4, 0xE5, 0xE6, 0xE7};
    }
}
