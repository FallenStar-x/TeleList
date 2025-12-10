using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeleList.Dialogs;
using TeleList.Models;
using TeleList.Services;
using TeleList.ViewModels;
using Microsoft.Win32;

namespace TeleList
{
    /// <summary>
    /// Main application window for viewing and managing game entities.
    /// Provides filtering, sorting, marking, and INI coordinate updating functionality.
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private List<Entity> _entities = new List<Entity>();
        private ObservableCollection<EntityViewModel> _filteredEntities = new ObservableCollection<EntityViewModel>();
        private Entity? _referenceEntity;
        private string _currentFilepath = string.Empty;
        private HashSet<string> _markedEntities = new HashSet<string>();

        // File watching
        private bool _fileWatcherRunning = false;
        private double _lastModifiedTime = 0;
        private long _lastFileSize = 0;
        private CancellationTokenSource? _watcherCts;

        // Global hotkeys
        private GlobalHotkeyManager? _hotkeyManager;
        private Dictionary<string, int> _hotkeyIds = new Dictionary<string, int>();

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.LoadSettings();
            EntityGrid.ItemsSource = _filteredEntities;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply settings
            Width = _settings.WindowWidth;
            Height = _settings.WindowHeight;

            // Validate window position is on screen
            var left = _settings.WindowLeft;
            var top = _settings.WindowTop;

            // Get virtual screen bounds (all monitors combined)
            var screenLeft = SystemParameters.VirtualScreenLeft;
            var screenTop = SystemParameters.VirtualScreenTop;
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;

            // Ensure window is at least partially visible
            if (left < screenLeft - Width + 100 || left > screenLeft + screenWidth - 100)
            {
                left = 100; // Reset to safe position
            }
            if (top < screenTop || top > screenTop + screenHeight - 50)
            {
                top = 100; // Reset to safe position
            }

            Left = left;
            Top = top;

            AutoRefreshCheckBox.IsChecked = _settings.AutoRefresh;
            GlobalHotkeysCheckBox.IsChecked = _settings.GlobalHotkeysEnabled;
            AutoUpdateIniCheckBox.IsChecked = _settings.AutoUpdateIni;
            IniPathBox.Text = _settings.IniFilePath;

            _markedEntities = new HashSet<string>(_settings.MarkedEntities ?? new List<string>());

            // Update hotkey button labels
            HotkeyNextBtn.Content = _settings.HotkeyNext;
            HotkeyPrevBtn.Content = _settings.HotkeyPrev;
            HotkeyMarkBtn.Content = _settings.HotkeyMark;
            HotkeyReloadBtn.Content = _settings.HotkeyReload;
            HotkeyUpdateIniBtn.Content = _settings.HotkeyUpdateIni;
            HotkeyClearBtn.Content = _settings.HotkeyClear;

            // Setup global hotkeys
            SetupGlobalHotkeys();

            // Auto-load entities file if configured and exists
            if (!string.IsNullOrEmpty(_settings.EntitiesFilePath) && File.Exists(_settings.EntitiesFilePath))
            {
                LoadFile(_settings.EntitiesFilePath);
            }
            else
            {
                StatusLabel.Text = "No entities file configured. Click 'Load File' to select one.";
            }

            RefreshIniCoordinates();
            UpdateMarkedCount();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save window geometry
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.MarkedEntities = _markedEntities.ToList();

