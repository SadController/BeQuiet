using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using BeQuiet.Audio;
using BeQuiet.Hotkey;
using BeQuiet.Models;
using NAudio.CoreAudioApi;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: TargetFramework(".NETCoreApp,Version=v9.0", FrameworkDisplayName = ".NET 9.0")]
[assembly: AssemblyCompany("BeQuiet")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyInformationalVersion("1.2.0")]
[assembly: AssemblyProduct("BeQuiet")]
[assembly: AssemblyTitle("BeQuiet")]
[assembly: TargetPlatform("Windows7.0")]
[assembly: SupportedOSPlatform("Windows7.0")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace BeQuiet
{
	public class App : System.Windows.Application
	{
		private Mutex? _mutex;

		private bool _contentLoaded;

		protected override void OnStartup(StartupEventArgs e)
		{
			_mutex = new Mutex(initiallyOwned: true, "BeQuiet_SingleInstance_7F3A", out var createdNew);
			if (!createdNew)
			{
				System.Windows.MessageBox.Show("BeQuiet is already running.", "BeQuiet", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				Shutdown();
			}
			else
			{
				base.OnStartup(e);
			}
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_mutex?.ReleaseMutex();
			_mutex?.Dispose();
			base.OnExit(e);
		}

		[DebuggerNonUserCode]
		[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
		public void InitializeComponent()
		{
			if (!_contentLoaded)
			{
				_contentLoaded = true;
				base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
				Uri resourceLocator = new Uri("/BeQuiet;component/app.xaml", UriKind.Relative);
				System.Windows.Application.LoadComponent(this, resourceLocator);
			}
		}

		[STAThread]
		[DebuggerNonUserCode]
		[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
		public static void Main()
		{
			App app = new App();
			app.InitializeComponent();
			app.Run();
		}
	}
	public class AudioSessionDisplay : INotifyPropertyChanged
	{
		private bool _isPriority;

		public string ProcessName { get; set; } = "";

		public int ProcessId { get; set; }

		public float Volume { get; set; }

		public bool IsPriority
		{
			get
			{
				return _isPriority;
			}
			set
			{
				_isPriority = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPriority"));
			}
		}

		public string VolumeDisplay => ProcessId == 0 ? "no audio" : $"{Volume:F0}%";

		public event PropertyChangedEventHandler? PropertyChanged;
	}
	public partial class MainWindow : Window, IComponentConnector, IStyleConnector
	{
		private readonly AudioManager _audioManager;

		private readonly GlobalHotkey _globalHotkey;

		private readonly AppSettings _settings;

		private readonly ObservableCollection<AudioSessionDisplay> _sessions = new ObservableCollection<AudioSessionDisplay>();

		private readonly DispatcherTimer _duckTimer;

		private readonly NotifyIcon _trayIcon;

		private ToolStripMenuItem? _toggleMenuItem;

		private bool _isCapturingHotkey;

		private bool _isExiting;

		private bool _initialized;

		internal System.Windows.Controls.ListBox AppList = null!;

		internal TextBlock DuckValueText = null!;

		internal Slider DuckSlider = null!;

		internal System.Windows.Controls.TextBox HotkeyDisplay = null!;

		internal System.Windows.Controls.Button ActivateButton = null!;

		internal TextBlock StatusText = null!;

		private bool _contentLoaded;

		public MainWindow()
		{
			InitializeComponent();
			_audioManager = new AudioManager();
			_globalHotkey = new GlobalHotkey();
			_settings = AppSettings.Load();
			AppList.ItemsSource = _sessions;
			_duckTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(3L)
			};
			_duckTimer.Tick += delegate
			{
				_audioManager.DuckNewSessions();
			};
			_trayIcon = CreateTrayIcon();
			_trayIcon.Visible = true;
			ApplySettings();
			RefreshSessions();
			_initialized = true;
			Loaded += (_, _) => UpdateFavoritesLabels();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			nint handle = new WindowInteropHelper(this).Handle;
			_globalHotkey.HotkeyPressed += delegate
			{
				base.Dispatcher.Invoke(ToggleDucking);
			};
			if (_settings.Hotkey.Key != 0 && !_globalHotkey.Register(handle, _settings.Hotkey.Modifiers, _settings.Hotkey.Key))
			{
				HotkeyDisplay.Text = "-- in use --";
			}
		}

		private NotifyIcon CreateTrayIcon()
		{
			return CreateFavoritesTrayIcon();
		}

		private void ApplySettings()
		{
			_audioManager.SetPriorityApps(_settings.PriorityProcessNames);
			_audioManager.SetDuckLevel(_settings.DuckLevel);
			DuckSlider.Value = _settings.DuckLevel;
			DuckValueText.Text = $"{_settings.DuckLevel}%";
			if (_settings.Hotkey.Key != 0)
			{
				HotkeyDisplay.Text = FormatHotkey(_settings.Hotkey.Modifiers, KeyInterop.KeyFromVirtualKey((int)_settings.Hotkey.Key));
			}
			else
			{
				HotkeyDisplay.Text = "none";
			}
		}

		private void RefreshSessions()
		{
			List<AudioSessionInfo> audioSessions = _audioManager.GetAudioSessions();
			var liveNames = audioSessions.Select(s => s.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (string favorite in _settings.FavoriteProcessNames!)
			{
				if (!liveNames.Contains(favorite))
					audioSessions.Add(new AudioSessionInfo { ProcessName = favorite, ProcessId = 0 });
			}
			_sessions.Clear();
			foreach (AudioSessionInfo item in audioSessions.OrderBy(s => s.ProcessName, StringComparer.OrdinalIgnoreCase))
			{
				_sessions.Add(new AudioSessionDisplay
				{
					ProcessName = item.ProcessName,
					ProcessId = item.ProcessId,
					Volume = item.Volume,
					IsPriority = _settings.FavoriteProcessNames!.Contains(item.ProcessName, StringComparer.OrdinalIgnoreCase)
				});
			}
		}

		private void Refresh_Click(object sender, RoutedEventArgs e)
		{
			RefreshSessions();
		}

		private void ResetAll_Click(object sender, RoutedEventArgs e)
		{
			_audioManager.ResetAllVolumes();
			UpdateUI();
		}

		private void Priority_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized && sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is AudioSessionDisplay display)
				SetFavorite(display.ProcessName, checkBox.IsChecked == true);
		}

		private void DuckSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (_initialized)
			{
				int num = (int)DuckSlider.Value;
				_settings.DuckLevel = num;
				_audioManager.SetDuckLevel(num);
				DuckValueText.Text = $"{num}%";
				_settings.Save();
				if (_audioManager.IsDucking)
				{
					RefreshSessions();
				}
			}
		}

		private void DuckPreset_Click(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.Button { Tag: string tag } && int.TryParse(tag, out var result))
			{
				DuckSlider.Value = result;
				DuckValueText.Text = $"{result}%";
			}
		}

		private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				DragMove();
			}
		}

		private void Minimize_Click(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private void Activate_Click(object sender, RoutedEventArgs e)
		{
			ToggleDucking();
		}

		private void ToggleDucking()
		{
			if (!_audioManager.IsDucking && _settings.PriorityProcessNames.Count == 0)
			{
				StatusText.Text = "Choose focus apps in the tray menu";
				return;
			}
			try
			{
				_audioManager.ToggleDucking();
			}
			catch
			{
			}
			UpdateUI();
		}

		private void UpdateUI()
		{
			if (_audioManager.IsDucking)
			{
				ActivateButton.Content = "[ DEACTIVATE ]";
				StatusText.Text = $"status: active — ducked to {_settings.DuckLevel}%";
				StatusText.Foreground = (System.Windows.Media.Brush)FindResource("GreenBrush");
				_trayIcon.Text = $"BeQuiet — active ({_settings.DuckLevel}%)";
				if (_toggleMenuItem != null)
				{
					_toggleMenuItem.Checked = true;
					_toggleMenuItem.Text = $"●  Active ({_settings.DuckLevel}%) — click to stop";
				}
				_duckTimer.Start();
			}
			else
			{
				ActivateButton.Content = "[ ACTIVATE ]";
				StatusText.Text = "status: inactive";
				StatusText.Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush");
				_trayIcon.Text = "BeQuiet — inactive";
				if (_toggleMenuItem != null)
				{
					_toggleMenuItem.Checked = false;
					_toggleMenuItem.Text = "○  Inactive — click to start";
				}
				_duckTimer.Stop();
			}
			RefreshSessions();
			UpdateFocusStatus();
		}

		private void SetHotkey_Click(object sender, RoutedEventArgs e)
		{
			_isCapturingHotkey = true;
			HotkeyDisplay.Text = "press combo...";
			HotkeyDisplay.Focus();
		}

		private void ClearHotkey_Click(object sender, RoutedEventArgs e)
		{
			_globalHotkey.Unregister();
			_settings.Hotkey = new HotkeyConfig
			{
				Modifiers = 0u,
				Key = 0u
			};
			_settings.Save();
			HotkeyDisplay.Text = "none";
		}

		protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
		{
			if (!_isCapturingHotkey)
			{
				base.OnKeyDown(e);
				return;
			}
			Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
			if (((uint)(key - 70) > 1u && (uint)(key - 116) > 5u) || 1 == 0)
			{
				uint num = 0u;
				ModifierKeys modifiers = Keyboard.Modifiers;
				if (modifiers.HasFlag(ModifierKeys.Control))
				{
					num |= 2;
				}
				if (modifiers.HasFlag(ModifierKeys.Alt))
				{
					num |= 1;
				}
				if (modifiers.HasFlag(ModifierKeys.Shift))
				{
					num |= 4;
				}
				if (modifiers.HasFlag(ModifierKeys.Windows))
				{
					num |= 8;
				}
				uint key2 = (uint)KeyInterop.VirtualKeyFromKey(key);
				nint handle = new WindowInteropHelper(this).Handle;
				if (_globalHotkey.Register(handle, num, key2))
				{
					_settings.Hotkey = new HotkeyConfig
					{
						Modifiers = num,
						Key = key2
					};
					_settings.Save();
					HotkeyDisplay.Text = FormatHotkey(num, key);
				}
				else
				{
					HotkeyDisplay.Text = "-- in use --";
				}
				_isCapturingHotkey = false;
				e.Handled = true;
			}
		}

		private static string FormatHotkey(uint modifiers, Key key)
		{
			List<string> list = new List<string>();
			if ((modifiers & 2) != 0)
			{
				list.Add("Ctrl");
			}
			if ((modifiers & 1) != 0)
			{
				list.Add("Alt");
			}
			if ((modifiers & 4) != 0)
			{
				list.Add("Shift");
			}
			if ((modifiers & 8) != 0)
			{
				list.Add("Win");
			}
			list.Add(key.ToString());
			return string.Join(" + ", list);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!_isExiting)
			{
				e.Cancel = true;
				Hide();
			}
			else
			{
				base.OnClosing(e);
			}
		}

		public void ShowFromTray()
		{
			Show();
			base.WindowState = WindowState.Normal;
			Activate();
		}

		public void ExitApp()
		{
			_isExiting = true;
			_duckTimer.Stop();
			_audioManager.Dispose();
			_globalHotkey.Dispose();
			_trayIcon.Visible = false;
			_trayIcon.Dispose();
			System.Windows.Application.Current.Shutdown();
		}

		[DebuggerNonUserCode]
		[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
		public void InitializeComponent()
		{
			if (!_contentLoaded)
			{
				_contentLoaded = true;
				Uri resourceLocator = new Uri("/BeQuiet;component/mainwindow.xaml", UriKind.Relative);
				System.Windows.Application.LoadComponent(this, resourceLocator);
			}
		}

		[DebuggerNonUserCode]
		[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		void IComponentConnector.Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
			case 1:
				((Grid)target).MouseLeftButtonDown += TitleBar_MouseDown;
				break;
			case 2:
				((System.Windows.Controls.Button)target).Click += Minimize_Click;
				break;
			case 3:
				((System.Windows.Controls.Button)target).Click += Close_Click;
				break;
			case 4:
				AppList = (System.Windows.Controls.ListBox)target;
				break;
			case 6:
				((System.Windows.Controls.Button)target).Click += Refresh_Click;
				break;
			case 7:
				((System.Windows.Controls.Button)target).Click += ResetAll_Click;
				break;
			case 8:
				DuckValueText = (TextBlock)target;
				break;
			case 9:
				((System.Windows.Controls.Button)target).Click += DuckPreset_Click;
				break;
			case 10:
				((System.Windows.Controls.Button)target).Click += DuckPreset_Click;
				break;
			case 11:
				((System.Windows.Controls.Button)target).Click += DuckPreset_Click;
				break;
			case 12:
				((System.Windows.Controls.Button)target).Click += DuckPreset_Click;
				break;
			case 13:
				DuckSlider = (Slider)target;
				DuckSlider.ValueChanged += DuckSlider_Changed;
				break;
			case 14:
				HotkeyDisplay = (System.Windows.Controls.TextBox)target;
				break;
			case 15:
				((System.Windows.Controls.Button)target).Click += SetHotkey_Click;
				break;
			case 16:
				((System.Windows.Controls.Button)target).Click += ClearHotkey_Click;
				break;
			case 17:
				ActivateButton = (System.Windows.Controls.Button)target;
				ActivateButton.Click += Activate_Click;
				break;
			case 18:
				StatusText = (TextBlock)target;
				break;
			default:
				_contentLoaded = true;
				break;
			}
		}

		[DebuggerNonUserCode]
		[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		void IStyleConnector.Connect(int connectionId, object target)
		{
			if (connectionId == 5)
			{
				((System.Windows.Controls.CheckBox)target).Checked += Priority_Changed;
				((System.Windows.Controls.CheckBox)target).Unchecked += Priority_Changed;
			}
		}
	}
}
namespace BeQuiet.Models
{
	public class HotkeyConfig
	{
		public uint Modifiers { get; set; } = 2u;

