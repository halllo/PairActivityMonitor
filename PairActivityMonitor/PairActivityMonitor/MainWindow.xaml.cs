using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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

		private decimal _percentage;

		public decimal Percentage
		{
			get { return _percentage; }
			set
			{
				_percentage = value; OnPropertyChanged();
				Background = FarbverlaufRotGrünRot(_percentage);
			}
		}


		private Brush _background = Brushes.White;
		public Brush Background
		{
			get { return _background; }
			set { _background = value; OnPropertyChanged(); }
		}
		private Brush FarbverlaufRotGrünRot(decimal prozent)
		{
			var einProzent = 255 / 50.0m;

			if (prozent <= 50.0m)
			{
				return new SolidColorBrush(Color.FromRgb((byte)(255 - prozent * einProzent), (byte)(0 + prozent * einProzent), 0));
			}
			else
			{
				return new SolidColorBrush(Color.FromRgb((byte)(0 + (prozent - 50) * einProzent), (byte)(255 - (prozent - 50) * einProzent), 0));
			}
		}
	}
}
