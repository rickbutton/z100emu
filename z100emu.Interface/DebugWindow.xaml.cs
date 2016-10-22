using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SDL2;
using z100emu.Ram;

namespace z100emu.Interface
{
    public delegate void ResetEventHandler();
    public delegate void ResumeEventHandler();
    public delegate void BreakEventHandler();
    public delegate void StepEventHandler();
    public delegate void StepOverEventHandler();
    public delegate void DebugCheckEventHandler(bool debug);

    public partial class DebugWindow : Window
    {
        public ResetEventHandler Reset { get; set; }
        public ResumeEventHandler Resume { get; set; }
        public BreakEventHandler Break { get; set; }
        public StepEventHandler Step { get; set; }
        public StepOverEventHandler StepOver { get; set; }
        public DebugCheckEventHandler DebugChecked { get; set; }

        public HexRowEnumerable HexRows { get; set; }
        public ZenithSystem ZSystem { get; set; }

        public DebugWindow(ZenithSystem system)
        {
            ZSystem = system;
            ZSystem.BreakpointHit += BreakpointHit;
            DataContext = system;
            InitializeComponent();
            RefreshHexGrid();
        }

        public void DebugLine(string line)
        {
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Reset?.Invoke();
            ClearHexGrid();
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            Resume?.Invoke();
            ClearHexGrid();
        }

        private void BreakButton_Click(object sender, RoutedEventArgs e)
        {
            Break?.Invoke();
            RefreshHexGrid();

            if (SnapToIpCheck.IsChecked ?? false)
            {
                Dispatcher.Invoke(GotoIp);
            }
        }

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            Step?.Invoke();
            RefreshHexGrid();

            if (SnapToIpCheck.IsChecked ?? false)
            {
                GotoIp();
            }
        }

        private void StepOverButton_Click(object sender, RoutedEventArgs e)
        {
            StepOver?.Invoke();
            RefreshHexGrid();

            if (SnapToIpCheck.IsChecked ?? false)
            {
                GotoIp();
            }
        }

        private void BreakpointHit()
        {
            Dispatcher.Invoke(() =>
            {
                RefreshHexGrid();

                if (SnapToIpCheck.IsChecked ?? false)
                {
                    GotoIp();
                }
            });
            
        }

        private void RefreshHexGrid()
        {
            HexRows = new HexRowEnumerable(ZSystem);
            HexGrid.ItemsSource = null;
            HexGrid.ItemsSource = HexRows;
        }

        private void ClearHexGrid()
        {
            HexGrid.ItemsSource = null;
        }

        private void DebugCheck_Checked(object sender, RoutedEventArgs e)
        {
            DebugChecked?.Invoke(DebugCheck.IsChecked.GetValueOrDefault());
        }

        private void GotoIpButton_OnClick(object sender, RoutedEventArgs e)
        {
            GotoIp();
        }

        private void GotoIp()
        {
            if (ZSystem.Paused)
            {
                var address = (ZSystem.CS??0)*0x10 + (ZSystem.IP??0);
                var offset = address/0x10;
                var index = address%0x10;
                var length = ZSystem.GetCurrentInstructionLength();
                HexGrid.SelectedCells.Clear();

                for (var i = 0; i < length; i++)
                {
                    var itemOffset = (index + i)/0x10;
                    var cell = (index + i)%0x10;
                    HexGrid.SelectedCells.Add(new DataGridCellInfo(HexGrid.Items[offset + itemOffset], HexGrid.Columns[cell + 1]));
                }

                HexGrid.ScrollIntoView(HexGrid.Items[offset]);
                HexGrid.Focus();

                NextInstructionLabel.Content = ZSystem.GetCurrentInstructionString();
            }
        }

        
    }
}
