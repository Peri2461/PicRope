using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenLine
{
    internal sealed class SaveChangesDialog : Window
    {
        internal MessageBoxResult Choice { get; private set; }

        public SaveChangesDialog(Window owner, string path)
        {
            Owner = owner;
            Title = "保存更改";
            AutomationProperties.SetAutomationId(this, "SaveChangesDialog");
            Choice = MessageBoxResult.Cancel;
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            var content = new StackPanel();
            var heading = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            heading.Children.Add(UiTheme.Paint(new TextBlock { Text = "保存这张图片的更改？", FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }, TextBlock.ForegroundProperty, "Ui.TextPrimary"));
            var close = LightTheme.Button("×", () => Choose(MessageBoxResult.Cancel), "quiet");
            close.Width = close.Height = 28; close.MinWidth = 0; close.Padding = new Thickness(0); close.FontSize = 20;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            AutomationProperties.SetName(close, "关闭");
            heading.Children.Add(close);
            content.Children.Add(heading);
            content.Children.Add(UiTheme.Paint(new TextBlock { Text = "关闭后，未保存的标注将丢失。", FontSize = 13 }, TextBlock.ForegroundProperty, "Ui.TextMuted"));
            var file = UiTheme.Paint(new Border {
                Margin = new Thickness(0, 16, 0, 22), Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(7),
                Child = UiTheme.Paint(new TextBlock { Text = Path.GetFileName(path), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = Path.GetFileName(path) }, TextBlock.ForegroundProperty, "Ui.TextSecondary")
            }, Border.BackgroundProperty, "Ui.SurfaceSubtle");
            content.Children.Add(file);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var discard = LightTheme.Button("不保存", () => Choose(MessageBoxResult.No));
            var cancel = LightTheme.Button("继续编辑", () => Choose(MessageBoxResult.Cancel));
            var save = LightTheme.Button("保存并关闭", () => Choose(MessageBoxResult.Yes), "primary");
            cancel.Margin = save.Margin = new Thickness(8, 0, 0, 0);
            cancel.IsCancel = true; save.IsDefault = true;
            actions.Children.Add(discard); actions.Children.Add(cancel); actions.Children.Add(save);
            content.Children.Add(actions);
            Content = LightTheme.Card(content);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Loaded += delegate { save.Focus(); };
        }

        void Choose(MessageBoxResult choice) { Choice = choice; DialogResult = true; }
    }
}
