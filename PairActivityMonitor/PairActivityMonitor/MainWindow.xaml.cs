using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using KeyboardAndMouseEvents;

namespace PairActivityMonitor
{
	public partial class MainWindow
	{
		private readonly MainWindowModel _mainWindowModel;
		public MainWindow()
		{
			InitializeComponent();
			DataContext = _mainWindowModel = new MainWindowModel();

			Loaded += (s, a) =>
			{
				_mainWindowModel.Start();
			};
		}
	}








	public class MainWindowModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}


		private RawInput _rawInput;
		public void Start()
		{
			var windowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
			_rawInput = new RawInput(windowHandle);
			_rawInput.KeyEvent += (sender, arg) => EventCounter++;
			_rawInput.MouseEvent += (sender, arg) => EventCounter++;
		}


		private int _eventCounter;
		public int EventCounter
		{
			get { return _eventCounter; }
			set { _eventCounter = value; OnPropertyChanged(); }
		}
	}
}
