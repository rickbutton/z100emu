#region License
// The MIT License (MIT)
// 
// Copyright (c) 2016 Rick Button
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using JetBrains.Annotations;
using z100emu.Core;
using z100emu.CPU;
using z100emu.Peripheral;
using z100emu.Peripheral.Floppy;
using z100emu.Peripheral.Floppy.Disk;
using z100emu.Peripheral.Floppy.Disk.Imd;
using z100emu.Peripheral.Zenith;
using z100emu.Ram;

namespace z100emu
{
    public delegate void DebugLineEventHandler(string line);
    public delegate void BreakpointHitEventHandler();

    public class ZenithSystem : INotifyPropertyChanged
    {
        private ZenithRom _rom;
        private Intel8259 _slavePic;
        private Intel8259 _masterPic;
        private Intel8253 _timer;
        private ZenithVideo _video;

        private IDisk _disk;

        private Cpu8086 _cpu;

        private StatusPort _masterStatus;
        private StatusPort _slaveStatus;
        private WD1797 _masterFloppy;
        private WD1797 _slaveFloppy;
        private ControlLatch _masterCl;
        private ControlLatch _slaveCl;

        private Stopwatch _sw;
        private long _steps = 0;
        private double _totalUs = 0;
        private double _totalTicks = 0;


        private const string Kernel32_DllName = "kernel32.dll";

        [DllImport(Kernel32_DllName)]
        private static extern bool AllocConsole();

        public byte[] DrawBuffer { get; private set; }
        public Zenith8041a Keyboard { get; private set; }
        public double Speed { get; private set; } = 0;
        public bool Debug { get; set; }
        public SystemStatus Status { get; private set; }
        public ZenithRam Ram { get; private set; }
        public List<Breakpoint> Breakpoints { get; private set; }
        public DebugLineEventHandler DebugLineEmitted { get; set; }
        public BreakpointHitEventHandler BreakpointHit { get; set; }

