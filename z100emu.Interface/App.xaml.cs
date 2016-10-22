using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using z100emu;

namespace z100emu.Interface
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ZenithSystem _system;
        private MainWindow _window;
        private DebugWindow _debug;

        public App()
        {
            _system = new ZenithSystem();
            _window = new MainWindow();
            _debug = new DebugWindow(_system);

            _window.KeyEvent += KeyEvent;
            _system.DebugLineEmitted += _debug.DebugLine;

            _debug.Reset += Reset;
            _debug.Resume += Resume;
            _debug.Break += Break;
            _debug.Step += Step;
            _debug.StepOver += StepOver;
            _debug.DebugChecked += DebugChecked;
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            _system.Initialize();
            _window.Show();
            _debug.Show();

            _system.Run();

            Task.Run(() =>
            {
                while (true)
                {
                    if (_system.Status == SystemStatus.Running)
                    {
                        DoStep();
                    }
                    else if (_system.Status == SystemStatus.Resetting)
                    {
                        _system.Initialize();
                        _system.Run();
                    }
                }
            });
        }

        private void DoStep()
        {
            _system.Step();
            if (_system.DrawBuffer != null)
            {
                Dispatcher.Invoke(() =>
                {

                    _window.Draw(_system.DrawBuffer);
                    _window.Title = _system.Speed.ToString("N2");
                });
            }
        }

        private void KeyEvent(byte key)
        {
            _system.InputKey(key);
        }

        private void Reset()
        {
            _system.Reset();
        }

        private void Resume()
        {
            if (_system.Status == SystemStatus.Paused)
            {
                _system.Run();
            }
        }

        private void Break()
        {
            if (_system.Status == SystemStatus.Running)
            {
                _system.Break();
            }
        }

        private void Step()
        {
            if (_system.Status == SystemStatus.Paused)
            {
                DoStep();
            }
        }

        private void StepOver()
        {
            if (_system.Status == SystemStatus.Paused)
            {
                _system.StepOver();
            }
        }

        private void DebugChecked(bool debug)
        {
            _system.Debug = debug;
        }
    }
}