		public uint Key { get; set; } = 81u;
	}
	public class AppSettings
	{
		private static readonly string SettingsDir = Environment.GetEnvironmentVariable("BEQUIET_SETTINGS_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BeQuiet");

		private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

		public List<string> PriorityProcessNames { get; set; } = new List<string>();

		public List<string>? FavoriteProcessNames { get; set; }

		public int DuckLevel { get; set; } = 50;

		public HotkeyConfig Hotkey { get; set; } = new HotkeyConfig();

		public bool StartMinimized { get; set; }

		public void Save()
		{
			Directory.CreateDirectory(SettingsDir);
			string contents = JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(SettingsPath, contents);
		}

		public static AppSettings Load()
		{
			AppSettings settings;
			try
			{
				settings = File.Exists(SettingsPath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings() : new AppSettings();
			}
			catch
			{
				settings = new AppSettings();
			}
			settings.PriorityProcessNames ??= new List<string>();
			settings.FavoriteProcessNames ??= settings.PriorityProcessNames.ToList();
			settings.FavoriteProcessNames = settings.FavoriteProcessNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			settings.PriorityProcessNames = settings.PriorityProcessNames.Where(n => settings.FavoriteProcessNames.Contains(n, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			settings.Hotkey ??= new HotkeyConfig();
			settings.DuckLevel = Math.Clamp(settings.DuckLevel, 5, 90);
			return settings;
		}
	}
}
namespace BeQuiet.Hotkey
{
	public class GlobalHotkey : IDisposable
	{
		private const int HOTKEY_ID = 9000;

		private nint _windowHandle;

		private HwndSource? _source;

		public uint Modifiers { get; private set; }

		public uint Key { get; private set; }

		public bool IsRegistered { get; private set; }

		public event Action? HotkeyPressed;

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(nint hWnd, int id);

		public bool Register(nint windowHandle, uint modifiers, uint key)
		{
			Unregister();
			_windowHandle = windowHandle;
			Modifiers = modifiers;
			Key = key;
			_source = HwndSource.FromHwnd(windowHandle);
			_source?.AddHook(WndProc);
			IsRegistered = RegisterHotKey(_windowHandle, 9000, modifiers, key);
			return IsRegistered;
		}

		public void Unregister()
		{
			if (_windowHandle != IntPtr.Zero)
			{
				UnregisterHotKey(_windowHandle, 9000);
				_source?.RemoveHook(WndProc);
				_windowHandle = IntPtr.Zero;
				IsRegistered = false;
			}
		}

		private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
		{
			if (msg == 786 && ((IntPtr)wParam).ToInt32() == 9000)
			{
				this.HotkeyPressed?.Invoke();
				handled = true;
			}
			return IntPtr.Zero;
		}

		public void Dispose()
		{
			Unregister();
		}
	}
	[Flags]
	public enum HotkeyModifiers : uint
	{
		None = 0u,
		Alt = 1u,
		Ctrl = 2u,
		Shift = 4u,
		Win = 8u
	}
}
namespace BeQuiet.Audio
{
	public class AudioSessionInfo
	{
		public string ProcessName { get; set; } = "";

		public int ProcessId { get; set; }

		public float Volume { get; set; }

		public bool IsPriority { get; set; }
	}
	public class AudioManager : IDisposable
	{
		private readonly MMDeviceEnumerator _enumerator;

		private MMDevice? _device;

		private readonly Dictionary<string, float> _originalVolumes = new Dictionary<string, float>(StringComparer.Ordinal);

		private float _duckLevel = 0.5f;

		private HashSet<string> _priorityProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public bool IsDucking { get; private set; }

		public AudioManager()
		{
			_enumerator = new MMDeviceEnumerator();
			try
			{
				_device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			}
			catch
			{
			}
		}

		private SessionCollection FreshSessions()
		{
			AudioSessionManager audioSessionManager = _device!.AudioSessionManager;
			audioSessionManager.RefreshSessions();
			return audioSessionManager.Sessions;
		}

		public List<AudioSessionInfo> GetAudioSessions()
		{
			List<AudioSessionInfo> list = new List<AudioSessionInfo>();
			if (_device == null)
			{
				return list;
			}
			try
			{
				SessionCollection sessionCollection = FreshSessions();
				for (int i = 0; i < sessionCollection.Count; i++)
				{
					try
					{
						AudioSessionControl audioSessionControl = sessionCollection[i];
						int getProcessID = (int)audioSessionControl.GetProcessID;
						if (getProcessID != 0)
						{
							Process processById = Process.GetProcessById(getProcessID);
							list.Add(new AudioSessionInfo
							{
								ProcessName = processById.ProcessName,
								ProcessId = getProcessID,
								Volume = audioSessionControl.SimpleAudioVolume.Volume * 100f,
								IsPriority = _priorityProcessNames.Contains(processById.ProcessName)
							});
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			return (from g in list.GroupBy<AudioSessionInfo, string>((AudioSessionInfo s) => s.ProcessName, StringComparer.OrdinalIgnoreCase)
				select g.First() into s
				orderby s.ProcessName
				select s).ToList();
		}

		public void SetPriorityApps(IEnumerable<string> names)
		{
			_priorityProcessNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
		}

		public void SetDuckLevel(int percent)
		{
			_duckLevel = (float)percent / 100f;
			if (IsDucking)
			{
				ReapplyDucking();
			}
		}

		private void ReapplyDucking()
		{
			if (_device == null)
			{
				return;
			}
			try
			{
				SessionCollection sessionCollection = FreshSessions();
				for (int i = 0; i < sessionCollection.Count; i++)
				{
					try
					{
						AudioSessionControl audioSessionControl = sessionCollection[i];
						string sessionId = audioSessionControl.GetSessionInstanceIdentifier;
						if (_originalVolumes.TryGetValue(sessionId, out var value))
						{
							audioSessionControl.SimpleAudioVolume.Volume = value * _duckLevel;
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		public void ActivateDucking()
		{
			if (_device != null && !IsDucking)
			{
				_originalVolumes.Clear();
				IsDucking = true;
				ApplyDucking();
			}
		}

		public void DeactivateDucking()
		{
			if (_device == null || !IsDucking)
			{
				return;
			}
			try
			{
				SessionCollection sessionCollection = FreshSessions();
				for (int i = 0; i < sessionCollection.Count; i++)
				{
					try
					{
						AudioSessionControl audioSessionControl = sessionCollection[i];
						string sessionId = audioSessionControl.GetSessionInstanceIdentifier;
						if (_originalVolumes.TryGetValue(sessionId, out var value))
						{
							audioSessionControl.SimpleAudioVolume.Volume = value;
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			_originalVolumes.Clear();
			IsDucking = false;
		}

		public void ResetAllVolumes()
		{
			if (_device == null)
			{
				return;
			}
			try
			{
				SessionCollection sessionCollection = FreshSessions();
				for (int i = 0; i < sessionCollection.Count; i++)
				{
					try
					{
						sessionCollection[i].SimpleAudioVolume.Volume = 1f;
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			_originalVolumes.Clear();
			IsDucking = false;
		}

		public void ReevaluatePriorities()
		{
			if (IsDucking)
			{
				DeactivateDucking();
				ActivateDucking();
			}
		}

		public void DuckNewSessions()
		{
			if (!IsDucking || _device == null)
			{
				return;
			}
			try
			{
				ApplyDucking();
			}
			catch
			{
			}
		}

		private void ApplyDucking()
		{
			if (_device == null)
			{
				return;
			}
			try
			{
				SessionCollection sessionCollection = FreshSessions();
				for (int i = 0; i < sessionCollection.Count; i++)
				{
					try
					{
						AudioSessionControl audioSessionControl = sessionCollection[i];
						int getProcessID = (int)audioSessionControl.GetProcessID;
						string sessionId = audioSessionControl.GetSessionInstanceIdentifier;
						if (getProcessID != 0 && !_originalVolumes.ContainsKey(sessionId))
						{
							Process processById = Process.GetProcessById(getProcessID);
							if (!_priorityProcessNames.Contains(processById.ProcessName))
							{
								float volume = audioSessionControl.SimpleAudioVolume.Volume;
								_originalVolumes[sessionId] = volume;
								audioSessionControl.SimpleAudioVolume.Volume = volume * _duckLevel;
							}
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		public void ToggleDucking()
		{
			if (IsDucking)
			{
				DeactivateDucking();
			}
			else
			{
				ActivateDucking();
			}
		}

		public void Dispose()
		{
			if (IsDucking)
			{
				DeactivateDucking();
			}
			_device?.Dispose();
			_enumerator?.Dispose();
		}
	}
}
