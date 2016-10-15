using System.Windows;
using System.Windows.Interop;
using KeyboardAndMouseEvents;

namespace PairActivityMonitor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			Loaded += (s, a) =>
			{
				var windowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
				_rawInput = new RawInput(windowHandle);
				_rawInput.KeyEvent += (sender, arg) => Log($"{_counter++}");
				_rawInput.MouseEvent += (sender, arg) => Log($"{_counter++}");
			};
		}

		private RawInput _rawInput;
		private decimal _counter = 0;

		private void Log(string text)
		{
			textBlock.Text = text;
		}
	}
}
