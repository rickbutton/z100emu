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
using System.Diagnostics;
using System.IO;
using SDL2;
using z100emu.Core;
using z100emu.CPU;
using z100emu.Peripheral;
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

            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) != 0)
                throw new InvalidOperationException();
            var window = SDL.SDL_CreateWindow("zenemu", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, 64, 64, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (window == IntPtr.Zero)
                throw new InvalidOperationException();
            var renderer = SDL.SDL_CreateRenderer(window, -1, 0);
            if (renderer == IntPtr.Zero)
                throw new InvalidOperationException();

            var rom = ZenithRom.GetRom("rom.bin");

            var slave8259 = new Intel8259();
            var master8259 = new Intel8259(slave8259);

            using (var video = new ZenithVideo(window, renderer, master8259))
            {
                var ram = new ZenithRam(1024 * 1024, master8259);
                ram.MapBank(rom);

                ICpu cpu = new Cpu8086(ram, master8259);

                var timer = new Intel8253(master8259);
                cpu.AttachPortDevice(timer);

                cpu.AttachPortDevice(master8259);
                cpu.AttachPortDevice(slave8259);

                cpu.AttachPortDevice(new ZenithMemControl(ram, rom));
                cpu.AttachPortDevice(new ZenithReserved());

                cpu.AttachPortDevice(new Zenith8041a());
                cpu.AttachPortDevice(new ZenithParallel());

                cpu.AttachPortDevice(new ZenithDIP());

                cpu.AttachPortDevice(new ZenithWinchester());

                cpu.AttachPortDevice(new ZenithFloppy(master8259));

                ram.MapBank(video);
                cpu.AttachPortDevice(video);


                var debug = false;

                var quit = false;
                while (!quit)
                {
                    /*SDL.SDL_Event evt;
                    while (SDL.SDL_PollEvent(out evt) != 0)
                    {
                        if (evt.type == SDL.SDL_EventType.SDL_QUIT)
                            quit = true;
                    }*/
                    SDL.SDL_Event evt;
                    SDL.SDL_PollEvent(out evt);
                    if (evt.type == SDL.SDL_EventType.SDL_QUIT)
                        quit = true;

                    timer.Step();
                    video.Step();
                    cpu.ProcessSingleInstruction(debug);
                }
            }

            SDL.SDL_DestroyRenderer(renderer);
            SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();
        }
    }
}
