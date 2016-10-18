﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

		private bool _firstTimeSettingsOpen = true;
		private bool _settings_Open = true; public bool Settings_Open { get { return _settings_Open; } set { _settings_Open = value; _firstTimeSettingsOpen = false; OnPropertyChanged(); } }
		bool _settings_P1Keyboard; public bool Settings_P1Keyboard { get { return _settings_P1Keyboard; } set { _settings_P1Keyboard = value; OnPropertyChanged(); } }
		bool _settings_P1Mouse; public bool Settings_P1Mouse { get { return _settings_P1Mouse; } set { _settings_P1Mouse = value; OnPropertyChanged(); } }
		bool _settings_P2Keyboard; public bool Settings_P2Keyboard { get { return _settings_P2Keyboard; } set { _settings_P2Keyboard = value; OnPropertyChanged(); } }
		bool _settings_P2Mouse; public bool Settings_P2Mouse { get { return _settings_P2Mouse; } set { _settings_P2Mouse = value; OnPropertyChanged(); } }
		bool _settings_ShowCounters; public bool Settings_ShowCounters { get { return _settings_ShowCounters; } set { _settings_ShowCounters = value; OnPropertyChanged(); } }

		private RawInput _rawInput;
		public void Start()
		{
			var windowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
			_rawInput = new RawInput(windowHandle);
			_rawInput.KeyEvent += _rawInput_KeyEvent;
			_rawInput.MouseEvent += _rawInput_MouseEvent;
		}
		private void _rawInput_KeyEvent(object sender, KeyInputEventArg e)
		{
			if (Settings_P1Keyboard) { _keyboardDeviceActions[e.KeyEvent.DeviceName] = (a, v) => a.P1Keyboard += v; Settings_P1Keyboard = false; }
			if (Settings_P2Keyboard) { _keyboardDeviceActions[e.KeyEvent.DeviceName] = (a, v) => a.P2Keyboard += v; Settings_P2Keyboard = false; }
			if (_firstTimeSettingsOpen && _keyboardDeviceActions.Count == 2 && 2 == _mouseDeviceActions.Count) Settings_Open = false;

			if (e.KeyEvent.PressState == KeyPressState.Released)
			{
				var activity = Activities[0];
				if (_keyboardDeviceActions.ContainsKey(e.KeyEvent.DeviceName))
					_keyboardDeviceActions[e.KeyEvent.DeviceName](activity, 1m);
				activity.Updated();
				Background = FarbverlaufRotGrünRotGauss(activity.P1Percent);
				EventCounter++;
			}
		}
		private void _rawInput_MouseEvent(object sender, MouseInputEventArg e)
		{
			if (Settings_P1Mouse) { _mouseDeviceActions[e.MouseEvent.DeviceName] = (a, v) => a.P1Mouse += v; Settings_P1Mouse = false; }
			if (Settings_P2Mouse) { _mouseDeviceActions[e.MouseEvent.DeviceName] = (a, v) => a.P2Mouse += v; Settings_P2Mouse = false; }
			if (_firstTimeSettingsOpen && _keyboardDeviceActions.Count == 2 && 2 == _mouseDeviceActions.Count) Settings_Open = false;

			var activity = Activities[0];
			if (_mouseDeviceActions.ContainsKey(e.MouseEvent.DeviceName))
				_mouseDeviceActions[e.MouseEvent.DeviceName](activity,
					  e.MouseEvent.Buttons == MouseButtons.None ? 0.01m
					: e.MouseEvent.Buttons == MouseButtons.MouseWheel ? 0.03m
					: 1m);

			activity.Updated();
			Background = FarbverlaufRotGrünRotGauss(activity.P1Percent);
			EventCounter++;
		}
		private Dictionary<string, Action<PairActivity, decimal>> _mouseDeviceActions = new Dictionary<string, Action<PairActivity, decimal>>();
		private Dictionary<string, Action<PairActivity, decimal>> _keyboardDeviceActions = new Dictionary<string, Action<PairActivity, decimal>>();

		public List<PairActivity> Activities { get; set; } = new List<PairActivity> { new PairActivity() { Name = "" } };

		private int _eventCounter; public int EventCounter { get { return _eventCounter; } set { _eventCounter = value; OnPropertyChanged(); } }

		private Brush _background = Brushes.Red; public Brush Background { get { return _background; } set { _background = value; OnPropertyChanged(); } }
		private Brush FarbverlaufRotGrünRotGauss(decimal prozent)
		{
			const double a = 1.0;//height of curves peak
			const double b = 50.0;//center of peak
			const double c = 10;//standard deviation
			Func<double, double> gaussianFunction = x => a * Math.Exp(-((x - b) * (x - b)) / (2.0 * c * c));

			var fx = gaussianFunction(Convert.ToDouble(prozent));
			return new SolidColorBrush(Color.FromRgb((byte)(255 * (1.0 - fx)), (byte)(255 * fx), 0));
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

		public decimal P1Percent => 100 * (P1Keyboard + P1Mouse) / Math.Max(0.0001m, P1Keyboard + P1Mouse + P2Keyboard + P2Mouse);
		public decimal P2Percent => 100 * (P2Keyboard + P2Mouse) / Math.Max(0.0001m, P1Keyboard + P1Mouse + P2Keyboard + P2Mouse);

		public decimal P1 => P1Keyboard + P1Mouse;
		public decimal P2 => P2Keyboard + P2Mouse;

		decimal _P1Keyboard; public decimal P1Keyboard { get { return _P1Keyboard; } set { _P1Keyboard = value; OnPropertyChanged(); } }
		decimal _P1Mouse; public decimal P1Mouse { get { return _P1Mouse; } set { _P1Mouse = value; OnPropertyChanged(); } }

		decimal _P2Keyboard; public decimal P2Keyboard { get { return _P2Keyboard; } set { _P2Keyboard = value; OnPropertyChanged(); } }
		decimal _P2Mouse; public decimal P2Mouse { get { return _P2Mouse; } set { _P2Mouse = value; OnPropertyChanged(); } }

		public string Name { get; set; }
	}
}
