using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenLine
{
    internal sealed class AppearanceWindow : Window
    {
        readonly Action<LineTheme> apply;
        readonly Action<string> applyMode;
        readonly List<TextBlock> selectionMarks = new List<TextBlock>();
        readonly Dictionary<string, Button> modeButtons = new Dictionary<string, Button>();
        internal readonly List<Button> Options = new List<Button>();
        internal string Selection { get; private set; }

        public AppearanceWindow(LineTheme selected, Gallery gallery, string mode, Action<LineTheme> apply, Action<string> applyMode)
        {
            this.apply = apply;
            this.applyMode = applyMode;
            Title = "外观";
            Width = 654; Height = 512;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; Topmost = true;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var body = new StackPanel();
            var heading = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            heading.Children.Add(UiTheme.Paint(new TextBlock { Text = "外观", FontSize = 21, FontWeight = FontWeights.SemiBold }, TextBlock.ForegroundProperty, "Ui.TextPrimary"));
            var close = LightTheme.Button("", Close);
            close.Content = LightTheme.CloseIcon(); close.Width = close.Height = 28; close.MinWidth = 0;
            close.Padding = new Thickness(0); close.Tag = "quiet"; close.HorizontalAlignment = HorizontalAlignment.Right;
            System.Windows.Automation.AutomationProperties.SetName(close, "关闭外观");
            heading.Children.Add(close);
            heading.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args) { if (args.OriginalSource is TextBlock) DragMove(); };
            body.Children.Add(heading);
            body.Children.Add(UiTheme.Paint(new TextBlock { Text = "界面深浅色与绳子外观分别切换", FontSize = 12, Margin = new Thickness(0, 0, 0, 14) }, TextBlock.ForegroundProperty, "Ui.TextMuted"));
            var modeRow = UiTheme.Paint(new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 18) }, Border.BackgroundProperty, "Ui.SurfaceSubtle");
            var modeGrid = new Grid();
            var modeText = UiTheme.Paint(new TextBlock { Text = "跟随系统，或固定使用浅色、深色", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, TextBlock.ForegroundProperty, "Ui.Accent");
            modeGrid.Children.Add(modeText);
            var pill = new Border {
                CornerRadius = new CornerRadius(14), Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
            };
            UiTheme.Paint(pill, Border.BackgroundProperty, "Ui.Surface");
            UiTheme.Paint(pill, Border.BorderBrushProperty, "Ui.Border");
            pill.BorderThickness = new Thickness(1);
            var modes = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var choice in new[] {
                new { Id = "system", Name = "跟随系统" },
                new { Id = "light", Name = "浅色" },
                new { Id = "dark", Name = "深色" } })
            {
                string id = choice.Id;
                var button = new Button { Content = choice.Name, Style = SegmentStyle, FontSize = 12 };
                button.Click += delegate { Choose(id); };
                System.Windows.Automation.AutomationProperties.SetName(button, choice.Name);
                System.Windows.Automation.AutomationProperties.SetAutomationId(button, "mode-" + id);
                modeButtons[id] = button; modes.Children.Add(button);
            }
            pill.Child = modes;
            modeGrid.Children.Add(pill);
            modeRow.Child = modeGrid;
            body.Children.Add(modeRow);
            SelectMode(mode);
            var choices = new WrapPanel();
            foreach (var theme in LineTheme.All)
            {
                var content = new StackPanel();
                content.Children.Add(Preview(theme, gallery));
                var label = new Grid { Margin = new Thickness(11, 0, 11, 5) };
                label.Children.Add(UiTheme.Paint(new TextBlock { Text = theme.Name, FontSize = 13, FontWeight = FontWeights.SemiBold }, TextBlock.ForegroundProperty, "Ui.TextPrimary"));
                var mark = UiTheme.Paint(new TextBlock { Text = "已选", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, TextBlock.ForegroundProperty, "Ui.Accent");
                selectionMarks.Add(mark); label.Children.Add(mark); content.Children.Add(label);
                content.Children.Add(UiTheme.Paint(new TextBlock { Text = theme.Description, FontSize = 10, Margin = new Thickness(11, 0, 0, 10) }, TextBlock.ForegroundProperty, "Ui.TextMuted"));
                var button = new Button { Content = content, Style = LightTheme.ButtonStyle,
                    Width = 180, Height = 139, Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 12), Tag = theme };
                System.Windows.Automation.AutomationProperties.SetName(button, theme.Name);
                System.Windows.Automation.AutomationProperties.SetAutomationId(button, "theme-" + theme.Id);
                button.Click += delegate { Select(theme); };
                Options.Add(button); choices.Children.Add(button);
            }
            body.Children.Add(choices);
            var card = LightTheme.Card(body);
            card.Padding = new Thickness(24); Content = card;
            Selected(selected);
            PreviewKeyDown += delegate(object sender, KeyEventArgs args) { if (args.Key == Key.Escape) Close(); };
        }

        internal Button ModeButton(string id) { return modeButtons[id]; }

        void Select(LineTheme theme)
        {
            apply(theme);
            Selected(theme);
        }

        void Choose(string mode)
        {
            if (Selection == mode) return;
            Selection = mode;
            SelectMode(mode);
            if (applyMode != null) applyMode(mode);
        }

        void SelectMode(string mode)
        {
            Selection = mode;
            foreach (var entry in modeButtons)
            {
                bool current = entry.Key == mode;
                entry.Value.Tag = current ? "primary" : null;
                System.Windows.Automation.AutomationProperties.SetItemStatus(entry.Value, current ? "已选择" : "");
            }
        }

        static readonly System.Windows.Style SegmentStyle = (System.Windows.Style)System.Windows.Markup.XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
  <Setter Property='Foreground' Value='{DynamicResource Ui.TextSecondary}'/>
  <Setter Property='Background' Value='Transparent'/>
  <Setter Property='Height' Value='24'/><Setter Property='Padding' Value='12,0'/>
  <Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
    <Border x:Name='Surface' CornerRadius='11' Background='{TemplateBinding Background}'>
      <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
    </Border>
    <ControlTemplate.Triggers>
      <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='{DynamicResource Ui.Hover}'/></Trigger>
      <Trigger Property='Tag' Value='primary'><Setter Property='Background' Value='{DynamicResource Ui.Accent}'/><Setter Property='Foreground' Value='{DynamicResource Ui.White}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger>
      <MultiTrigger><MultiTrigger.Conditions><Condition Property='Tag' Value='primary'/><Condition Property='IsMouseOver' Value='True'/></MultiTrigger.Conditions><Setter TargetName='Surface' Property='Background' Value='{DynamicResource Ui.AccentHover}'/></MultiTrigger>
      <Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Opacity' Value='0.88'/></Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
