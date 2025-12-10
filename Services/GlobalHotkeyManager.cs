using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TeleList.Services
{
    public class GlobalHotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;

        // Modifier flags
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly Window _window;
        private readonly IntPtr _hwnd;
        private readonly HwndSource _source;
        private readonly Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();
        private int _currentId = 0;
        private bool _disposed = false;

        public bool IsEnabled { get; set; } = true;

        public GlobalHotkeyManager(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd) ?? throw new InvalidOperationException("Failed to get HwndSource");
            _source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && IsEnabled)
            {
                int id = wParam.ToInt32();
                if (_hotkeyActions.TryGetValue(id, out var action))
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash on hotkey action errors
                        System.Diagnostics.Debug.WriteLine($"Hotkey action error: {ex.Message}");
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public int RegisterHotkey(string hotkeyString, Action action)
        {
            if (string.IsNullOrWhiteSpace(hotkeyString))
                return -1;

            var (modifiers, vk) = ParseHotkeyString(hotkeyString);
            if (vk == 0)
                return -1;

            int id = ++_currentId;

            if (RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk))
            {
                _hotkeyActions[id] = action;
                return id;
            }

            return -1;
        }

        public void UnregisterHotkey(int id)
        {
            if (id > 0 && _hotkeyActions.ContainsKey(id))
            {
                UnregisterHotKey(_hwnd, id);
                _hotkeyActions.Remove(id);
            }
        }

        public void UnregisterAllHotkeys()
        {
            foreach (var id in _hotkeyActions.Keys)
            {
                UnregisterHotKey(_hwnd, id);
            }
            _hotkeyActions.Clear();
        }

        private (uint modifiers, uint vk) ParseHotkeyString(string hotkeyString)
        {
            uint modifiers = MOD_NONE;
            uint vk = 0;

            var parts = hotkeyString.ToLower().Split('+');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                switch (trimmed)
                {
                    case "ctrl":
                    case "control":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "shift":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "alt":
                        modifiers |= MOD_ALT;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= MOD_WIN;
                        break;
                    default:
                        vk = GetVirtualKeyCode(trimmed);
                        break;
                }
            }

            return (modifiers, vk);
        }

        private uint GetVirtualKeyCode(string key)
        {
            // Handle common key names
            return key.ToLower() switch
            {
                // Arrow keys
                "left" => 0x25,
                "up" => 0x26,
                "right" => 0x27,
                "down" => 0x28,

                // Navigation keys
                "pageup" or "page up" or "prior" => 0x21,
                "pagedown" or "page down" or "next" => 0x22,
                "home" => 0x24,
                "end" => 0x23,
                "insert" => 0x2D,
                "delete" => 0x2E,

                // Function keys
                "f1" => 0x70,
                "f2" => 0x71,
                "f3" => 0x72,
                "f4" => 0x73,
                "f5" => 0x74,
                "f6" => 0x75,
                "f7" => 0x76,
                "f8" => 0x77,
                "f9" => 0x78,
                "f10" => 0x79,
                "f11" => 0x7A,
                "f12" => 0x7B,

                // Special keys
                "space" => 0x20,
                "enter" or "return" => 0x0D,
                "escape" or "esc" => 0x1B,
                "tab" => 0x09,
                "backspace" or "back" => 0x08,

                // Numpad
                "numpad0" or "num0" => 0x60,
                "numpad1" or "num1" => 0x61,
                "numpad2" or "num2" => 0x62,
                "numpad3" or "num3" => 0x63,
                "numpad4" or "num4" => 0x64,
                "numpad5" or "num5" => 0x65,
                "numpad6" or "num6" => 0x66,
                "numpad7" or "num7" => 0x67,
                "numpad8" or "num8" => 0x68,
                "numpad9" or "num9" => 0x69,
                "multiply" => 0x6A,
                "add" => 0x6B,
                "subtract" => 0x6D,
                "decimal" => 0x6E,
                "divide" => 0x6F,

                // OEM keys (keyboard-specific punctuation)
                "oem1" or "oemsemicolon" => 0xBA,      // ;:
                "oem2" or "oemquestion" => 0xBF,       // /?
                "oem3" or "oemtilde" => 0xC0,          // `~
                "oem4" or "oemopenbrackets" => 0xDB,   // [{
                "oem5" or "oempipe" => 0xDC,           // \|
                "oem6" or "oemclosebrackets" => 0xDD,  // ]}
                "oem7" or "oemquotes" => 0xDE,         // '"
                "oemplus" => 0xBB,                     // =+
                "oemcomma" => 0xBC,                    // ,<
                "oemminus" => 0xBD,                    // -_
                "oemperiod" => 0xBE,                   // .>

                // Common punctuation by symbol
                ";" or "semicolon" => 0xBA,
                "/" or "slash" => 0xBF,
                "`" or "grave" or "tilde" => 0xC0,
                "[" or "openbracket" => 0xDB,
                "\\" or "backslash" or "pipe" => 0xDC,
                "]" or "closebracket" => 0xDD,
                "'" or "quote" => 0xDE,
                "=" or "equals" or "plus" => 0xBB,
                "," or "comma" => 0xBC,
                "-" or "minus" => 0xBD,
                "." or "period" => 0xBE,

                // Letters (A-Z)
                _ when key.Length == 1 && char.IsLetter(key[0]) => (uint)(char.ToUpper(key[0])),

                // Numbers (0-9)
                _ when key.Length == 1 && char.IsDigit(key[0]) => (uint)key[0],

                _ => 0
            };
        }

        public static string KeyToString(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");

            var keyName = key switch
            {
                Key.Prior => "PageUp",
                Key.Next => "PageDown",
                Key.Return => "Enter",
                Key.Back => "Backspace",
                Key.Escape => "Escape",
                Key.Space => "Space",
                Key.Tab => "Tab",
                Key.Insert => "Insert",
                Key.Delete => "Delete",
                Key.Home => "Home",
                Key.End => "End",
                // OEM keys - output names that GetVirtualKeyCode recognizes
                Key.Oem1 => "Oem1",           // ;:
                Key.Oem2 => "Oem2",           // /?
                Key.Oem3 => "Oem3",           // `~
                Key.Oem4 => "Oem4",           // [{
                Key.Oem5 => "Oem5",           // \|
                Key.Oem6 => "Oem6",           // ]}
                Key.Oem7 => "Oem7",           // '"
                Key.OemPlus => "OemPlus",     // =+
                Key.OemComma => "OemComma",   // ,<
                Key.OemMinus => "OemMinus",   // -_
                Key.OemPeriod => "OemPeriod", // .>
                Key.Multiply => "Multiply",
                Key.Add => "Add",
                Key.Subtract => "Subtract",
                Key.Decimal => "Decimal",
                Key.Divide => "Divide",
                _ => key.ToString()
            };

            parts.Add(keyName);

            return string.Join("+", parts);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterAllHotkeys();
                _source.RemoveHook(HwndHook);
                _disposed = true;
            }
        }
    }
}
