using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KeyboardAndMouseEvents
{
	public class RawInput : NativeWindow
	{
		public delegate void DeviceKeyEventHandler(object sender, KeyInputEventArg e);
		public event DeviceKeyEventHandler KeyEvent;

		public delegate void DeviceMouseEventHandler(object sender, MouseInputEventArg e);
		public event DeviceMouseEventHandler MouseEvent;

		private readonly Dictionary<IntPtr, InputEvent> _deviceList = new Dictionary<IntPtr, InputEvent>();

		public RawInput(IntPtr parentHandle)
		{
			AssignHandle(parentHandle);
			RememberInputDevices();
			Register(captureOnlyInForeground: false);
		}

		public int NumberOfKeyboards { get; private set; }
		public int NumberOfMouses { get; private set; }

		private void Register(bool captureOnlyInForeground)
		{
			var rid = new RawInputDevice[2];

			rid[0].UsagePage = HidUsagePage.GENERIC;
			rid[0].Usage = HidUsage.Keyboard;
			rid[0].Flags = (captureOnlyInForeground ? RawInputDeviceFlags.NONE : RawInputDeviceFlags.INPUTSINK) | RawInputDeviceFlags.DEVNOTIFY;
			rid[0].Target = Handle;

			rid[1].UsagePage = HidUsagePage.GENERIC;
			rid[1].Usage = HidUsage.Mouse;
			rid[1].Flags = (captureOnlyInForeground ? RawInputDeviceFlags.NONE : RawInputDeviceFlags.INPUTSINK) | RawInputDeviceFlags.DEVNOTIFY;
			rid[1].Target = Handle;

			if (!Win32.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
			{
				throw new ApplicationException("Failed to register raw input device(s).");
			}
		}

		private void RememberInputDevices()
		{
			var keyboardNumber = 0;
			var mouseNumber = 0;
			uint deviceCount = 0;
			var dwSize = (Marshal.SizeOf(typeof(Rawinputdevicelist)));
			if (Win32.GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, (uint)dwSize) == 0)
			{
				var pRawInputDeviceList = Marshal.AllocHGlobal((int)(dwSize * deviceCount));
				Win32.GetRawInputDeviceList(pRawInputDeviceList, ref deviceCount, (uint)dwSize);

				for (var i = 0; i < deviceCount; i++)
				{
					uint pcbSize = 0;

					// On Window 8 64bit when compiling against .Net > 3.5 using .ToInt32 you will generate an arithmetic overflow. Leave as it is for 32bit/64bit applications
					var rid = (Rawinputdevicelist)Marshal.PtrToStructure(new IntPtr((pRawInputDeviceList.ToInt64() + (dwSize * i))), typeof(Rawinputdevicelist));

					Win32.GetRawInputDeviceInfo(rid.hDevice, RawInputDeviceInfo.RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);

					if (pcbSize <= 0) continue;

					var pData = Marshal.AllocHGlobal((int)pcbSize);
					Win32.GetRawInputDeviceInfo(rid.hDevice, RawInputDeviceInfo.RIDI_DEVICENAME, pData, ref pcbSize);
					var deviceName = Marshal.PtrToStringAnsi(pData);

					if (rid.dwType == DeviceType.RimTypekeyboard)
					{
						var deviceDesc = DeviceInformation.GetDeviceDescription(deviceName);
						var dInfo = new KeyEvent
						{
							DeviceName = Marshal.PtrToStringAnsi(pData),
							DeviceHandle = rid.hDevice,
							DeviceType = DeviceInformation.GetDeviceType(rid.dwType),
							Name = deviceDesc,
							Source = $"Keyboard_{keyboardNumber++.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}"
						};

						if (!_deviceList.ContainsKey(rid.hDevice))
						{
							_deviceList.Add(rid.hDevice, dInfo);
						}
					}
					else if (rid.dwType == DeviceType.RimTypemouse)
					{
						var deviceDesc = DeviceInformation.GetDeviceDescription(deviceName);
						var dInfo = new MouseEvent
						{
							DeviceName = Marshal.PtrToStringAnsi(pData),
							DeviceHandle = rid.hDevice,
							DeviceType = DeviceInformation.GetDeviceType(rid.dwType),
							Name = deviceDesc,
							Source = $"Mouse_{mouseNumber++.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}"
						};

						if (!_deviceList.ContainsKey(rid.hDevice))
						{
							_deviceList.Add(rid.hDevice, dInfo);
						}
					}

					Marshal.FreeHGlobal(pData);
				}

				Marshal.FreeHGlobal(pRawInputDeviceList);

				NumberOfKeyboards = keyboardNumber;
				NumberOfMouses = mouseNumber;
				Debug.WriteLine($"We found {NumberOfKeyboards} Keyboard(s) and {NumberOfMouses} Mouse(s)");
				return;
			}
		}

		protected override void WndProc(ref Message message)
		{
			switch (message.Msg)
			{
				case Win32.WM_INPUT:
					{
						ProcessRawInput(message.LParam);
					}
					break;

				case Win32.WM_USB_DEVICECHANGE:
					{
					}
					break;
			}

			base.WndProc(ref message);
		}

		private void ProcessRawInput(IntPtr hdevice)
		{
			var dwSize = 0;
			Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, IntPtr.Zero, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader)));

			InputData rawBuffer;
			var rawInputData = Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, out rawBuffer, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader)));
			if (dwSize != rawInputData)
			{
				Debug.WriteLine("Error getting the rawinput buffer");
				return;
			}

			if (_deviceList.ContainsKey(rawBuffer.header.hDevice))
			{
				var inputEvent = _deviceList[rawBuffer.header.hDevice];
				if (inputEvent.IsKeyboard)
				{
					int virtualKey = rawBuffer.data.keyboard.VKey;
					int makeCode = rawBuffer.data.keyboard.Makecode;
					int flags = rawBuffer.data.keyboard.Flags;

					if (virtualKey == Win32.KEYBOARD_OVERRUN_MAKE_CODE) return;

					var isE0BitSet = ((flags & Win32.RI_KEY_E0) != 0);
					var isBreakBitSet = ((flags & Win32.RI_KEY_BREAK) != 0);

					var keyPressEvent = (KeyEvent)inputEvent;
					keyPressEvent.PressState = isBreakBitSet ? KeyPressState.Released : KeyPressState.Pressed;
					keyPressEvent.Message = rawBuffer.data.keyboard.Message;
					keyPressEvent.VKeyName = KeyMapper.GetKeyName(KeyMapper.VirtualKeyCorrection(rawBuffer, virtualKey, isE0BitSet, makeCode)).ToUpper();
					keyPressEvent.VKey = virtualKey;
					KeyEvent?.Invoke(this, new KeyInputEventArg(keyPressEvent));
				}
				else if (inputEvent.IsMouse)
				{
					var mouseMovedEvent = (MouseEvent)inputEvent;
					mouseMovedEvent.Buttons = (MouseButtons)rawBuffer.data.mouse.usButtonFlags;
					MouseEvent?.Invoke(this, new MouseInputEventArg(mouseMovedEvent));
				}
			}
			else
			{
				Debug.WriteLine("Handle: {0} was not in the device list.", rawBuffer.header.hDevice);
			}
		}
	}

	internal static class KeyMapper
	{
		// I prefer to have control over the key mapping
		// This mapping could be loading from file to allow mapping changes without a recompile
		public static string GetKeyName(int value)
		{
			switch (value)
			{
				case 0x41:
					return "A";
				case 0x6b:
					return "Add";
				case 0x40000:
					return "Alt";
				case 0x5d:
					return "Apps";
				case 0xf6:
					return "Attn";
				case 0x42:
					return "B";
				case 8:
					return "Back";
				case 0xa6:
					return "BrowserBack";
				case 0xab:
					return "BrowserFavorites";
				case 0xa7:
					return "BrowserForward";
				case 0xac:
					return "BrowserHome";
				case 0xa8:
					return "BrowserRefresh";
				case 170:
					return "BrowserSearch";
				case 0xa9:
					return "BrowserStop";
				case 0x43:
					return "C";
				case 3:
					return "Cancel";
				case 20:
					return "Capital";
				//case 20:      return "CapsLock";
				case 12:
					return "Clear";
				case 0x20000:
					return "Control";
				case 0x11:
					return "ControlKey";
				case 0xf7:
					return "Crsel";
				case 0x44:
					return "D";
				case 0x30:
					return "D0";
				case 0x31:
					return "D1";
				case 50:
					return "D2";
				case 0x33:
					return "D3";
				case 0x34:
					return "D4";
				case 0x35:
					return "D5";
				case 0x36:
					return "D6";
				case 0x37:
					return "D7";
				case 0x38:
					return "D8";
				case 0x39:
					return "D9";
				case 110:
					return "Decimal";
				case 0x2e:
					return "Delete";
				case 0x6f:
					return "Divide";
				case 40:
					return "Down";
				case 0x45:
					return "E";
				case 0x23:
					return "End";
				case 13:
					return "Enter";
				case 0xf9:
					return "EraseEof";
				case 0x1b:
					return "Escape";
				case 0x2b:
					return "Execute";
				case 0xf8:
					return "Exsel";
				case 70:
					return "F";
				case 0x70:
					return "F1";
				case 0x79:
					return "F10";
				case 0x7a:
					return "F11";
				case 0x7b:
					return "F12";
				case 0x7c:
					return "F13";
				case 0x7d:
					return "F14";
				case 0x7e:
					return "F15";
				case 0x7f:
					return "F16";
				case 0x80:
					return "F17";
				case 0x81:
					return "F18";
				case 130:
					return "F19";
				case 0x71:
					return "F2";
				case 0x83:
					return "F20";
				case 0x84:
					return "F21";
				case 0x85:
					return "F22";
				case 0x86:
					return "F23";
				case 0x87:
					return "F24";
				case 0x72:
					return "F3";
				case 0x73:
					return "F4";
				case 0x74:
					return "F5";
				case 0x75:
					return "F6";
				case 0x76:
					return "F7";
				case 0x77:
					return "F8";
				case 120:
					return "F9";
				case 0x18:
					return "FinalMode";
				case 0x47:
					return "G";
				case 0x48:
					return "H";
				case 0x15:
					return "HanguelMode";
				//case 0x15:    return "HangulMode";
				case 0x19:
					return "HanjaMode";
				case 0x2f:
					return "Help";
				case 0x24:
					return "Home";
				case 0x49:
					return "I";
				case 30:
					return "IMEAceept";
				case 0x1c:
					return "IMEConvert";
				case 0x1f:
					return "IMEModeChange";
				case 0x1d:
					return "IMENonconvert";
				case 0x2d:
					return "Insert";
				case 0x4a:
					return "J";
				case 0x17:
					return "JunjaMode";
				case 0x4b:
					return "K";
				//case 0x15:    return "KanaMode";
				//case 0x19:    return "KanjiMode";
				case 0xffff:
					return "KeyCode";
				case 0x4c:
					return "L";
				case 0xb6:
					return "LaunchApplication1";
				case 0xb7:
					return "LaunchApplication2";
				case 180:
					return "LaunchMail";
				case 1:
					return "LButton";
				case 0xa2:
					return "LControl";
				case 0x25:
					return "Left";
				case 10:
					return "LineFeed";
				case 0xa4:
					return "LMenu";
				case 160:
					return "LShift";
				case 0x5b:
					return "LWin";
				case 0x4d:
					return "M";
				case 4:
					return "MButton";
				case 0xb0:
					return "MediaNextTrack";
				case 0xb3:
					return "MediaPlayPause";
				case 0xb1:
					return "MediaPreviousTrack";
				case 0xb2:
					return "MediaStop";
				case 0x12:
					return "Menu";
				// case 65536:  return "Modifiers";
				case 0x6a:
					return "Multiply";
				case 0x4e:
					return "N";
				case 0x22:
					return "Next";
				case 0xfc:
					return "NoName";
				case 0:
					return "None";
				case 0x90:
					return "NumLock";
				case 0x60:
					return "NumPad0";
				case 0x61:
					return "NumPad1";
				case 0x62:
					return "NumPad2";
				case 0x63:
					return "NumPad3";
				case 100:
					return "NumPad4";
				case 0x65:
					return "NumPad5";
				case 0x66:
					return "NumPad6";
				case 0x67:
					return "NumPad7";
				case 0x68:
					return "NumPad8";
				case 0x69:
					return "NumPad9";
				case 0x4f:
					return "O";
				case 0xdf:
					return "Oem8";
				case 0xe2:
					return "OemBackslash";
				case 0xfe:
					return "OemClear";
				case 0xdd:
					return "OemCloseBrackets";
				case 0xbc:
					return "OemComma";
				case 0xbd:
					return "OemMinus";
				case 0xdb:
					return "OemOpenBrackets";
				case 190:
					return "OemPeriod";
				case 220:
					return "OemPipe";
				case 0xbb:
					return "Oemplus";
				case 0xbf:
					return "OemQuestion";
				case 0xde:
					return "OemQuotes";
				case 0xba:
					return "OemSemicolon";
				case 0xc0:
					return "Oemtilde";
				case 80:
					return "P";
				case 0xfd:
					return "Pa1";
				// case 0x22:   return "PageDown";
				// case 0x21:   return "PageUp";
				case 0x13:
					return "Pause";
				case 250:
					return "Play";
				case 0x2a:
					return "Print";
				case 0x2c:
					return "PrintScreen";
				case 0x21:
					return "Prior";
				case 0xe5:
					return "ProcessKey";
				case 0x51:
					return "Q";
				case 0x52:
					return "R";
				case 2:
					return "RButton";
				case 0xa3:
					return "RControl";
				//case 13:      return "Return";
				case 0x27:
					return "Right";
				case 0xa5:
					return "RMenu";
				case 0xa1:
					return "RShift";
				case 0x5c:
					return "RWin";
				case 0x53:
					return "S";
				case 0x91:
					return "Scroll";
				case 0x29:
					return "Select";
				case 0xb5:
					return "SelectMedia";
				case 0x6c:
					return "Separator";
				case 0x10000:
					return "Shift";
				case 0x10:
					return "ShiftKey";
				//case 0x2c:    return "Snapshot";
				case 0x20:
					return "Space";
				case 0x6d:
					return "Subtract";
				case 0x54:
					return "T";
				case 9:
					return "Tab";
				case 0x55:
					return "U";
				case 0x26:
					return "Up";
				case 0x56:
					return "V";
				case 0xae:
					return "VolumeDown";
				case 0xad:
					return "VolumeMute";
				case 0xaf:
					return "VolumeUp";
				case 0x57:
					return "W";
				case 0x58:
					return "X";
				case 5:
					return "XButton1";
				case 6:
					return "XButton2";
				case 0x59:
					return "Y";
				case 90:
					return "Z";
				case 0xfb:
					return "Zoom";
			}

			return value.ToString(CultureInfo.InvariantCulture).ToUpper();
		}

		// If you prefer the virtualkey converted into a Microsoft virtualkey code use this
		public static string GetMicrosoftKeyName(int virtualKey)
		{
			return new KeysConverter().ConvertToString(virtualKey);
		}

		public static int VirtualKeyCorrection(InputData rawBuffer, int virtualKey, bool isE0BitSet, int makeCode)
		{
			var correctedVKey = virtualKey;

			if (rawBuffer.header.hDevice == IntPtr.Zero)
			{
				// When hDevice is 0 and the vkey is VK_CONTROL indicates the ZOOM key
				if (rawBuffer.data.keyboard.VKey == Win32.VK_CONTROL)
				{
					correctedVKey = Win32.VK_ZOOM;
				}
			}
			else
			{
				switch (virtualKey)
				{
					// Right-hand CTRL and ALT have their e0 bit set 
					case Win32.VK_CONTROL:
						correctedVKey = isE0BitSet ? Win32.VK_RCONTROL : Win32.VK_LCONTROL;
						break;
					case Win32.VK_MENU:
						correctedVKey = isE0BitSet ? Win32.VK_RMENU : Win32.VK_LMENU;
						break;
					case Win32.VK_SHIFT:
						correctedVKey = makeCode == Win32.SC_SHIFT_R ? Win32.VK_RSHIFT : Win32.VK_LSHIFT;
						break;
					default:
						correctedVKey = virtualKey;
						break;
				}
			}

			return correctedVKey;
		}
	}
	public enum KeyPressState
	{
		Pressed,
		Released
	}
	[Flags]
	public enum MouseButtons : ushort
	{
		/// <summary>No button.</summary>
		None = 0,
		/// <summary>Left (button 1) down.</summary>
		LeftDown = 0x0001,
		/// <summary>Left (button 1) up.</summary>
		LeftUp = 0x0002,
		/// <summary>Right (button 2) down.</summary>
		RightDown = 0x0004,
		/// <summary>Right (button 2) up.</summary>
		RightUp = 0x0008,
		/// <summary>Middle (button 3) down.</summary>
		MiddleDown = 0x0010,
		/// <summary>Middle (button 3) up.</summary>
		MiddleUp = 0x0020,
		/// <summary>Button 4 down.</summary>
		Button4Down = 0x0040,
		/// <summary>Button 4 up.</summary>
		Button4Up = 0x0080,
		/// <summary>Button 5 down.</summary>
		Button5Down = 0x0100,
		/// <summary>Button 5 up.</summary>
		Button5Up = 0x0200,
		/// <summary>Mouse wheel moved.</summary>
		MouseWheel = 0x0400
	}







	public class InputEvent
	{
		public string DeviceName;       // i.e. \\?\HID#VID_045E&PID_00DD&MI_00#8&1eb402&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}
		public string DeviceType;       // KEYBOARD or HID
		public IntPtr DeviceHandle;     // Handle to the device that send the input
		public string Name;             // i.e. Microsoft USB Comfort Curve Keyboard 2000 (Mouse and Keyboard Center)
		public string Source;           // Keyboard_XX

		public virtual bool IsMouse => false;
		public virtual bool IsKeyboard => false;

		public override string ToString()
		{
			return $"Device\n DeviceName: {DeviceName}\n DeviceType: {DeviceType}\n DeviceHandle: {DeviceHandle.ToInt64().ToString("X")}\n Name: {Name}\n";
		}
	}



	public class KeyInputEventArg : EventArgs
	{
		public KeyInputEventArg(KeyEvent arg)
		{
			KeyEvent = arg;
		}

		public KeyEvent KeyEvent { get; private set; }
	}
	public class KeyEvent : InputEvent
	{
		public override bool IsKeyboard => true;

		public int VKey;                // Virtual Key. Corrected for L/R keys(i.e. LSHIFT/RSHIFT) and Zoom
		public string VKeyName;         // Virtual Key Name. Corrected for L/R keys(i.e. LSHIFT/RSHIFT) and Zoom
		public uint Message;            // WM_KEYDOWN or WM_KEYUP
		public KeyPressState PressState;    // MAKE or BREAK
	}



	public class MouseInputEventArg : EventArgs
	{
		public MouseInputEventArg(MouseEvent arg)
		{
			MouseEvent = arg;
		}

		public MouseEvent MouseEvent { get; private set; }
	}
	public class MouseEvent : InputEvent
	{
		public override bool IsMouse => true;

		public MouseButtons Buttons;
	}
















































	internal static class DeviceInformation
	{
		internal static string GetDeviceType(uint device)
		{
			string deviceType;
			switch (device)
			{
				case DeviceType.RimTypemouse: deviceType = "MOUSE"; break;
				case DeviceType.RimTypekeyboard: deviceType = "KEYBOARD"; break;
				case DeviceType.RimTypeHid: deviceType = "HID"; break;
				default: deviceType = "UNKNOWN"; break;
			}

			return deviceType;
		}

		internal static string GetDeviceDescription(string device)
		{
			string deviceDesc;
			try
			{
				var deviceKey = GetDeviceKey(device);
				deviceDesc = deviceKey.GetValue("DeviceDesc").ToString();
				deviceDesc = deviceDesc.Substring(deviceDesc.IndexOf(';') + 1);
			}
			catch (Exception)
			{
				deviceDesc = "Device is malformed unable to look up in the registry";
			}

			//var deviceClass = RegistryAccess.GetClassType(deviceKey.GetValue("ClassGUID").ToString());
			//isKeyboard = deviceClass.ToUpper().Equals( "KEYBOARD" );

			return deviceDesc;
		}

		private static RegistryKey GetDeviceKey(string device)
		{
			var split = device.Substring(4).Split('#');

			var classCode = split[0];       // ACPI (Class code)
			var subClassCode = split[1];    // PNP0303 (SubClass code)
			var protocolCode = split[2];    // 3&13c0b0c5&0 (Protocol code)

			return Registry.LocalMachine.OpenSubKey(string.Format(@"System\CurrentControlSet\Enum\{0}\{1}\{2}", classCode, subClassCode, protocolCode));
		}

		private static string GetClassType(string classGuid)
		{
			var classGuidKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\" + classGuid);

			return classGuidKey != null ? (string)classGuidKey.GetValue("Class") : string.Empty;
		}
	}















































	internal static class Win32
	{
		public static int LoWord(int dwValue)
		{
			return (dwValue & 0xFFFF);
		}

		public static int HiWord(Int64 dwValue)
		{
			return (int)(dwValue >> 16) & ~FAPPCOMMANDMASK;
		}

		public static ushort LowWord(uint val)
		{
			return (ushort)val;
		}

		public static ushort HighWord(uint val)
		{
			return (ushort)(val >> 16);
		}

		public static uint BuildWParam(ushort low, ushort high)
		{
			return ((uint)high << 16) | low;
		}

		// ReSharper disable InconsistentNaming
		public const int KEYBOARD_OVERRUN_MAKE_CODE = 0xFF;
		public const int WM_APPCOMMAND = 0x0319;
		private const int FAPPCOMMANDMASK = 0xF000;
		internal const int FAPPCOMMANDMOUSE = 0x8000;
		internal const int FAPPCOMMANDOEM = 0x1000;

		public const int WM_KEYDOWN = 0x0100;
		public const int WM_KEYUP = 0x0101;
		internal const int WM_SYSKEYDOWN = 0x0104;
		internal const int WM_INPUT = 0x00FF;
		internal const int WM_USB_DEVICECHANGE = 0x0219;

		internal const int VK_SHIFT = 0x10;

		internal const int RI_KEY_MAKE = 0x00;      // Key Down
		internal const int RI_KEY_BREAK = 0x01;     // Key Up
		internal const int RI_KEY_E0 = 0x02;        // Left version of the key
		internal const int RI_KEY_E1 = 0x04;        // Right version of the key. Only seems to be set for the Pause/Break key.

		internal const int VK_CONTROL = 0x11;
		internal const int VK_MENU = 0x12;
		internal const int VK_ZOOM = 0xFB;
		internal const int VK_LSHIFT = 0xA0;
		internal const int VK_RSHIFT = 0xA1;
		internal const int VK_LCONTROL = 0xA2;
		internal const int VK_RCONTROL = 0xA3;
		internal const int VK_LMENU = 0xA4;
		internal const int VK_RMENU = 0xA5;

		internal const int SC_SHIFT_R = 0x36;
		internal const int SC_SHIFT_L = 0x2a;
		internal const int RIM_INPUT = 0x00;
		// ReSharper restore InconsistentNaming

		[DllImport("User32.dll", SetLastError = true)]
		internal static extern int GetRawInputData(IntPtr hRawInput, DataCommand command, [Out] out InputData buffer, [In, Out] ref int size, int cbSizeHeader);

		[DllImport("User32.dll", SetLastError = true)]
		internal static extern int GetRawInputData(IntPtr hRawInput, DataCommand command, [Out] IntPtr pData, [In, Out] ref int size, int sizeHeader);

		[DllImport("User32.dll", SetLastError = true)]
		internal static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfo command, IntPtr pData, ref uint size);

		[DllImport("User32.dll", SetLastError = true)]
		internal static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint numberDevices, uint size);

		[DllImport("User32.dll", SetLastError = true)]
		internal static extern bool RegisterRawInputDevices(RawInputDevice[] pRawInputDevice, uint numberDevices, uint size);
	}

	internal enum DataCommand : uint
	{
		RID_HEADER = 0x10000005, // Get the header information from the RAWINPUT structure.
		RID_INPUT = 0x10000003   // Get the raw data from the RAWINPUT structure.
	}

	internal static class DeviceType
	{
		public const int RimTypemouse = 0;
		public const int RimTypekeyboard = 1;
		public const int RimTypeHid = 2;
	}

	internal enum RawInputDeviceInfo : uint
	{
		RIDI_DEVICENAME = 0x20000007,
		RIDI_DEVICEINFO = 0x2000000b,
		PREPARSEDDATA = 0x20000005
	}

	internal enum BroadcastDeviceType
	{
		DBT_DEVTYP_OEM = 0,
		DBT_DEVTYP_DEVNODE = 1,
		DBT_DEVTYP_VOLUME = 2,
		DBT_DEVTYP_PORT = 3,
		DBT_DEVTYP_NET = 4,
		DBT_DEVTYP_DEVICEINTERFACE = 5,
		DBT_DEVTYP_HANDLE = 6,
	}

	internal enum DeviceNotification
	{
		DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000,           // The hRecipient parameter is a window handle
		DEVICE_NOTIFY_SERVICE_HANDLE = 0x00000001,          // The hRecipient parameter is a service status handle
		DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x00000004    // Notifies the recipient of device interface events for all device interface classes. (The dbcc_classguid member is ignored.)
															// This value can be used only if the dbch_devicetype member is DBT_DEVTYP_DEVICEINTERFACE.
	}

	[Flags]
	internal enum RawInputDeviceFlags
	{
		NONE = 0,                   // No flags
		REMOVE = 0x00000001,        // Removes the top level collection from the inclusion list. This tells the operating system to stop reading from a device which matches the top level collection. 
		EXCLUDE = 0x00000010,       // Specifies the top level collections to exclude when reading a complete usage page. This flag only affects a TLC whose usage page is already specified with PageOnly.
		PAGEONLY = 0x00000020,      // Specifies all devices whose top level collection is from the specified UsagePage. Note that Usage must be zero. To exclude a particular top level collection, use Exclude.
		NOLEGACY = 0x00000030,      // Prevents any devices specified by UsagePage or Usage from generating legacy messages. This is only for the mouse and keyboard.
		INPUTSINK = 0x00000100,     // Enables the caller to receive the input even when the caller is not in the foreground. Note that WindowHandle must be specified.
		CAPTUREMOUSE = 0x00000200,  // Mouse button click does not activate the other window.
		NOHOTKEYS = 0x00000200,     // Application-defined keyboard device hotkeys are not handled. However, the system hotkeys; for example, ALT+TAB and CTRL+ALT+DEL, are still handled. By default, all keyboard hotkeys are handled. NoHotKeys can be specified even if NoLegacy is not specified and WindowHandle is NULL.
		APPKEYS = 0x00000400,       // Application keys are handled.  NoLegacy must be specified.  Keyboard only.

		// Enables the caller to receive input in the background only if the foreground application does not process it. 
		// In other words, if the foreground application is not registered for raw input, then the background application that is registered will receive the input.
		EXINPUTSINK = 0x00001000,
		DEVNOTIFY = 0x00002000
	}

	internal enum HidUsagePage : ushort
	{
		UNDEFINED = 0x00,   // Unknown usage page
		GENERIC = 0x01,     // Generic desktop controls
		SIMULATION = 0x02,  // Simulation controls
		VR = 0x03,          // Virtual reality controls
		SPORT = 0x04,       // Sports controls
		GAME = 0x05,        // Games controls
		KEYBOARD = 0x07,    // Keyboard controls
	}

	internal enum HidUsage : ushort
	{
		Undefined = 0x00,       // Unknown usage
		Pointer = 0x01,         // Pointer
		Mouse = 0x02,           // Mouse
		Joystick = 0x04,        // Joystick
		Gamepad = 0x05,         // Game Pad
		Keyboard = 0x06,        // Keyboard
		Keypad = 0x07,          // Keypad
		SystemControl = 0x80,   // Muilt-axis Controller
		Tablet = 0x80,          // Tablet PC controls
		Consumer = 0x0C,        // Consumer
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Rawinputdevicelist
	{
		public IntPtr hDevice;
		public uint dwType;
	}

	[StructLayout(LayoutKind.Explicit)]
	internal struct RawData
	{
		[FieldOffset(0)]
		internal Rawmouse mouse;
		[FieldOffset(0)]
		internal Rawkeyboard keyboard;
		[FieldOffset(0)]
		internal Rawhid hid;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct InputData
	{
		public Rawinputheader header;           // 64 bit header size: 24  32 bit the header size: 16
		public RawData data;                    // Creating the rest in a struct allows the header size to align correctly for 32/64 bit
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Rawinputheader
	{
		public uint dwType;                     // Type of raw input (RIM_TYPEHID 2, RIM_TYPEKEYBOARD 1, RIM_TYPEMOUSE 0)
		public uint dwSize;                     // Size in bytes of the entire input packet of data. This includes RAWINPUT plus possible extra input reports in the RAWHID variable length array. 
		public IntPtr hDevice;                  // A handle to the device generating the raw input data. 
		public IntPtr wParam;                   // RIM_INPUT 0 if input occurred while application was in the foreground else RIM_INPUTSINK 1 if it was not.

		public override string ToString()
		{
			return string.Format("RawInputHeader\n dwType : {0}\n dwSize : {1}\n hDevice : {2}\n wParam : {3}", dwType, dwSize, hDevice, wParam);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Rawhid
	{
		public uint dwSizHid;
		public uint dwCount;
		public byte bRawData;

		public override string ToString()
		{
			return string.Format("Rawhib\n dwSizeHid : {0}\n dwCount : {1}\n bRawData : {2}\n", dwSizHid, dwCount, bRawData);
		}
	}

	[StructLayout(LayoutKind.Explicit)]
	internal struct Rawmouse
	{
		[FieldOffset(0)]
		public ushort usFlags;
		[FieldOffset(4)]
		public uint ulButtons;
		[FieldOffset(4)]
		public ushort usButtonFlags;
		[FieldOffset(6)]
		public ushort usButtonData;
		[FieldOffset(8)]
		public uint ulRawButtons;
		[FieldOffset(12)]
		public int lLastX;
		[FieldOffset(16)]
		public int lLastY;
		[FieldOffset(20)]
		public uint ulExtraInformation;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Rawkeyboard
	{
		public ushort Makecode;                 // Scan code from the key depression
		public ushort Flags;                    // One or more of RI_KEY_MAKE, RI_KEY_BREAK, RI_KEY_E0, RI_KEY_E1
		private readonly ushort Reserved;       // Always 0    
		public ushort VKey;                     // Virtual Key Code
		public uint Message;                    // Corresponding Windows message for exmaple (WM_KEYDOWN, WM_SYASKEYDOWN etc)
		public uint ExtraInformation;           // The device-specific addition information for the event (seems to always be zero for keyboards)

		public override string ToString()
		{
			return string.Format("Rawkeyboard\n Makecode: {0}\n Makecode(hex) : {0:X}\n Flags: {1}\n Reserved: {2}\n VKeyName: {3}\n Message: {4}\n ExtraInformation {5}\n",
												Makecode, Flags, Reserved, VKey, Message, ExtraInformation);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct RawInputDevice
	{
		internal HidUsagePage UsagePage;
		internal HidUsage Usage;
		internal RawInputDeviceFlags Flags;
		internal IntPtr Target;

		public override string ToString()
		{
			return string.Format("{0}/{1}, flags: {2}, target: {3}", UsagePage, Usage, Flags, Target);
		}
	}
}