</Style>");

        void Selected(LineTheme theme)
        {
            for (int i = 0; i < Options.Count; i++)
            {
                var button = Options[i];
                bool current = button.Tag == theme;
                selectionMarks[i].Visibility = current ? Visibility.Visible : Visibility.Collapsed;
                UiTheme.Paint(button, Control.BorderBrushProperty, current ? "Ui.Focus" : "Ui.BorderStrong");
                button.BorderThickness = new Thickness(current ? 2 : 1);
                System.Windows.Automation.AutomationProperties.SetItemStatus(button, current ? "已选择" : "");
            }
        }

        static Canvas Preview(LineTheme theme, Gallery gallery)
        {
            var canvas = new Canvas { Width = 174, Height = 81, IsHitTestVisible = false };
            var rope = new System.Windows.Shapes.Path { Data = Geometry.Parse("M0,20 Q87,30 174,20") };
            RopeLine.Apply(rope, theme); canvas.Children.Add(rope);
            for (int i = 0; i < 3; i++)
            {
                var frame = UiTheme.Paint(new Border { Width = 44, Height = 32, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(0.7), Padding = new Thickness(2) }, Border.BackgroundProperty, "Ui.Surface");
                UiTheme.Paint(frame, Border.BorderBrushProperty, "Ui.BorderStrong");
                var image = gallery.Items.Skip(i).FirstOrDefault() ?? gallery.Items.FirstOrDefault();
                frame.Child = image == null ? (UIElement)UiTheme.Paint(new Border { CornerRadius = new CornerRadius(2) }, Border.BackgroundProperty, "Ui.SurfaceMuted")
                    : new Image { Source = image.Image.Thumbnail, Stretch = Stretch.Uniform, ClipToBounds = true };
                double x = 6 + i * 59, y = 27 + (i == 1 ? 2 : 0);
                canvas.Children.Add(frame); Canvas.SetLeft(frame, x); Canvas.SetTop(frame, y);
                var clip = new ClothesClip(theme, i) { RenderTransform = new ScaleTransform(0.42, 0.42) };
                canvas.Children.Add(clip); Canvas.SetLeft(clip, x + 22 - 8 * 0.42); Canvas.SetTop(clip, y - 8.4);
            }
            return canvas;
        }
    }
}
