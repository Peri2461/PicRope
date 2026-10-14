using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace ScreenLine
{
    internal static class WindowsClipboard
    {
        public static async Task Write(object data)
        {
            ExternalException last = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try { Clipboard.SetDataObject(data, true); return; }
                catch (ExternalException error) { last = error; }
                await Task.Delay(60);
            }
            throw last;
        }
    }
}
