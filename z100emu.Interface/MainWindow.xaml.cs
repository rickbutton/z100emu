using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace z100emu.Interface
{
    public delegate void KeyEventHandler(byte key);

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private static int WIDTH = 80 * 8;
        private static int HEIGHT = 25 * 9;

        private WriteableBitmap _bitmap;

        public KeyEventHandler KeyEvent { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            _bitmap = new WriteableBitmap(WIDTH, HEIGHT, 1, 1, PixelFormats.Rgb24, null);
            Image.Source = _bitmap;
        }

        public void Draw(byte[] buffer)
        {
            _bitmap.WritePixels(new Int32Rect(0, 0, WIDTH, HEIGHT), buffer, WIDTH * 3, 0);
        }

        private void MainWindow_OnTextInput(object sender, TextCompositionEventArgs e)
        {
            KeyEvent?.Invoke(Encoding.ASCII.GetBytes(e.Text)[0]);
        }
    }
}
