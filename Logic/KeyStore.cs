﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XiboClient
{
    /// <summary>
    /// The KeyStoreEventHandler is used by the KeyPress event of the KeyStore
    /// class. It notifies listeners of a named key press.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    public delegate void KeyStoreEventHandler(string name);

    class KeyStore : IMessageFilter
    {
        // Interop
        [DllImport("user32.dll")]
        static extern short GetKeyState(Keys key);

        // Windows message constants
        private const int WM_KEYDOWN = 0x100;
        private const int WM_KEYUP = 0x101;

        // The singleton instance
        private static KeyStore s_instance = null;

        // The modifier keys
        private bool _shift = false;
        private bool _control = false;

        // The screensaver setting
        public bool ScreenSaver { get; set; }

        // The definitions
        private Dictionary<Keys, string> _definitions;

        // The KeyPressed Event
        public event KeyStoreEventHandler KeyPress;

        /// <summary>
        /// Adds a key definition to the store.
        /// </summary>
        /// <param name="name">The name of the key.</param>
        /// <param name="key">The key</param>
        /// <param name="modifiers">The modifiers (shift, control)</param>
        public void AddKeyDefinition(string name, Keys key, Keys modifiers)
        {
            Keys combined = key | modifiers;

            _definitions[combined] = name;
        }

        /// <summary>
        /// The filter message.
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            bool handled = false;
            Keys key;
            switch (m.Msg)
            {
                case WM_KEYUP:
                    key = (Keys)m.WParam;
                    handled = HandleModifier(key, false);
                    break;

                case WM_KEYDOWN:
                    key = (Keys)m.WParam;
                    handled = HandleModifier(key, true);
                    if (false == handled)
                    {
                        // If one of the defined keys was pressed then we
                        // raise an event.
                        handled = HandleDefinedKey(key);
                    }
                    break;
            }

            return handled;
        }

        /// <summary>
        /// Handle Raw Keypresses
        /// </summary>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        public void HandleRawKey(IntPtr wParam, IntPtr lParam)
        {
            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                Keys key = (Keys)Marshal.ReadInt32(lParam);
                HandleModifier(key, true);
            }
            else if (wParam == (IntPtr)WM_KEYUP)
            {
                Keys key = (Keys)Marshal.ReadInt32(lParam);
                if (HandleModifier(key, false) == false)
                {
                    // If one of the defined keys was pressed then we
                    // raise an event.
                    HandleDefinedKey(key);
                }
            }
        }

        /// <summary>
        /// Compares a key against the definitions, and raises an event
        /// if there is a match.
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>True if the key was one of the defined key combinations.</returns>
        private bool HandleDefinedKey(Keys key)
        {
            bool handled = false;

            Keys combined = key;
            if (_shift) combined |= Keys.Shift;
            if (_control) combined |= Keys.Control; 
            
            Debug.WriteLine(key.ToString() + "shift: " + _shift + ", control: " + _control, "KeyStore");

            // If we have found a matching combination then we
            // raise an event.
            if (true == _definitions.TryGetValue(combined, out string name))
            {
                OnKeyPress(name);

                handled = true;
            }
            else if (ScreenSaver)
            {
                OnKeyPress("ScreenSaver");
                handled = true;
            }

            Debug.WriteLine("HandleDefinedKey: " + key.ToString() + ", handled: " + handled, "KeyStore");
            return handled;
        }

        /// <summary>
        /// Attempt to handle a modifier key, and return a boolean indicating if a modifier key was
        /// handled.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="isDown">True if the key is pressed; False if it is released.</param>
        /// <returns>True if a modifier key was selected; False otherwise.</returns>
        private bool HandleModifier(Keys key, bool isDown)
        {
            bool handled = false;

            switch (key)
            {
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.ControlKey:
                    _control = isDown;
                    handled = true;
                    break;

                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.ShiftKey:
                    _shift = isDown;
                    handled = true;
                    break;
            }

            Debug.WriteLine("HandleModifier: " + key.ToString() + " isDown: " + isDown + ", handled: " + handled 
                + ", shift: " + _shift + ", control: " + _control, "KeyStore");

            return handled;
        }

        /// <summary>
        /// Raises the KeyPress event.
        /// </summary>
        /// <param name="name">The name of the key.</param>
        private void OnKeyPress(string name)
        {
            // Raise event
            KeyPress?.Invoke(name);

            // Check if modifier keys were released in the mean time.
            _control =
                -127 == GetKeyState(Keys.ControlKey) ||
                -127 == GetKeyState(Keys.LControlKey) ||
                -127 == GetKeyState(Keys.RControlKey);

            _shift =
                -127 == GetKeyState(Keys.ShiftKey) ||
                -127 == GetKeyState(Keys.LShiftKey) ||
                -127 == GetKeyState(Keys.RShiftKey);

        }

        /// <summary>
        /// Returns the singleton instance.
        /// </summary>
        public static KeyStore Instance
        {
            get
            {
                if (null == s_instance)
                    s_instance = new KeyStore();

                return s_instance;
            }
        }

        // The constructor is private because this is a singleton class.
        private KeyStore()
        {
            _definitions = new Dictionary<Keys, string>();
        }
    }
}