            if (!SettingsManager.SaveSettings(_settings))
            {
                var result = MessageBox.Show(
                    "Could not save settings. Your marked items may be lost.\n\nClose anyway?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Cleanup
            StopFileWatcher();
            _hotkeyManager?.Dispose();
        }

        #region Global Hotkeys

        private void SetupGlobalHotkeys()
        {
            // Dispose existing manager
            _hotkeyManager?.Dispose();
            _hotkeyIds.Clear();

            if (!_settings.GlobalHotkeysEnabled)
                return;

            try
            {
                _hotkeyManager = new GlobalHotkeyManager(this);

                var totalHotkeys = 6;
                RegisterHotkey("HotkeyNext", _settings.HotkeyNext, NavigateNext);
                RegisterHotkey("HotkeyPrev", _settings.HotkeyPrev, NavigatePrev);
                RegisterHotkey("HotkeyMark", _settings.HotkeyMark, ToggleMarkSelected);
                RegisterHotkey("HotkeyReload", _settings.HotkeyReload, () => ReloadFile());
                RegisterHotkey("HotkeyUpdateIni", _settings.HotkeyUpdateIni, () => UpdateIniCoordinates(false));
                RegisterHotkey("HotkeyClear", _settings.HotkeyClear, ClearEntitiesFile);

                var registered = _hotkeyIds.Count;
                if (registered < totalHotkeys)
                {
                    StatusLabel.Text = $"Hotkeys: {registered}/{totalHotkeys} registered (some may be in use)";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Hotkey error: {ex.Message}";
            }
        }

        private void RegisterHotkey(string name, string hotkeyString, Action action)
        {
            if (_hotkeyManager == null || string.IsNullOrWhiteSpace(hotkeyString))
                return;

            var id = _hotkeyManager.RegisterHotkey(hotkeyString, action);
            if (id > 0)
            {
                _hotkeyIds[name] = id;
            }
            else
            {
                // Log failed registration - hotkey might be in use by another app or invalid
                System.Diagnostics.Debug.WriteLine($"Failed to register hotkey: {name} = {hotkeyString}");
            }
        }

        private int GetRegisteredHotkeyCount()
        {
            return _hotkeyIds.Count;
        }

        private void GlobalHotkeys_Changed(object sender, RoutedEventArgs e)
        {
            _settings.GlobalHotkeysEnabled = GlobalHotkeysCheckBox.IsChecked ?? false;
            SettingsManager.SaveSettings(_settings);

            if (_settings.GlobalHotkeysEnabled)
            {
                SetupGlobalHotkeys();
                StatusLabel.Text = "Global hotkeys enabled";
            }
            else
            {
                _hotkeyManager?.Dispose();
                _hotkeyManager = null;
                StatusLabel.Text = "Global hotkeys disabled";
            }
        }

        private void ConfigureHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string keyName)
                return;

            var currentKey = keyName switch
            {
                "HotkeyNext" => _settings.HotkeyNext,
                "HotkeyPrev" => _settings.HotkeyPrev,
                "HotkeyMark" => _settings.HotkeyMark,
                "HotkeyReload" => _settings.HotkeyReload,
                "HotkeyUpdateIni" => _settings.HotkeyUpdateIni,
                "HotkeyClear" => _settings.HotkeyClear,
                _ => ""
            };

            var dialog = new HotkeyConfigDialog(this, $"Configure: {keyName}", currentKey);
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Result))
            {
                // Update settings
                switch (keyName)
                {
                    case "HotkeyNext": _settings.HotkeyNext = dialog.Result; break;
                    case "HotkeyPrev": _settings.HotkeyPrev = dialog.Result; break;
                    case "HotkeyMark": _settings.HotkeyMark = dialog.Result; break;
                    case "HotkeyReload": _settings.HotkeyReload = dialog.Result; break;
                    case "HotkeyUpdateIni": _settings.HotkeyUpdateIni = dialog.Result; break;
                    case "HotkeyClear": _settings.HotkeyClear = dialog.Result; break;
                }

                btn.Content = dialog.Result;
                SettingsManager.SaveSettings(_settings);
                SetupGlobalHotkeys();
                StatusLabel.Text = $"Hotkey set to: {dialog.Result}";
            }
        }

        #endregion

        #region Navigation

        private void NavigateNext()
        {
            try
            {
                if (_filteredEntities == null || _filteredEntities.Count == 0)
                    return;

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var currentIndex = EntityGrid.SelectedIndex;
                        var count = _filteredEntities.Count;

                        if (currentIndex < count - 1)
                        {
                            EntityGrid.SelectedIndex = currentIndex + 1;
                        }
                        else if (currentIndex == -1 && count > 0)
                        {
                            EntityGrid.SelectedIndex = 0;
                        }

                        // Safely scroll into view
                        var selectedItem = EntityGrid.SelectedItem;
                        if (selectedItem != null)
                        {
                            EntityGrid.ScrollIntoView(selectedItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NavigateNext error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NavigateNext outer error: {ex.Message}");
            }
        }

        private void NavigatePrev()
        {
            try
            {
                if (_filteredEntities == null || _filteredEntities.Count == 0)
                    return;

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var currentIndex = EntityGrid.SelectedIndex;
                        var count = _filteredEntities.Count;

                        if (currentIndex > 0)
                        {
                            EntityGrid.SelectedIndex = currentIndex - 1;
                        }
                        else if (currentIndex == -1 && count > 0)
                        {
                            EntityGrid.SelectedIndex = count - 1;
                        }

                        // Safely scroll into view
                        var selectedItem = EntityGrid.SelectedItem;
                        if (selectedItem != null)
                        {
                            EntityGrid.ScrollIntoView(selectedItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NavigatePrev error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NavigatePrev outer error: {ex.Message}");
            }
        }

        private void EntityGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoUpdateIniCheckBox.IsChecked == true)
            {
                UpdateIniCoordinates(true);
            }
        }

        #endregion

        #region File Operations

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Entity File",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(_currentFilepath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
            }
        }

        private void LoadFile(string filepath, bool silent = false)
        {
            try
            {
                _entities = EntityParser.ParseFile(filepath);
                _currentFilepath = filepath;
                FilePathLabel.Text = Path.GetFileName(filepath);

                _settings.EntitiesFilePath = filepath;
                SettingsManager.SaveSettings(_settings);

                // Update file tracking
                if (File.Exists(filepath))
                {
                    var fileInfo = new FileInfo(filepath);
                    _lastModifiedTime = fileInfo.LastWriteTime.Ticks;
                    _lastFileSize = fileInfo.Length;
                }

                // Update type filter options
                UpdateTypeFilterOptions();

                // Update reference combo
                UpdateReferenceComboOptions();

                // Try to set PlayerAvatar_0 as default reference
                var player = _entities.FirstOrDefault(e => e.EntityType.Contains("PlayerAvatar"));
                if (player != null)
                {
                    _referenceEntity = player;
                    ReferenceCombo.Text = player.EntityType;
                    ReferenceInfoLabel.Text = $"Reference: {player.EntityType}";
                }

                ApplyFilters();
                UpdateMarkedCount();

                if (!silent)
                {
                    StatusLabel.Text = $"Loaded {_entities.Count} entities from {Path.GetFileName(filepath)}";
                }
                else
                {
                    StatusLabel.Text = $"Auto-refreshed: {_entities.Count} entities";
                }

                // Start file watcher
                if (_settings.AutoRefresh && !_fileWatcherRunning)
                {
                    StartFileWatcher();
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                StatusLabel.Text = $"Error loading file: {ex.Message}";
            }
        }

        private void ReloadFile_Click(object sender, RoutedEventArgs e)
        {
            ReloadFile();
        }

        private void ReloadFile()
        {
            if (!string.IsNullOrEmpty(_currentFilepath))
            {
                LoadFile(_currentFilepath);
            }
            else
            {
                StatusLabel.Text = "No file to reload";
            }
        }

        private void ClearEntities_Click(object sender, RoutedEventArgs e)
        {
            ClearEntitiesFile();
        }

        private void ClearEntitiesFile()
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(_currentFilepath))
                {
                    StatusLabel.Text = "No entities file loaded";
                    return;
                }

                if (!File.Exists(_currentFilepath))
                {
                    StatusLabel.Text = "Entities file not found";
                    return;
                }

                try
                {
                    File.WriteAllText(_currentFilepath, string.Empty);

                    // Update tracking
                    var fileInfo = new FileInfo(_currentFilepath);
                    _lastModifiedTime = fileInfo.LastWriteTime.Ticks;
                    _lastFileSize = 0;

                    _entities.Clear();
                    _filteredEntities.Clear();

                    StatusLabel.Text = $"Cleared: {Path.GetFileName(_currentFilepath)}";
                }
                catch (Exception ex)
                {
                    StatusLabel.Text = $"Error clearing file: {ex.Message}";
                }
            });
        }

        #endregion

        #region File Watching

        private void AutoRefresh_Changed(object sender, RoutedEventArgs e)
        {
            _settings.AutoRefresh = AutoRefreshCheckBox.IsChecked ?? false;
            SettingsManager.SaveSettings(_settings);

            if (_settings.AutoRefresh)
            {
                StartFileWatcher();
                StatusLabel.Text = "Auto-refresh enabled";
            }
            else
            {
                StopFileWatcher();
                StatusLabel.Text = "Auto-refresh disabled";
            }
        }

        private void StartFileWatcher()
        {
            if (string.IsNullOrEmpty(_currentFilepath) || _fileWatcherRunning)
                return;

            _fileWatcherRunning = true;
            _watcherCts = new CancellationTokenSource();
            var token = _watcherCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !string.IsNullOrEmpty(_currentFilepath))
                {
                    try
                    {
                        if (File.Exists(_currentFilepath))
                        {
                            var fileInfo = new FileInfo(_currentFilepath);
                            var currentMtime = fileInfo.LastWriteTime.Ticks;
                            var currentSize = fileInfo.Length;

                            if (currentMtime != _lastModifiedTime || currentSize != _lastFileSize)
                            {
                                _lastModifiedTime = currentMtime;
                                _lastFileSize = currentSize;

                                Dispatcher.Invoke(() => LoadFile(_currentFilepath, true));
                            }
                        }

                        await Task.Delay(500, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        await Task.Delay(1000, token);
                    }
                }
            }, token);
        }

        private void StopFileWatcher()
        {
            _fileWatcherRunning = false;
            _watcherCts?.Cancel();
            _watcherCts?.Dispose();
            _watcherCts = null;
        }

        #endregion

        #region Filtering and Sorting

        private void UpdateTypeFilterOptions()
        {
            var baseTypes = _entities.Select(e => e.BaseType).Distinct().OrderBy(t => t).ToList();
            TypeFilterCombo.Items.Clear();
            TypeFilterCombo.Items.Add("All Types");
            foreach (var type in baseTypes)
            {
                TypeFilterCombo.Items.Add(type);
            }
            TypeFilterCombo.SelectedIndex = 0;
        }

        private void UpdateReferenceComboOptions()
        {
            var entityTypes = _entities.Select(e => e.EntityType).Distinct().OrderBy(t => t).ToList();
            ReferenceCombo.Items.Clear();
            foreach (var type in entityTypes)
            {
                ReferenceCombo.Items.Add(type);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void QuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filterValue)
            {
                SearchBox.Text = filterValue;
                if (string.IsNullOrEmpty(filterValue))
                {
                    TypeFilterCombo.SelectedIndex = 0;
                }
            }
        }

        private void ApplyFilters()
        {
            if (_entities.Count == 0)
            {
                _filteredEntities.Clear();
                UpdateCount();
                return;
            }

            var filtered = _entities.AsEnumerable();

            // Apply search filter
            var searchText = SearchBox.Text?.ToLower().Trim() ?? "";
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(e => e.EntityType.ToLower().Contains(searchText));
            }

            // Apply type filter
            var typeFilter = TypeFilterCombo.SelectedItem?.ToString() ?? "All Types";
            if (typeFilter != "All Types")
            {
                filtered = filtered.Where(e => e.BaseType == typeFilter);
            }

            // Apply sorting
            var sortOption = (SortCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Distance (Ascending)";
            var reverse = sortOption.Contains("Descending") || sortOption.Contains("Z-A");

            filtered = sortOption switch
            {
                var s when s.Contains("Entity Type") =>
                    reverse ? filtered.OrderByDescending(e => e.EntityType.ToLower())
                            : filtered.OrderBy(e => e.EntityType.ToLower()),

                var s when s.Contains("Location X") =>
                    reverse ? filtered.OrderByDescending(e => e.X) : filtered.OrderBy(e => e.X),

                var s when s.Contains("Location Y") =>
                    reverse ? filtered.OrderByDescending(e => e.Y) : filtered.OrderBy(e => e.Y),

                var s when s.Contains("Location Z") =>
                    reverse ? filtered.OrderByDescending(e => e.Z) : filtered.OrderBy(e => e.Z),

                _ => _referenceEntity != null
                    ? (reverse ? filtered.OrderByDescending(e => Entity.CalculateDistance(_referenceEntity, e))
                               : filtered.OrderBy(e => Entity.CalculateDistance(_referenceEntity, e)))
                    : (reverse ? filtered.OrderByDescending(e => e.Distance)
                               : filtered.OrderBy(e => e.Distance))
            };

            // Update observable collection
            _filteredEntities.Clear();
            foreach (var entity in filtered)
            {
                var calcDist = _referenceEntity != null
                    ? Entity.CalculateDistance(_referenceEntity, entity).ToString("F2")
                    : "";

                var entityKey = entity.GetEntityKey();

                _filteredEntities.Add(new EntityViewModel
                {
                    EntityType = entity.EntityType,
                    BaseType = entity.BaseType,
                    LocationStr = entity.LocationStr,
                    X = entity.X,
                    Y = entity.Y,
                    Z = entity.Z,
                    Distance = entity.Distance,
                    CalcDistance = calcDist,
                    IsMarked = _markedEntities.Contains(entityKey)
                });
            }

            UpdateCount();
        }

        private void UpdateCount()
        {
            CountLabel.Text = $"Showing {_filteredEntities.Count} of {_entities.Count} entities";
        }

        #endregion

        #region Reference Entity

        private void SetReference_Click(object sender, RoutedEventArgs e)
        {
            var refType = ReferenceCombo.Text;
            var refEntity = _entities.FirstOrDefault(en => en.EntityType == refType);

            if (refEntity != null)
            {
                _referenceEntity = refEntity;
                ReferenceInfoLabel.Text = $"Reference: {refEntity.EntityType}";
                ApplyFilters();
                StatusLabel.Text = $"Reference set to {refEntity.EntityType}";
            }
            else
            {
                StatusLabel.Text = $"Entity not found: {refType}";
            }
        }

        private void UseSelected_Click(object sender, RoutedEventArgs e)
        {
            if (EntityGrid.SelectedItem is not EntityViewModel selected)
            {
                StatusLabel.Text = "No entity selected";
                return;
            }

            var refEntity = _entities.FirstOrDefault(en =>
                en.EntityType == selected.EntityType && en.LocationStr == selected.LocationStr);

            if (refEntity != null)
            {
                _referenceEntity = refEntity;
                ReferenceCombo.Text = refEntity.EntityType;
                ReferenceInfoLabel.Text = $"Reference: {refEntity.EntityType}";
                ApplyFilters();
                StatusLabel.Text = $"Reference set to {refEntity.EntityType}";
            }
        }

        private void FindClosest_Click(object sender, RoutedEventArgs e)
        {
            if (EntityGrid.SelectedItem is not EntityViewModel selected)
            {
                StatusLabel.Text = "No entity selected";
                return;
            }

            var refEntity = _entities.FirstOrDefault(en =>
                en.EntityType == selected.EntityType && en.LocationStr == selected.LocationStr);

            if (refEntity != null)
            {
                _referenceEntity = refEntity;
                ReferenceCombo.Text = refEntity.EntityType;
                ReferenceInfoLabel.Text = $"Reference: {refEntity.EntityType}";
                SortCombo.SelectedIndex = 0; // Distance (Ascending)
                ApplyFilters();
                StatusLabel.Text = $"Showing entities closest to {refEntity.EntityType}";
            }
        }

        #endregion

        #region INI File Operations

        private void BrowseIni_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select INI File",
                Filter = "INI files (*.ini)|*.ini|All files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(_settings.IniFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                _settings.IniFilePath = dialog.FileName;
                IniPathBox.Text = dialog.FileName;
                SettingsManager.SaveSettings(_settings);
                RefreshIniCoordinates();
                StatusLabel.Text = $"INI file set to: {Path.GetFileName(dialog.FileName)}";
            }
        }

        private void RefreshIni_Click(object sender, RoutedEventArgs e)
        {
            RefreshIniCoordinates();
        }

        /// <summary>
        /// Reads and displays current teleport coordinates from the INI file.
        /// </summary>
        private void RefreshIniCoordinates()
        {
            var iniPath = IniPathBox.Text;
            if (string.IsNullOrEmpty(iniPath))
            {
                IniCoordsLabel.Text = "Current: No INI file configured";
                return;
            }

            if (!File.Exists(iniPath))
            {
                IniCoordsLabel.Text = "Current: File not found";
                return;
            }

            var coords = INICoordinateUpdater.GetCurrentCoordinates(iniPath);
            if (coords.HasValue)
            {
                IniCoordsLabel.Text = $"Current: {coords.Value.x:F2}, {coords.Value.y:F2}, {coords.Value.z:F2}";
            }
            else
            {
                IniCoordsLabel.Text = "Current: No coordinates found in INI";
            }
        }

        private void UpdateIni_Click(object sender, RoutedEventArgs e)
        {
            UpdateIniCoordinates(false);
        }

        private void AutoUpdateIni_Changed(object sender, RoutedEventArgs e)
        {
            _settings.AutoUpdateIni = AutoUpdateIniCheckBox.IsChecked ?? false;
            SettingsManager.SaveSettings(_settings);

            StatusLabel.Text = _settings.AutoUpdateIni
                ? "Auto Update INI enabled"
                : "Auto Update INI disabled";
        }

        private void UpdateIniCoordinates(bool silent)
        {
            Dispatcher.Invoke(() =>
            {
                if (EntityGrid.SelectedItem is not EntityViewModel selected)
                {
                    if (!silent) StatusLabel.Text = "No entity selected";
                    return;
                }

                var iniPath = IniPathBox.Text;
                if (string.IsNullOrEmpty(iniPath))
                {
                    if (!silent) StatusLabel.Text = "No INI file configured";
                    return;
                }

                if (!File.Exists(iniPath))
                {
                    if (!silent) StatusLabel.Text = "INI file not found";
                    return;
                }

                var (success, message) = INICoordinateUpdater.UpdateCoordinates(
                    iniPath, selected.X, selected.Y, selected.Z);

                if (success)
                {
                    RefreshIniCoordinates();
                    if (!silent)
                    {
                        StatusLabel.Text = $"INI updated: {selected.X:F2}, {selected.Y:F2}, {selected.Z:F2}";
                    }
                }
                else
                {
                    StatusLabel.Text = $"Update failed: {message}";
                }
            });
        }

        #endregion

        #region Marking

        private void ToggleMark_Click(object sender, RoutedEventArgs e)
        {
            ToggleMarkSelected();
        }

        private void ToggleMarkSelected()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (EntityGrid.SelectedItem is not EntityViewModel selected)
                        {
                            StatusLabel.Text = "No entity selected to mark";
                            return;
                        }

                        var entityKey = selected.GetEntityKey();
                        var displayName = string.IsNullOrEmpty(selected.EntityType)
                            ? "Unknown"
                            : (selected.EntityType.Length > 50
                                ? selected.EntityType.Substring(0, 50) + "..."
                                : selected.EntityType);

                        if (_markedEntities.Contains(entityKey))
                        {
                            _markedEntities.Remove(entityKey);
                            selected.IsMarked = false;
                            StatusLabel.Text = $"Unmarked: {displayName}";
                        }
                        else
                        {
                            _markedEntities.Add(entityKey);
                            selected.IsMarked = true;
                            StatusLabel.Text = $"Marked: {displayName}";
                        }

                        UpdateMarkedCount();

                        _settings.MarkedEntities = _markedEntities.ToList();
                        SettingsManager.SaveSettings(_settings);

                        // Force refresh the row
                        EntityGrid.Items.Refresh();
                    }
                    catch (Exception ex)
                    {
                        StatusLabel.Text = $"Mark error: {ex.Message}";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ToggleMarkSelected error: {ex.Message}");
            }
        }

        private void ClearMarks_Click(object sender, RoutedEventArgs e)
        {
            _markedEntities.Clear();
            _settings.MarkedEntities.Clear();
            SettingsManager.SaveSettings(_settings);

            foreach (var entity in _filteredEntities)
            {
                entity.IsMarked = false;
            }

            EntityGrid.Items.Refresh();
            UpdateMarkedCount();
            StatusLabel.Text = "All marks cleared";
        }

        private void UpdateMarkedCount()
        {
            MarkedCountLabel.Text = $"Marked: {_markedEntities.Count}";
        }

        #endregion

        #region Clipboard Operations

        private void EntityGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            CopyAllInfo();
        }

        private void EntityGrid_RightClick(object sender, MouseButtonEventArgs e)
        {
            // Context menu is handled via XAML
        }

        private void CopyEntityType_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EntityGrid.SelectedItem is EntityViewModel selected && !string.IsNullOrEmpty(selected.EntityType))
                {
                    Clipboard.SetText(selected.EntityType);
                    StatusLabel.Text = "Entity type copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Copy failed: {ex.Message}";
            }
        }

        private void CopyLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EntityGrid.SelectedItem is EntityViewModel selected && !string.IsNullOrEmpty(selected.LocationStr))
                {
                    Clipboard.SetText(selected.LocationStr);
                    StatusLabel.Text = "Location copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Copy failed: {ex.Message}";
            }
        }

        private void CopyAllInfo_Click(object sender, RoutedEventArgs e)
        {
            CopyAllInfo();
        }

        private void CopyAllInfo()
        {
            try
            {
                if (EntityGrid.SelectedItem is EntityViewModel selected)
                {
                    var info = $"Type: {selected.EntityType ?? "Unknown"}\nLocation: {selected.LocationStr ?? "Unknown"}\nDistance: {selected.Distance:F2}";
                    if (!string.IsNullOrEmpty(selected.CalcDistance))
                    {
                        info += $"\nDistance from Reference: {selected.CalcDistance}";
                    }
                    Clipboard.SetText(info);
                    StatusLabel.Text = "Entity info copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Copy failed: {ex.Message}";
            }
        }

        #endregion
    }
}
