﻿#region License
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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using z100emu.Core;
using z100emu.CPU;
using z100emu.Peripheral;
using z100emu.Peripheral.Floppy;
using z100emu.Peripheral.Floppy.Disk.Imd;
using z100emu.Peripheral.Zenith;
using z100emu.Ram;

namespace z100emu.GUI
{
    internal static class Program
    {
        private static void Main()
        {
            Console.BufferHeight = 32766;
            Console.WriteLine("z100emu");

            var rom = ZenithRom.GetRom("v1-2.bin");

            var slave8259 = new Intel8259();
            var master8259 = new Intel8259(slave8259);

            var video = new ZenithVideo(master8259);
            var ram = new ZenithRam(1024 * 1024, master8259);
            ram.MapBank(rom);

            var disk = new ImdFloppy(File.ReadAllBytes("msd2131.imd"));
            Console.WriteLine("IMD Version: " + disk.ImdVersion);
            Console.WriteLine("IMD Date:    " + disk.ImdDate);
            Console.WriteLine("IMD Comment: " + disk.ImdComment);

            ICpu cpu = new Cpu8086(ram, master8259);

            var timer = new Intel8253(master8259);
            cpu.AttachPortDevice(timer);

            cpu.AttachPortDevice(master8259);
            cpu.AttachPortDevice(slave8259);

            cpu.AttachPortDevice(new ZenithMemControl(ram, rom));
            cpu.AttachPortDevice(new ZenithReserved());

            var kb = new Zenith8041a(master8259);

            cpu.AttachPortDevice(kb);
            cpu.AttachPortDevice(new ZenithParallel());
            cpu.AttachPortDevice(new ZenithSerial(0xE8));
            cpu.AttachPortDevice(new ZenithSerial(0xEC));
            cpu.AttachPortDevice(new ZenithExpansion());

            cpu.AttachPortDevice(new ZenithDIP());

            cpu.AttachPortDevice(new ZenithWinchester());

            var masterStatus = new StatusPort(slave8259, 0xB5);
            var masterFloppy = new WD1797(masterStatus, 0xB0, disk);
            var masterCl = new ControlLatch(0xB4);
            var slaveStatus = new StatusPort(slave8259, 0xBD);
            var slaveFloppy = new WD1797(slaveStatus, 0xB8, disk);
            var slaveCl = new ControlLatch(0xBC);

            cpu.AttachPortDevice(masterFloppy);
            cpu.AttachPortDevice(masterCl);
            cpu.AttachPortDevice(masterStatus);
            cpu.AttachPortDevice(slaveFloppy);
            cpu.AttachPortDevice(slaveCl);
            cpu.AttachPortDevice(slaveStatus);

            ram.MapBank(video);
            cpu.AttachPortDevice(video);


            var debug = false;

            double cpuHertz = 4.77 * 1000000;

            Stopwatch sw = new Stopwatch();

            var quit = false;
            while (!quit)
            {
                sw.Restart();
                double clocks = cpu.ProcessSingleInstruction(debug);
                sw.Stop();
                double realUs = sw.ElapsedTicks * TimeSpan.TicksPerMillisecond * 1000;
                double us = (clocks / cpuHertz) * 1000000;
                timer.Step(us);
                video.Step(us);
                master8259.Step();
                slave8259.Step();
                masterFloppy.Step(us);
                slaveFloppy.Step(us);
            }
        }
    }
}