        public ushort? AX => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.AX) : null;
        public ushort? BX => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.BX) : null;
        public ushort? CX => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.CX) : null;
        public ushort? DX => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.DX) : null;
        public ushort? SI => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.SI) : null;
        public ushort? DI => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.DI) : null;
        public ushort? BP => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.BP) : null;
        public ushort? SP => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.SP) : null;
        public ushort? CS => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.CS) : null;
        public ushort? DS => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.DS) : null;
        public ushort? ES => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.ES) : null;
        public ushort? SS => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.SS) : null;
        public ushort? IP => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.IP) : null;

        public ushort? Flags => Status == SystemStatus.Paused ? _cpu?.GetRegister(Register.FLAGS) : null;

        public bool CarryFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Carry) ?? false);
        public bool ParityFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Parity) ?? false);
        public bool AuxFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Auxiliary) ?? false);
        public bool ZeroFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Zero) ?? false);
        public bool SignFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Sign) ?? false);
        public bool TrapFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Trap) ?? false);
        public bool InterruptFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Interrupt) ?? false);
        public bool DirectionFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Direction) ?? false);
        public bool OverflowFlag => Status == SystemStatus.Paused && (_cpu?.GetFlags().HasFlag(FlagsRegister.Overflow) ?? false);

        public bool Running => Status == SystemStatus.Running;
        public bool Paused => Status == SystemStatus.Paused;

        public ZenithSystem()
        {
        }

        public void Initialize()
        {
            AllocConsole();
            Console.BufferHeight = 32766;
            Console.WriteLine("z100emu");
            //Console.WriteLine("IMD Version: " + _disk.ImdVersion);
            //Console.WriteLine("IMD Date:    " + _disk.ImdDate);
            //Console.WriteLine("IMD Comment: " + _disk.ImdComment);
            
            _rom = ZenithRom.GetRom("mtr.bin");

            _slavePic = new Intel8259();
            _masterPic = new Intel8259(_slavePic);
            _timer = new Intel8253(_masterPic);
            Ram = new ZenithRam(512 * 1024, _masterPic);
            _video = new ZenithVideo(_masterPic);
            //_disk = new RawFloppy(File.ReadAllBytes("zdos4-1.img"));
            _disk = new RawFloppy(File.ReadAllBytes("bios402-msdos401-1.img"));
            //_disk = new RawFloppy(File.ReadAllBytes("zdos31-diag.img"));
            //_disk = new RawFloppy(File.ReadAllBytes("zdos31-1.img"));
            //_disk = new ImdFloppy(File.ReadAllBytes("msdosv11.imd"));

            _cpu = new Cpu8086(Ram, _masterPic);
            _cpu.DebugLineEmitted += DebugLine;

            Keyboard = new Zenith8041a(_masterPic);

            _masterStatus = new StatusPort(_slavePic, 0xB5);
            _masterFloppy = new WD1797(_masterStatus, 0xB0, _disk);
            _masterCl = new ControlLatch(0xB4);
            _slaveStatus = new StatusPort(_slavePic, 0xBD);
            _slaveFloppy = new WD1797(_slaveStatus, 0xB8, _disk);
            _slaveCl = new ControlLatch(0xBC);

            _steps = 0;
            _totalUs = 0;
            _totalTicks = 0;
            _sw = new Stopwatch();


            Ram.MapBank(_rom);
            Ram.MapBank(_video);

            _cpu.AttachPortDevice(_timer);

            _cpu.AttachPortDevice(_masterPic);
            _cpu.AttachPortDevice(_slavePic);

            _cpu.AttachPortDevice(new ZenithMemControl(Ram, _rom));
            _cpu.AttachPortDevice(new ZenithReserved());

            _cpu.AttachPortDevice(Keyboard);
            _cpu.AttachPortDevice(new ZenithParallel());
            _cpu.AttachPortDevice(new ZenithSerial(0xE8));
            _cpu.AttachPortDevice(new ZenithSerial(0xEC));
            _cpu.AttachPortDevice(new ZenithExpansion());

            _cpu.AttachPortDevice(new ZenithDIP());

            _cpu.AttachPortDevice(new ZenithWinchester());

            _cpu.AttachPortDevice(_masterFloppy);
            _cpu.AttachPortDevice(_masterCl);
            _cpu.AttachPortDevice(_masterStatus);
            _cpu.AttachPortDevice(_slaveFloppy);
            _cpu.AttachPortDevice(_slaveCl);
            _cpu.AttachPortDevice(_slaveStatus);

            _cpu.AttachPortDevice(_video);

            Breakpoints = new List<Breakpoint>();
            
            Status = SystemStatus.Paused;
            RefreshProps();

            //AddBreakpoint(0x51B9);
            //AddBreakpoint(0x40 * 0x10 + 0x4424); // timer
            //AddBreakpoint(0x40 * 0x10 + 0x44B6); // mem
            AddBreakpoint(0x40 * 0x10 + 0x450D); // mem
            AddBreakpoint(0x8716); // ?
        }
        
        public void Step() {
            try
            {
                RefreshProps();
                double cpuHertz = 4.77*1000000;

                _sw.Restart();
                double clocks = _cpu.ProcessSingleInstruction(Debug);
                _sw.Stop();
                double us = (clocks/cpuHertz)*1000000;

                UpdateSpeed(us);

                _timer.Step(us);
                DrawBuffer = _video.Step(us);
                _masterPic.Step();
                _slavePic.Step();
                _masterFloppy.Step(us);
                _slaveFloppy.Step(us);
                RefreshProps();

                var breakpoint = Breakpoints.FirstOrDefault(b => b.Address == GetCurrentAddress());
                if (breakpoint != null)
                {
                    Status = SystemStatus.Paused;
                    if (breakpoint.IsInternal)
                        RemoveBreakpoint(breakpoint.Address);
                    BreakpointHit?.Invoke();
                    RefreshProps(true);
                }
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("SYSTEM PAUSED");
                Status = SystemStatus.Paused;
                BreakpointHit?.Invoke();
                RefreshProps(true);
            }
        }

        public void StepOver()
        {
            var address = GetCurrentAddress();
            var length = GetCurrentInstructionLength();
            AddBreakpoint(address + length, true);
            Status = SystemStatus.Running;
            RefreshProps(true);
        }

        public void AddBreakpoint(int address)
        {
            AddBreakpoint(address, false); 
        }

        private void AddBreakpoint(int address, bool isInternal)
        {
            if (Breakpoints.All(b => b.Address != address))
            {
                Breakpoints.Add(new Breakpoint(address, isInternal)); 
            }
        }

        public void RemoveBreakpoint(int address)
        {
            Breakpoints.RemoveAll(b => b.Address == address);
        }

        public void InputKey(byte key)
        {
            if (Running)
                Keyboard.Input(key);
        }

        public void Run()
        {
            Status = SystemStatus.Running;
            RefreshProps(true);
        }

        public void Break()
        {
            Status = SystemStatus.Paused;
            RefreshProps(true);
        }

        public void Reset()
        {
            Status = SystemStatus.Resetting;
            RefreshProps(true);
        }

        public int GetCurrentAddress()
        {
            return _cpu.GetRegister(Register.CS)*0x10 + _cpu.GetRegister(Register.IP);
        }

        public int GetCurrentInstructionLength()
        {
            var fetcher = new Fetcher8086(_cpu);
            var instruction = OpCodeManager.Decode(fetcher);
            return fetcher.GetBytesFetched();
        }

        public string GetCurrentInstructionString()
        {
            var fetcher = new Fetcher8086(_cpu);
            var instruction = OpCodeManager.Decode(fetcher);
            var instructionText = $"{_cpu.GetRegister(Register.CS):X4}:{_cpu.GetRegister(Register.IP):X4} ";
            instructionText += InstructionStringHelper.OutputInstruction(instruction);
            return instructionText;
        }

        private void RefreshProps(bool force = false)
        {
            if (Status == SystemStatus.Paused || force)
            {
                OnPropertyChanged(nameof(AX));
                OnPropertyChanged(nameof(BX));
                OnPropertyChanged(nameof(CX));
                OnPropertyChanged(nameof(DX));
                OnPropertyChanged(nameof(SI));
                OnPropertyChanged(nameof(DI));
                OnPropertyChanged(nameof(BP));
                OnPropertyChanged(nameof(SP));
                OnPropertyChanged(nameof(CS));
                OnPropertyChanged(nameof(DS));
                OnPropertyChanged(nameof(ES));
                OnPropertyChanged(nameof(SS));
                OnPropertyChanged(nameof(IP));
                OnPropertyChanged(nameof(Flags));
                OnPropertyChanged(nameof(CarryFlag));
                OnPropertyChanged(nameof(ParityFlag));
                OnPropertyChanged(nameof(AuxFlag));
                OnPropertyChanged(nameof(ZeroFlag));
                OnPropertyChanged(nameof(SignFlag));
                OnPropertyChanged(nameof(TrapFlag));
                OnPropertyChanged(nameof(InterruptFlag));
                OnPropertyChanged(nameof(DirectionFlag));
                OnPropertyChanged(nameof(OverflowFlag));

                OnPropertyChanged(nameof(Paused));
                OnPropertyChanged(nameof(Running));
            }
        }

        private void UpdateSpeed(double us)
        {
            _totalUs += us;
            _totalTicks += _sw.ElapsedTicks;
            _steps++;
            if (_steps == 100000)
            {
                Speed = _totalUs / (_totalTicks * 1000000 / Stopwatch.Frequency) * 100;
                _steps = 0;
                _totalUs = 0;
                _totalTicks = 0;
            }
        }

        private void DebugLine(string line)
        {
            DebugLineEmitted?.Invoke(line);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
