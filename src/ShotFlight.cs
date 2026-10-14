using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace ScreenLine
{
    internal sealed class ShotFlight : Window
    {
        readonly ShotCard card;
        readonly Rect screenOrigin;
        readonly Canvas stage = new Canvas(), frame = new Canvas();
        readonly Border border = new Border();
        readonly Image photo = new Image();
        readonly ClothesClip clip = new ClothesClip();
        readonly RotateTransform turn = new RotateTransform();
        readonly DropShadowEffect shadow = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4 };
        readonly Stopwatch clock = new Stopwatch();
        readonly Action landed;
        Rect origin;
        bool completed;
        public bool Falling { get; private set; }

        public ShotFlight(Forms.Screen screen, ShotCard card, Rect origin, bool falling, Action landed, BitmapSource image = null)
        {
            this.card = card;
            ApplyTheme(card.Appearance);
            screenOrigin = origin;
            this.landed = landed;
            Falling = falling;
            double scale = WindowsNative.MonitorScale(screen);
            Width = screen.Bounds.Width / scale;
            Height = screen.Bounds.Height / scale;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            IsHitTestVisible = false;
            Content = stage;
            stage.Children.Add(frame);
            frame.RenderTransform = turn;
            photo.Source = image ?? card.Item.Image.Thumbnail;
            photo.Stretch = Stretch.Fill;
            border.Child = photo;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 225, 233));
            border.Effect = shadow;
            frame.Children.Add(border);
            frame.Children.Add(clip);
            SourceInitialized += delegate
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                WindowsNative.PrepareOverlay(handle);
                WindowsNative.PassMouse(handle, true);
                WindowsNative.SetWindowPos(handle, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, 0x10);
                this.origin = Local(screenOrigin);
                clock.Start();
                Render(null, EventArgs.Empty);
                CompositionTarget.Rendering += Render;
            };
            Closed += delegate
            {
                CompositionTarget.Rendering -= Render;
                Complete();
            };
        }

        Rect Local(Rect rectangle)
        {
            return new Rect(PointFromScreen(rectangle.TopLeft), PointFromScreen(rectangle.BottomRight));
        }

        public void ApplyTheme(LineTheme theme) { clip.Apply(theme, card.ColorIndex); }

        void Complete()
        {
            if (completed) return;
            completed = true;
            landed();
        }

        void Render(object sender, EventArgs args)
        {
            if (!Falling && card.IsDisposed) { Close(); return; }
            double seconds = clock.Elapsed.TotalSeconds;
            double progress = Math.Min(1, seconds / (Falling ? 0.55 : 0.65));
            // 五次曲线让起飞与抵达的速度、加速度都平滑归零。
            double ease = progress * progress * progress * (10 + progress * (-15 + 6 * progress));
            Rect target = Falling ? origin : Local(card.PhotoRect());
            double width = origin.Width + (target.Width - origin.Width) * ease;
            double height = origin.Height + (target.Height - origin.Height) * ease;
            double x = origin.X + origin.Width / 2 + (target.X + target.Width / 2 - origin.X - origin.Width / 2) * ease;
            double y = origin.Y + (target.Y - origin.Y) * ease - 30 * Math.Sin(Math.PI * ease);
            double chrome = Math.Max(0, Math.Min(1, (ease - 0.3) / 0.6));
            chrome = chrome * chrome * (3 - 2 * chrome);
            turn.Angle = card.Tilt * ease;
            if (Falling)
            {
                width = origin.Width; height = origin.Height;
                x = origin.X + width / 2;
                y = origin.Y + 520 * progress * progress;
                turn.Angle = card.Tilt + (card.Tilt < 0 ? -32 : 32) * progress * progress;
                chrome = 1;
                Opacity = 1 - progress * progress;
            }
            frame.Width = border.Width = width;
            frame.Height = border.Height = height;
            Canvas.SetLeft(frame, x - width / 2);
            Canvas.SetTop(frame, y);
            turn.CenterX = width / 2;
            turn.CenterY = -12;
            border.Background = new SolidColorBrush(Color.FromArgb((byte)(245 * chrome), 255, 255, 255));
            border.BorderThickness = new Thickness(chrome);
            border.CornerRadius = new CornerRadius(12 * (Falling ? 1 : ease));
            photo.Margin = new Thickness(6 * (Falling ? 1 : ease));
            photo.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(1, width - photo.Margin.Left * 2 - chrome * 2), Math.Max(1, height - photo.Margin.Top * 2 - chrome * 2)), 7 * (Falling ? 1 : ease), 7 * (Falling ? 1 : ease));
            shadow.Opacity = 0.2 * chrome;
            clip.Opacity = chrome;
            Canvas.SetLeft(clip, width / 2 - 8);
            Canvas.SetTop(clip, -20);
            if (progress < 1) return;
            Complete();
            if (Falling) { Close(); return; }
            Opacity = Math.Max(0, 1 - (seconds - 0.65) / 0.16);
            if (seconds >= 0.81) Close();
        }
    }
}
