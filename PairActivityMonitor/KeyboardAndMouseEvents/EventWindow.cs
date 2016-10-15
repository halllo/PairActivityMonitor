using System.Windows.Forms;

namespace KeyboardAndMouseEvents
{
	public partial class EventWindow : Form
	{
		private RawInput _rawInput;

		public EventWindow()
		{
			InitializeComponent();


			_rawInput = new RawInput(Handle);
			_rawInput.KeyEvent += (sender, arg) => Log($"keybo:\t{arg.KeyEvent.DeviceName}: {arg.KeyEvent.VKeyName} ({arg.KeyEvent.PressState})");
			_rawInput.MouseEvent += (sender, arg) => Log($"mouse:\t{arg.MouseEvent.DeviceName}: {arg.MouseEvent.Buttons}");
		}

		private void Log(string text)
		{
			if (IsDisposed) return;
			textBox1.AppendText(text + "\n");
			textBox1.ScrollToCaret();
		}
	}
}
