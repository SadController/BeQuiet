using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;

namespace BeQuiet
{
    public partial class MainWindow
    {
        private ContextMenuStrip _favoritesMenu = null!;

        private NotifyIcon CreateFavoritesTrayIcon()
        {
            var icon = new NotifyIcon { Text = "BeQuiet — focus off" };
            using (var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"))?.Stream)
            {
                if (resource != null) icon.Icon = new Icon(resource);
            }
            _favoritesMenu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
            _favoritesMenu.Opening += (_, _) => RebuildFavoritesMenu();
            icon.ContextMenuStrip = _favoritesMenu;
            icon.MouseClick += (_, args) =>
            {
                if (args.Button == MouseButtons.Left)
                    Dispatcher.Invoke(() => _favoritesMenu.Show(System.Windows.Forms.Cursor.Position));
            };
            return icon;
        }

        private void RebuildFavoritesMenu()
        {
            var oldItems = _favoritesMenu.Items.Cast<ToolStripItem>().ToArray();
            _favoritesMenu.Items.Clear();
            foreach (var item in oldItems) item.Dispose();

            _toggleMenuItem = new ToolStripMenuItem
            {
                Text = _audioManager.IsDucking ? "Focus on — turn off" : "Focus off — turn on",
                Checked = _audioManager.IsDucking,
                Enabled = _audioManager.IsDucking || _settings.PriorityProcessNames.Count > 0
            };
            _toggleMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleDucking);
            _favoritesMenu.Items.Add(_toggleMenuItem);
            _favoritesMenu.Items.Add(new ToolStripSeparator());
            _favoritesMenu.Items.Add(new ToolStripMenuItem("Favorites — choose focus") { Enabled = false });

            var liveNames = _audioManager.GetAudioSessions().Select(s => s.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string name in _settings.FavoriteProcessNames!.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                string processName = name;
                var item = new ToolStripMenuItem(liveNames.Contains(name) ? name : name + " (no audio)")
                {
                    Tag = name,
                    Checked = _settings.PriorityProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase),
                    ToolTipText = "Keep this app at its original volume. Other apps will be turned down."
                };
                item.Click += (_, _) => Dispatcher.Invoke(() => SetFocused(processName, !_settings.PriorityProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase)));
                _favoritesMenu.Items.Add(item);
            }
            if (_settings.FavoriteProcessNames!.Count == 0)
                _favoritesMenu.Items.Add(new ToolStripMenuItem("Add favorites in the main window") { Enabled = false });

            _favoritesMenu.Items.Add(new ToolStripSeparator());
            _favoritesMenu.Items.Add("Manage favorites…", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
            _favoritesMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApp));
        }

        private void SetFavorite(string name, bool favorite)
        {
            var favorites = _settings.FavoriteProcessNames!;
            if (favorites.Contains(name, StringComparer.OrdinalIgnoreCase) == favorite) return;
            if (favorite) favorites.Add(name);
            else
            {
                favorites.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                bool removedFocus = _settings.PriorityProcessNames.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) > 0;
                if (removedFocus) ApplyFocusedApps(false);
            }
            _settings.Save();
            UpdateFocusStatus();
        }

        private void SetFocused(string name, bool focused)
        {
            if (!_settings.FavoriteProcessNames!.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
            bool wasFocused = _settings.PriorityProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase);
            if (wasFocused == focused) return;
            if (focused) _settings.PriorityProcessNames.Add(name);
            else _settings.PriorityProcessNames.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            ApplyFocusedApps(true);
            _settings.Save();
        }

        private void ApplyFocusedApps(bool activate)
        {
            _audioManager.SetPriorityApps(_settings.PriorityProcessNames);
            if (_settings.PriorityProcessNames.Count == 0) _audioManager.DeactivateDucking();
            else if (_audioManager.IsDucking) _audioManager.ReevaluatePriorities();
            else if (activate) _audioManager.ActivateDucking();
            UpdateUI();
        }

        private void UpdateFocusStatus()
        {
            bool active = _audioManager.IsDucking;
            ActivateButton.Content = active ? "[ DISABLE FOCUS ]" : "[ ENABLE FOCUS ]";
            StatusText.Text = active ? $"Focus: {_settings.PriorityProcessNames.Count} • background: {_settings.DuckLevel}%" : "Focus off • choose apps in the tray";
            _trayIcon.Text = active ? $"BeQuiet — focus on, background {_settings.DuckLevel}%" : "BeQuiet — focus off";
            if (_toggleMenuItem != null && !_toggleMenuItem.IsDisposed)
            {
                _toggleMenuItem.Checked = active;
                _toggleMenuItem.Text = active ? "Focus on — turn off" : "Focus off — turn on";
                _toggleMenuItem.Enabled = active || _settings.PriorityProcessNames.Count > 0;
            }
        }

        private void UpdateFavoritesLabels()
        {
            ReplaceHeading(Content is DependencyObject content ? content : this);
            AppList.ToolTip = "Choose your favorites here, then select focus apps from the BeQuiet tray icon.";
            UpdateFocusStatus();
        }

        private static void ReplaceHeading(DependencyObject root)
        {
            if (root is TextBlock label && label.Text == "Priority Apps") label.Text = "Favorites";
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                ReplaceHeading(VisualTreeHelper.GetChild(root, i));
        }
    }
}
