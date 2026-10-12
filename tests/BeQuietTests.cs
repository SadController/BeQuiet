using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using BeQuiet;
using BeQuiet.Audio;
using BeQuiet.Models;
using NAudio.CoreAudioApi;

internal static class BeQuietTests
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
        Console.WriteLine("PASS " + message);
    }
    private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);

    private static float Volume(int processId, float? set = null)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        device.AudioSessionManager.RefreshSessions();
        var sessions = device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];
            if (session.GetProcessID != processId) continue;
            if (set.HasValue) session.SimpleAudioVolume.Volume = set.Value;
            return session.SimpleAudioVolume.Volume;
        }
        throw new Exception("Audio session not found: " + processId);
    }
    private static void Near(int processId, float expected, string message) => Check(Math.Abs(Volume(processId) - expected) < 0.001f, message);
    private static Process StartSilent(string root, string name)
    {
        var process = Process.Start(new ProcessStartInfo(Path.Combine(root, "tests", "helpers", name + ".exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        })!;
        var deadline = Stopwatch.StartNew();
        while (true)
        {
            try { Volume(process.Id); return process; }
            catch { if (deadline.ElapsedMilliseconds > 5000) { process.Kill(); throw; } }
            Thread.Sleep(100);
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        string root = args[0];
        string settingsDirectory = Environment.GetEnvironmentVariable("BEQUIET_SETTINGS_DIR")!;
        if (string.IsNullOrEmpty(settingsDirectory) || !Path.GetFullPath(settingsDirectory).StartsWith(Path.Combine(root, "build") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Tests require isolated settings inside the build directory");
        Directory.CreateDirectory(settingsDirectory);
        string settingsFile = Path.Combine(settingsDirectory, "settings.json");
        var processes = new List<Process>();
        MainWindow? window = null;
        App? app = null;
        Dictionary<int, float> baseline = new();
        try
        {
            File.WriteAllText(settingsFile, "{\"PriorityProcessNames\":[\"SavedApp\"],\"DuckLevel\":20}");
            var migrated = AppSettings.Load();
            Check(migrated.FavoriteProcessNames!.SequenceEqual(new[] { "SavedApp" }), "Legacy priorities become favorites");
            Check(migrated.PriorityProcessNames.SequenceEqual(new[] { "SavedApp" }), "Legacy focus selection is preserved");

            // Always keep a separate protected session, even when the user has no audio apps open.
            processes.Add(StartSilent(root, "ProtectedAudioTest"));
            using (var audio = new AudioManager())
            {
                foreach (var session in audio.GetAudioSessions()) baseline[session.ProcessId] = Volume(session.ProcessId);
                var names = audio.GetAudioSessions().Select(s => s.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                File.WriteAllText(settingsFile, JsonSerializer.Serialize(new AppSettings
                {
                    FavoriteProcessNames = names.Concat(new[] { "FocusTarget", "BackgroundTest", "NewBackgroundTest", "OfflinePinned" }).ToList(),
                    PriorityProcessNames = names,
                    DuckLevel = 20
                }));
            }
            processes.Add(StartSilent(root, "FocusTarget"));
            processes.Add(StartSilent(root, "BackgroundTest"));
            int targetId = processes[^2].Id, backgroundId = processes[^1].Id;
            Volume(targetId, 0.73f);
            Volume(backgroundId, 0.62f);

            app = new App();
            app.InitializeComponent();
            window = new MainWindow();
            ((NotifyIcon)Field(window, "_trayIcon")).Visible = false;
            var settings = (AppSettings)Field(window, "_settings");
            var manager = (AudioManager)Field(window, "_audioManager");

            Invoke(window, "SetFavorite", "NoSoundYet", true);
            Check(!manager.IsDucking && !settings.PriorityProcessNames.Contains("NoSoundYet"), "Adding a favorite does not activate focus");
            Invoke(window, "RefreshSessions");
            var displayed = (IEnumerable<AudioSessionDisplay>)Field(window, "_sessions");
            Check(displayed.Any(s => s.ProcessName == "NoSoundYet" && s.IsPriority && s.ProcessId == 0), "Offline favorites remain in the main list");
            Check(displayed.Single(s => s.ProcessName == "FocusTarget").IsPriority && !settings.PriorityProcessNames.Contains("FocusTarget"), "Main checkboxes represent favorites, independently of focus");

            var content = (System.Windows.FrameworkElement)window.Content;
            content.Measure(new System.Windows.Size(800, 800));
            content.Arrange(new System.Windows.Rect(0, 0, 800, 800));
            content.UpdateLayout();
            Invoke(window, "UpdateFavoritesLabels");
            var labels = TextLabels(content).ToList();
            Check(labels.Any(label => label.Text == "Favorites"), "Favorites heading is English");
            Check(labels.Where(label => label.DataContext is not AudioSessionDisplay).All(label => IsEnglish(label.Text)), "Main window labels contain no Russian text");
            Check(IsEnglish(window.AppList.ToolTip?.ToString() ?? ""), "Favorites tooltip is English");
            Check(displayed.Single(s => s.ProcessName == "NoSoundYet").VolumeDisplay == "no audio", "Offline app status is English");
            var list = (System.Windows.Controls.ListBox)Field(window, "AppList");
            var targetRow = list.ItemContainerGenerator.ContainerFromItem(displayed.Single(s => s.ProcessName == "FocusTarget"));
            var checkbox = FindCheckbox(targetRow)!;
            Check(checkbox != null && checkbox.IsChecked == true, "Original WPF template binds the favorite checkbox");
            checkbox!.IsChecked = false;
            Check(!settings.FavoriteProcessNames!.Contains("FocusTarget"), "Main checkbox removes a favorite through its real event handler");
            checkbox.IsChecked = true;
            Check(settings.FavoriteProcessNames.Contains("FocusTarget") && !settings.PriorityProcessNames.Contains("FocusTarget") && !manager.IsDucking, "Main checkbox adds a favorite without selecting focus");

            Invoke(window, "RebuildFavoritesMenu");
            var menu = (ContextMenuStrip)Field(window, "_favoritesMenu");
            Check(menu.Items.Cast<ToolStripItem>().Where(item => item.Tag == null).All(item => IsEnglish(item.Text)) && menu.Items.Cast<ToolStripItem>().All(item => IsEnglish(item.ToolTipText)), "Tray menu labels and tooltips are English");
            Check(menu.Items.OfType<ToolStripMenuItem>().Any(i => (string?)i.Tag == "OfflinePinned"), "Offline favorite appears in the tray menu");
            var targetItem = menu.Items.OfType<ToolStripMenuItem>().Single(i => (string?)i.Tag == "FocusTarget");
            Check(!targetItem.Checked, "Favorite starts unchecked in the focus menu");
            targetItem.PerformClick();
            Check(manager.IsDucking && settings.PriorityProcessNames.Contains("FocusTarget"), "Clicking a tray app selects focus and activates ducking");
            Check(IsEnglish(window.StatusText.Text) && IsEnglish(window.ActivateButton.Content.ToString()!) && IsEnglish(((NotifyIcon)Field(window, "_trayIcon")).Text), "Active focus controls and tray status are English");
            Near(targetId, 0.73f, "Focused volume stays unchanged");
            Near(backgroundId, 0.124f, "Background volume is reduced to 20 percent of its original level");
            foreach (var before in baseline) Near(before.Key, before.Value, "Existing app remains protected during testing");

            Invoke(window, "SetFocused", "BackgroundTest", true);
            Near(targetId, 0.73f, "Multiple focused apps keep the first app's volume");
            Near(backgroundId, 0.62f, "Adding a ducked app to focus restores its original volume");
            Invoke(window, "SetFocused", "FocusTarget", false);
            Near(targetId, 0.146f, "Removing focus ducks that app from its original volume");
            Near(backgroundId, 0.62f, "Remaining focused app stays unchanged");

            processes.Add(StartSilent(root, "NewBackgroundTest"));
            int newId = processes[^1].Id;
            Volume(newId, 0.41f);
            manager.DuckNewSessions();
            Near(newId, 0.082f, "New background sessions are ducked while focus is active");
            manager.DuckNewSessions();
            Near(newId, 0.082f, "Polling does not repeatedly multiply the volume");

            Invoke(window, "SetFavorite", "OfflinePinned", false);
            Check(settings.FavoriteProcessNames!.Contains("NoSoundYet"), "Removing one favorite preserves other offline favorites");
            Invoke(window, "SetFavorite", "BackgroundTest", false);
            Check(!settings.PriorityProcessNames.Contains("BackgroundTest"), "Removing a favorite also removes it from focus");
            Near(backgroundId, 0.124f, "Removed favorite becomes background while another focus is active");

            // Exercise the final-item branch without temporarily ducking the user's existing sessions.
            settings.PriorityProcessNames = new List<string> { "FocusTarget" };
            Invoke(window, "SetFocused", "FocusTarget", false);
            Check(!manager.IsDucking && settings.PriorityProcessNames.Count == 0, "Unchecking the final focused app turns focus off");
            Check(IsEnglish(window.StatusText.Text) && IsEnglish(window.ActivateButton.Content.ToString()!), "Inactive focus controls are English");
            Near(targetId, 0.73f, "Final uncheck restores the first app's exact volume");
            Near(backgroundId, 0.62f, "Final uncheck restores the second app's exact volume");
            Near(newId, 0.41f, "Final uncheck restores newly created sessions");
            var saved = AppSettings.Load();
            Check(saved.FavoriteProcessNames!.Contains("NoSoundYet") && !saved.FavoriteProcessNames.Contains("BackgroundTest"), "Favorite changes survive settings reload");
            Check(saved.PriorityProcessNames.Count == 0, "Cleared focus selection survives settings reload");
            Console.WriteLine($"SUCCESS: {_checks} checks on Windows {Environment.OSVersion.Version}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            if (window != null) window.ExitApp();
            else app?.Shutdown();
            foreach (var process in processes)
            {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); }
                process.Dispose();
            }
            foreach (var before in baseline)
            {
                try { Volume(before.Key, before.Value); }
                catch { /* An app may have closed while the test ran. */ }
            }
        }
    }

    private static System.Windows.Controls.CheckBox? FindCheckbox(System.Windows.DependencyObject? root)
    {
        if (root == null) return null;
        if (root is System.Windows.Controls.CheckBox checkbox) return checkbox;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindCheckbox(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    private static bool IsEnglish(string? text) => !System.Text.RegularExpressions.Regex.IsMatch(text ?? "", "[\\u0400-\\u04ff]");

    private static IEnumerable<System.Windows.Controls.TextBlock> TextLabels(System.Windows.DependencyObject root)
    {
        if (root is System.Windows.Controls.TextBlock label) yield return label;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in TextLabels(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
