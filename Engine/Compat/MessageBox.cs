using System;

namespace Engine
{
    internal enum MessageBoxButtons
    {
        OK
    }

    internal enum MessageBoxIcon
    {
        None,
        Error
    }

    /// <summary>
    /// Cross-platform replacement for System.Windows.Forms.MessageBox.
    /// Writes the message to log and stderr.
    /// </summary>
    internal static class MessageBox
    {
        public static void Show(string message)
        {
            Write(message);
        }

        public static void Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Write(caption + ": " + message);
        }

        private static void Write(string message)
        {
            try
            {
                Log.LogMessage("[MessageBox] " + message);
            }
            catch (Exception)
            {
                // ignored
            }
            try
            {
                Console.Error.WriteLine("[MessageBox] " + message);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
