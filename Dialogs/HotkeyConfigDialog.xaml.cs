using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TeleList.Services;

namespace TeleList.Dialogs
{
    public partial class HotkeyConfigDialog : Window, INotifyPropertyChanged
    {
        private string _keyDisplay = "Waiting for key...";
        private string _instructionText;
        private ModifierKeys _currentModifiers = ModifierKeys.None;

        public string? Result { get; private set; }

        public string KeyDisplay
        {
            get => _keyDisplay;
            set
            {
                _keyDisplay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeyDisplay)));
            }
        }

        public string InstructionText
        {
            get => _instructionText;
            set
            {
                _instructionText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstructionText)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public HotkeyConfigDialog(Window owner, string title, string currentKey)
        {
            InitializeComponent();
            DataContext = this;

            Owner = owner;
            Title = title;
            _instructionText = $"Current: {currentKey}\n\nPress any key or combination\n(e.g., Ctrl+Shift+F)";

            PreviewKeyDown += OnPreviewKeyDown;
            PreviewKeyUp += OnPreviewKeyUp;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // Track modifiers
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _currentModifiers |= ModifierKeys.Control;
                return;
            }
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                _currentModifiers |= ModifierKeys.Shift;
                return;
            }
            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                _currentModifiers |= ModifierKeys.Alt;
                return;
            }

            // Ignore modifier-only presses
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System)
            {
                return;
            }

            // Get the actual key (handle System key for Alt combinations)
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            // Build the hotkey string
            Result = GlobalHotkeyManager.KeyToString(actualKey, _currentModifiers);
            KeyDisplay = Result;

            // Close after a short delay
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                DialogResult = true;
                Close();
            };
            timer.Start();
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            // Clear modifiers on key up
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _currentModifiers &= ~ModifierKeys.Control;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                _currentModifiers &= ~ModifierKeys.Shift;
            }
            else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                _currentModifiers &= ~ModifierKeys.Alt;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }
    }
}
