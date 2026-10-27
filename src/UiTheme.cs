using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ScreenLine
{
    internal static class UiTheme
    {
        static readonly Dictionary<string, string[]> tones = new Dictionary<string, string[]>
        {
            { "Ui.Surface", new[] { "#FFFFFF", "#242624" } },
            { "Ui.SurfaceRaised", new[] { "#FFFFFF", "#2A2C2A" } },
            { "Ui.SurfaceGlass", new[] { "#F8FFFFFF", "#F2242624" } },
            { "Ui.SurfaceMuted", new[] { "#F1F4F8", "#191B1A" } },
            { "Ui.SurfaceSubtle", new[] { "#F5F7FA", "#202220" } },
            { "Ui.Border", new[] { "#E0E4EA", "#353836" } },
            { "Ui.BorderStrong", new[] { "#DCE2EA", "#3C403D" } },
            { "Ui.Divider", new[] { "#E8EBEF", "#2D302D" } },
            { "Ui.TextPrimary", new[] { "#283343", "#E7EBF0" } },
            { "Ui.TextBody", new[] { "#343D49", "#D3D9E1" } },
            { "Ui.TextSecondary", new[] { "#536172", "#A7B0BC" } },
            { "Ui.TextMuted", new[] { "#65717F", "#8B96A4" } },
            { "Ui.TextFaint", new[] { "#8390A1", "#6F7A88" } },
            { "Ui.Accent", new[] { "#2463AE", "#5A9BD8" } },
            { "Ui.AccentHover", new[] { "#1B5498", "#6FAFE8" } },
            { "Ui.AccentSoft", new[] { "#EDF4FF", "#1B3047" } },
            { "Ui.AccentSoftHover", new[] { "#E0ECFF", "#23405C" } },
            { "Ui.AccentText", new[] { "#20408E", "#9CC4F0" } },
            { "Ui.Focus", new[] { "#6E97CB", "#6FA8E0" } },
            { "Ui.Hover", new[] { "#F0F3F7", "#2C2F2C" } },
            { "Ui.HoverAccent", new[] { "#E7EEF8", "#24384D" } },
            { "Ui.Pressed", new[] { "#E8EDF3", "#363936" } },
            { "Ui.Danger", new[] { "#BB514E", "#E0807C" } },
            { "Ui.DangerStrong", new[] { "#B74E4A", "#E0807C" } },
            { "Ui.DangerSoft", new[] { "#FFF0ED", "#3A2322" } },
            { "Ui.DangerSurface", new[] { "#FFF8F7", "#2B1E1D" } },
            { "Ui.DangerBorder", new[] { "#ECD6D2", "#5A3A37" } },
            { "Ui.SwitchOff", new[] { "#B8C2D0", "#454A45" } },
            { "Ui.White", new[] { "#FFFFFF", "#FFFFFF" } },
            { "Ui.ScrollThumb", new[] { "#C9D2DC", "#454A45" } },
            { "Ui.ScrollThumbHover", new[] { "#AEBBC9", "#565C56" } },
            { "Ui.EditorBackground", new[] { "#F4F6F9", "#191B1A" } },
            { "Ui.EditorCanvas", new[] { "#F0F1F3", "#141615" } }
        };

        static bool installed;

        public static bool Dark { get; private set; }
        public static event Action Changed;

        public static bool SystemDark
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object light = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return light is int && (int)light == 0;
                }
            }
        }

        public static bool Resolve(string mode)
        {
            if (mode == "dark") return true;
            if (mode == "light") return false;
            return SystemDark;
        }

        public static void Install(Application app)
        {
            if (installed) return;
            installed = true;
            Fill(app.Resources);
        }

        public static void Apply(bool dark)
        {
            Dark = dark;
            if (Application.Current != null) Fill(Application.Current.Resources);
            var changed = Changed;
            if (changed != null) changed();
        }

        public static T Paint<T>(T element, DependencyProperty property, string key) where T : FrameworkElement
        {
            element.SetResourceReference(property, key);
            return element;
        }

        public static Color MediaColor(string key)
        {
            return ((SolidColorBrush)Application.Current.Resources[key]).Color;
        }

        static void Fill(ResourceDictionary resources)
        {
            foreach (var entry in tones)
                resources[entry.Key] = new SolidColorBrush(Parse(entry.Value[Dark ? 1 : 0]));
        }

        static Color Parse(string value) { return (Color)ColorConverter.ConvertFromString(value); }
    }
}
