using System;
using System.Collections.Generic;
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

			var random = new Random();
			var activity = Activities[0];
			activity.P1Keyboard += random.Next(0, 3000);
			activity.P1Mouse += random.Next(0, 3000);
			activity.P2Keyboard += random.Next(0, 3000);
			activity.P2Mouse += random.Next(0, 3000);
			activity.Updated();
			Background = FarbverlaufRotGrünRot(activity.P1Percent);
		}

		public List<PairActivity> Activities { get; set; } = new List<PairActivity> { new PairActivity() { Name = "" } };

		private int _eventCounter;
		public int EventCounter
		{
			get { return _eventCounter; }
			set { _eventCounter = value; OnPropertyChanged(); }
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









	public class PairActivity : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		
		public void Updated()
		{
			OnPropertyChanged(nameof(P1));
			OnPropertyChanged(nameof(P2));
			OnPropertyChanged(nameof(P1Percent));
			OnPropertyChanged(nameof(P2Percent));
		}

		public decimal P1Percent => 100 * (P1Keyboard + P1Mouse) / Math.Max(1, P1Keyboard + P1Mouse + P2Keyboard + P2Mouse);
		public decimal P2Percent => 100 * (P2Keyboard + P2Mouse) / Math.Max(1, P1Keyboard + P1Mouse + P2Keyboard + P2Mouse);
		
		public decimal P1 => P1Keyboard + P1Mouse;
		public decimal P2 => P2Keyboard + P2Mouse;

		decimal _P1Keyboard;
		public decimal P1Keyboard { get { return _P1Keyboard; } set { _P1Keyboard = value; OnPropertyChanged(); } }
		decimal _P1Mouse;
		public decimal P1Mouse { get { return _P1Mouse; } set { _P1Mouse = value; OnPropertyChanged(); } }

		decimal _P2Keyboard;
		public decimal P2Keyboard { get { return _P2Keyboard; } set { _P2Keyboard = value; OnPropertyChanged(); } }
		decimal _P2Mouse;
		public decimal P2Mouse { get { return _P2Mouse; } set { _P2Mouse = value; OnPropertyChanged(); } }

		public string Name { get; set; }
	}
}
