using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ScreenLine
{
    internal enum ShotAction { Copy, Open, Edit, Remove, Trash, Save, Locate }

    internal sealed class ShelfWindow : Window
    {
        readonly Gallery gallery;
        readonly Action<ShotAction, CaptureItem> act;
        readonly ScrollViewer viewport = new ScrollViewer();
        readonly Canvas content = new Canvas();
        readonly System.Windows.Shapes.Path rope = new System.Windows.Shapes.Path();
        readonly Dictionary<string, ShotCard> cards = new Dictionary<string, ShotCard>(StringComparer.OrdinalIgnoreCase);
        readonly Border hint = new Border();
        readonly Button enable = new Button();
        readonly TranslateTransform slide = new TranslateTransform(0, -240);
        readonly SpringMotion revealMotion = new SpringMotion(0.42, 0.82, -240);
        readonly SpringMotion ropeMotion = new SpringMotion(0.55, 0.78, 18);
        readonly SpringMotion scrollMotion = new SpringMotion(0.55, 0.78, 0);
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random random = new Random();
        readonly List<ShotFlight> flights = new List<ShotFlight>();
        readonly Dictionary<string, PinnedImageWindow> pins = new Dictionary<string, PinnedImageWindow>(StringComparer.OrdinalIgnoreCase);
        bool closing;
        readonly DispatcherTimer pointer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        DateTime edgeSince = DateTime.MaxValue, awaySince = DateTime.MaxValue, peekUntil;
        DateTime nextGust;
        double lastFrame, hideTime, hideFrom, drawnWidth = -1, drawnSag = -1;
        double lastScrollOffset;
        bool rendering;
        bool pinned, previousDown, suppressEdge;
        string viewingName;
        IntPtr viewingWindow;
        DateTime viewerSearchUntil;
        public bool Revealed { get; private set; }
        public IntPtr Handle { get; private set; }
        public Forms.Screen Display { get; private set; }
        public LineTheme Appearance { get; private set; }
        internal bool ChoosingAppearance { get; set; }

        public ShelfWindow(Gallery gallery, Action<ShotAction, CaptureItem> act, Action enableClipboard)
        {
            this.gallery = gallery;
            this.act = act;
            Title = "PicRope · 图绳";
            Width = 1000;
            Height = 220;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            viewport.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            viewport.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            viewport.Content = content;
            viewport.Margin = new Thickness(24, 0, 24, 0);
            viewport.RenderTransform = slide;
            viewport.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                double target = Math.Max(0, Math.Min(viewport.ScrollableWidth, scrollMotion.Target - e.Delta));
                if (Math.Abs(target - scrollMotion.Target) > 0.01)
                {
                    scrollMotion.Target = target;
                    nextGust = DateTime.UtcNow.AddSeconds(7 + random.NextDouble() * 17);
                }
                e.Handled = true;
            };
            Content = viewport;
            Appearance = LineTheme.FromId(gallery.Store.State.Theme);
            RopeLine.Apply(rope, Appearance);
            content.Children.Add(rope);

            var help = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };
            help.Children.Add(new TextBlock { Text = "截图会挂在这里", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center });
            help.Children.Add(new TextBlock { Text = "Win + PrtScn · 或开启剪贴板截图收集", FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 12), HorizontalAlignment = HorizontalAlignment.Center });
            enable.Content = "开启剪贴板收集";
            enable.Style = LightTheme.ButtonStyle;
            enable.Tag = "primary";
            enable.Padding = new Thickness(14, 5, 14, 5);
            enable.HorizontalAlignment = HorizontalAlignment.Center;
            enable.Focusable = false;
            enable.Click += delegate { enableClipboard(); Update(); };
            help.Children.Add(enable);
            hint.Child = help;
            hint.Width = 380;
            hint.Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255));
            hint.CornerRadius = new CornerRadius(16);
            hint.BorderBrush = new SolidColorBrush(Color.FromRgb(218, 224, 232));
            hint.BorderThickness = new Thickness(1);
            hint.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.18 };
            content.Children.Add(hint);

            SourceInitialized += delegate
            {
                Handle = new WindowInteropHelper(this).Handle;
                WindowsNative.PrepareOverlay(Handle);
                WindowsNative.PassMouse(Handle, true);
                HwndSource.FromHwnd(Handle).AddHook(Messages);
                MoveTo(WindowsNative.ScreenAtPointer());
                pointer.Start();
            };
            SizeChanged += delegate { LayoutCards(); };
            pointer.Tick += Tick;
            gallery.Changed += Update;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
            Closed += delegate
            {
                closing = true;
                foreach (var pin in pins.Values.ToArray()) pin.Close();
                pointer.Stop();
                StopRendering();
                foreach (var flight in flights.ToArray()) flight.Close();
                gallery.Changed -= Update;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
                foreach (var card in cards.Values) card.Dispose();
            };
            Update();
        }

        IntPtr Messages(IntPtr window, int message, IntPtr w, IntPtr l, ref bool handled)
        {
            if (message == 0x21) { handled = true; return new IntPtr(3); }
            if (message == 0x02E0) Dispatcher.BeginInvoke(new Action(delegate { if (Display != null) MoveTo(Display); }));
            return IntPtr.Zero;
        }

        void DisplaysChanged(object sender, EventArgs args)
        {
            Dispatcher.BeginInvoke(new Action(delegate { MoveTo(WindowsNative.ScreenAtPointer()); }));
        }

        public void MoveTo(Forms.Screen screen)
        {
            Display = screen;
            double scale = WindowsNative.MonitorScale(screen);
            Width = screen.WorkingArea.Width / scale;
            if (Handle != IntPtr.Zero)
                WindowsNative.SetWindowPos(Handle, new IntPtr(-1), screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, (int)Math.Round(Height * scale), 0x10);
            LayoutCards();
        }

        public void Update()
        {
            var retained = new HashSet<string>(gallery.Items.Select(i => i.Path), StringComparer.OrdinalIgnoreCase);
            foreach (string gone in cards.Keys.Where(k => !retained.Contains(k)).ToArray())
            {
                cards[gone].Dispose();
                PinnedImageWindow pin;
                if (pins.TryGetValue(gone, out pin)) pin.Close();
                content.Children.Remove(cards[gone]);
                cards.Remove(gone);
            }
            foreach (var item in gallery.Items)
            {
                ShotCard card;
                if (!cards.TryGetValue(item.Path, out card))
                {
                    card = new ShotCard(item, FileRules.IsInside(item.Path, gallery.Store.Inbox), act);
                    card.ApplyTheme(Appearance, gallery.Items.IndexOf(item));
                    var mounted = card;
                    card.Grip.Click += async delegate { await ReleasePhoto(mounted); };
                    cards.Add(item.Path, card);
                    content.Children.Add(card);
                }
                bool changed = card.UpdateImage();
                PinnedImageWindow pinnedImage;
                if (changed && pins.TryGetValue(item.Path, out pinnedImage)) pinnedImage.UpdateImage();
            }
            hint.Visibility = gallery.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            enable.Visibility = gallery.Store.State.CollectClipboard ? Visibility.Collapsed : Visibility.Visible;
            LayoutCards();
        }

        void LayoutCards()
        {
            double available = Math.Max(200, (ActualWidth > 0 ? ActualWidth : Width) - 48);
            var hanging = gallery.Items.Where(item => !cards[item.Path].Detached).ToArray();
            content.Width = Math.Max(available, hanging.Length * 172 + 32);
            content.Height = 220;
            ropeMotion.Target = 18 + Math.Min(7, hanging.Length) * 1.2;
            if (!Revealed) ropeMotion.Snap(ropeMotion.Target);
            DrawRope();
            Canvas.SetLeft(hint, (content.Width - hint.Width) / 2);
            Canvas.SetTop(hint, 56);
            for (int i = 0; i < hanging.Length; i++)
            {
                ShotCard card = cards[hanging[i].Path];
                double x = (content.Width - hanging.Length * 172) / 2 + i * 172;
                card.Place(x, !Revealed);
                card.Frame(0, ropeMotion.Position, content.Width);
            }
        }

        public void ApplyTheme(LineTheme theme)
        {
            Appearance = theme;
            RopeLine.Apply(rope, theme);
            for (int i = 0; i < gallery.Items.Count; i++) cards[gallery.Items[i].Path].ApplyTheme(theme, i);
            foreach (var flight in flights) flight.ApplyTheme(theme);
            foreach (var entry in pins) entry.Value.ApplyTheme(theme, cards[entry.Key].ColorIndex);
        }

        void DrawRope()
        {
            if (Math.Abs(drawnWidth - content.Width) < 0.01 && Math.Abs(drawnSag - ropeMotion.Position) < 0.01) return;
            drawnWidth = content.Width;
            drawnSag = ropeMotion.Position;
            var geometry = new PathGeometry();
            var shape = new PathFigure { StartPoint = new Point(0, 18) };
            shape.Segments.Add(new QuadraticBezierSegment(new Point(content.Width / 2, 18 + 2 * drawnSag), new Point(content.Width, 18), true));
            geometry.Figures.Add(shape);
            rope.Data = geometry;
        }

        public void Toggle()
        {
            viewingName = null;
            viewingWindow = IntPtr.Zero;
            if (Revealed) Reveal(false, false, 0);
            else { MoveTo(WindowsNative.ScreenAtPointer()); Reveal(true, true, 0); }
        }

        public void FollowImageViewer(string path)
        {
            viewingName = System.IO.Path.GetFileName(path);
            viewingWindow = IntPtr.Zero;
            viewerSearchUntil = DateTime.UtcNow.AddSeconds(5);
            awaySince = DateTime.MaxValue;
        }

        bool ViewingImage(DateTime now)
        {
            if (viewingName == null) return false;
            IntPtr foreground = WindowsNative.GetForegroundWindow();
            if (viewingWindow == IntPtr.Zero)
            {
                if (foreground != Handle && WindowsNative.WindowTitle(foreground).IndexOf(viewingName, StringComparison.OrdinalIgnoreCase) >= 0)
                    viewingWindow = foreground;
                else if (now < viewerSearchUntil) return true;
                else { viewingName = null; return false; }
            }
            if (!WindowsNative.IsWindowVisible(viewingWindow))
            {
                viewingName = null;
                viewingWindow = IntPtr.Zero;
                Reveal(true, false, 0);
                return true;
            }
            return foreground == viewingWindow;
        }

        public void ShowNewCapture(CaptureItem item, Point? origin)
        {
            MoveTo(WindowsNative.ScreenAtPointer());
            Reveal(true, false, 2);
            scrollMotion.Target = Math.Max(0, content.Width - viewport.ViewportWidth);
            ropeMotion.Velocity += 8;
            if (!Revealed || !origin.HasValue || flights.Count(f => !f.Falling) >= 2) return;
            ShotCard card = cards[item.Path];
            if (card.Detached) return;
            card.BeginFlight();
            var bounds = Display.Bounds;
            double scale = Math.Min(1, Math.Min(bounds.Width * 0.7 / item.Image.PixelWidth, bounds.Height * 0.7 / item.Image.PixelHeight));
            double width = item.Image.PixelWidth * scale, height = item.Image.PixelHeight * scale;
            double left = Math.Max(bounds.Left, Math.Min(bounds.Right - width, origin.Value.X - width));
            double top = Math.Max(bounds.Top, Math.Min(bounds.Bottom - height, origin.Value.Y - height));
            Rect start = new Rect(left, top, width, height);
            if (item.Image.PixelWidth == bounds.Width && item.Image.PixelHeight == bounds.Height)
                start = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            PlayFlight(new ShotFlight(Display, card, start, false, card.Land));
        }

        public void ShowDiscard(CaptureItem item)
        {
            ShotCard card;
            if (!Revealed || !cards.TryGetValue(item.Path, out card) || card.Flying) return;
            PlayFlight(new ShotFlight(Display, card, card.PhotoRect(), true, delegate { }));
        }

        public void ShowEditedImage(string path, Rect origin, BitmapSource picture)
        {
            ShotCard card;
            if (closing || !cards.TryGetValue(path, out card) || card.Detached || card.Flying) return;
            MoveTo(Forms.Screen.FromPoint(new System.Drawing.Point((int)(origin.X + origin.Width / 2), (int)(origin.Y + origin.Height / 2))));
            Reveal(true, false, 2);
            if (!Revealed) return;
            card.BeginFlight();
            LayoutCards();
            viewport.UpdateLayout();
            scrollMotion.Target = Math.Max(0, Math.Min(content.Width - viewport.ViewportWidth, card.MountX + 82 - viewport.ViewportWidth / 2));
            PlayFlight(new ShotFlight(Display, card, origin, false, delegate { card.Land(); ropeMotion.Velocity += 12; }, picture));
        }

        void PlayFlight(ShotFlight flight)
        {
            flights.Add(flight);
            flight.Closed += delegate { flights.Remove(flight); };
            flight.Show();
        }

        async Task ReleasePhoto(ShotCard card)
        {
            if (card.Releasing || card.Flying || card.Detached) return;
            Rect origin = card.PhotoRect();
            card.OpenClip();
            awaySince = DateTime.MaxValue;
            try
            {
                var picture = Task.Run(() => PinnedImageWindow.ReadPicture(card.Item.Path));
                await Task.Delay(40);
                var image = await picture;
                if (closing || card.IsDisposed) return;
                var window = new PinnedImageWindow(card.Item, origin, ((RotateTransform)card.RenderTransform).Angle, image, Appearance, card.ColorIndex);
                pins.Add(card.Item.Path, window);
                window.Closed += delegate
                {
                    pins.Remove(card.Item.Path);
                    if (closing || card.IsDisposed) return;
                    card.Detached = false;
                    card.ResetClip();
                    card.BeginFlight();
                    var bounds = window.ReturnBounds;
                    MoveTo(Forms.Screen.FromPoint(new System.Drawing.Point((int)(bounds.X + bounds.Width / 2), (int)(bounds.Y + bounds.Height / 2))));
                    Reveal(true, false, 3);
                    LayoutCards();
                    viewport.UpdateLayout();
                    scrollMotion.Target = Math.Max(0, Math.Min(content.Width - viewport.ViewportWidth, card.MountX + 82 - viewport.ViewportWidth / 2));
                    PlayFlight(new ShotFlight(Display, card, bounds, false, card.Land, window.Picture));
                };
                card.DetachPhoto();
                ropeMotion.Velocity -= 24;
                LayoutCards();
                window.Show();
            }
            catch (Exception error) { gallery.Store.Log(error); card.ResetClip(); }
        }

        public void Reveal(bool shown, bool pin, double seconds)
        {
            pinned = pin;
            peekUntil = DateTime.UtcNow.AddSeconds(seconds);
            awaySince = DateTime.MaxValue;
            if (shown && WindowsNative.FullScreen(Display, Handle)) return;
            if (shown == Revealed) return;
            Revealed = shown;
            revealMotion.Target = shown ? 0 : -240;
            hideFrom = revealMotion.Position;
            hideTime = 0;
            StartRendering();
            if (!shown)
            {
                foreach (var flight in flights.ToArray()) flight.Close();
                WindowsNative.PassMouse(Handle, true);
            }
            else
            {
                if (nextGust == default(DateTime)) nextGust = DateTime.UtcNow.AddSeconds(7 + random.NextDouble() * 17);
                foreach (var card in cards.Values) card.Nudge(1.2);
            }
        }

        void StartRendering()
        {
            if (rendering) return;
            rendering = true;
            lastFrame = clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += RenderFrame;
        }

        void StopRendering()
        {
            if (!rendering) return;
            rendering = false;
            CompositionTarget.Rendering -= RenderFrame;
        }

        void RenderFrame(object sender, EventArgs args)
        {
            double now = clock.Elapsed.TotalSeconds;
            double elapsed = now - lastFrame;
            lastFrame = now;
            if (Revealed) revealMotion.Step(elapsed);
            else
            {
                hideTime += elapsed;
                double t = Math.Min(1, hideTime / 0.22);
                revealMotion.Position = hideFrom + (-240 - hideFrom) * t * t;
                revealMotion.Velocity = 2 * (-240 - hideFrom) * t / 0.22;
                if (t == 1) revealMotion.Snap(-240);
            }
            slide.Y = revealMotion.Position;
            ropeMotion.Step(elapsed);
            DrawRope();
            scrollMotion.Target = Math.Min(scrollMotion.Target, viewport.ScrollableWidth);
            scrollMotion.Step(elapsed);
            viewport.ScrollToHorizontalOffset(scrollMotion.Position);
            double scrollVelocity = Revealed && elapsed > 0 ? (viewport.HorizontalOffset - lastScrollOffset) / elapsed : 0;
            lastScrollOffset = viewport.HorizontalOffset;
            foreach (var card in cards.Values)
            {
                card.ScrollWith(scrollVelocity);
                card.Frame(elapsed, ropeMotion.Position, content.Width);
            }
            if (!Revealed && hideTime >= 0.22) StopRendering();
        }

        bool OverControl(WindowsNative.Point cursor)
        {
            Point local = PointFromScreen(new Point(cursor.X, cursor.Y));
            IEnumerable<FrameworkElement> targets = cards.Values.Where(c => !c.Flying && !c.Detached).SelectMany(c => new FrameworkElement[] { c.Surface, c.Grip });
            if (hint.IsVisible) targets = targets.Concat(new[] { hint });
            foreach (var target in targets)
            {
                var bounds = target.TransformToAncestor(this).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
                if (bounds.Contains(local)) return true;
            }
            return false;
        }

        void Tick(object sender, EventArgs args)
        {
            var cursor = WindowsNative.Cursor();
            var screen = WindowsNative.ScreenAtPointer();
            bool down = (WindowsNative.GetAsyncKeyState(1) & 0x8000) != 0;
            bool nearTop = cursor.Y >= screen.Bounds.Top && cursor.Y <= Math.Max(screen.Bounds.Top + 2, screen.WorkingArea.Top - 1);
            bool over = Revealed && OverControl(cursor);
            var now = DateTime.UtcNow;
            bool viewing = ViewingImage(now);
            if (!nearTop) suppressEdge = false;
            if (nearTop && down && !previousDown && !over && !viewing)
            {
                suppressEdge = true;
                viewingName = null;
                viewingWindow = IntPtr.Zero;
                Reveal(false, false, 0);
            }
            previousDown = down;
            if (nearTop && !down && !suppressEdge && !WindowsNative.FullScreen(screen, Handle))
            {
                if (edgeSince == DateTime.MaxValue) edgeSince = now;
                if (now - edgeSince >= TimeSpan.FromMilliseconds(320) && (!Revealed || screen.DeviceName != Display.DeviceName))
                {
                    MoveTo(screen);
                    Reveal(true, false, 0);
                }
            }
            else edgeSince = DateTime.MaxValue;
            if (WindowsNative.FullScreen(Display, Handle)) { Reveal(false, false, 0); return; }
            bool busy = cards.Values.Any(c => c.Busy);
            WindowsNative.PassMouse(Handle, !Revealed || (!over && !busy));
            if (!Revealed) return;
            if (cards.Count > 0 && Math.Abs(scrollMotion.Velocity) < 8 && Math.Abs(scrollMotion.Target - viewport.HorizontalOffset) < 0.5 && !busy && now >= nextGust)
            {
                nextGust = now.AddSeconds(7 + random.NextDouble() * 17);
                int direction = random.Next(2) == 0 ? -1 : 1;
                foreach (var card in cards.Values) card.Breeze(random.NextDouble() * 0.35, direction * (1.6 + random.NextDouble() * 1.8));
                StartRendering();
            }
            WindowsNative.Rectangle bounds;
            WindowsNative.GetWindowRect(Handle, out bounds);
            bool inside = cursor.X >= bounds.Left && cursor.X < bounds.Right && cursor.Y >= Display.Bounds.Top && cursor.Y < bounds.Bottom;
            if (inside) pinned = false;
            if (inside || pinned || busy || viewing || ChoosingAppearance || now < peekUntil) awaySince = DateTime.MaxValue;
            else
            {
                if (awaySince == DateTime.MaxValue) awaySince = now;
                if (now - awaySince > TimeSpan.FromMilliseconds(1200)) Reveal(false, false, 0);
            }
        }

        public void ShowCopied(string path) { if (cards.ContainsKey(path)) cards[path].FlashCopied(); }
        internal ShotCard CardFor(string path) { return cards[path]; }
        internal IEnumerable<ShotFlight> MotionWindows { get { return flights; } }
        internal IEnumerable<PinnedImageWindow> PinWindows { get { return pins.Values; } }
        internal bool Animating { get { return rendering; } }
    }

    internal sealed class ShotCard : Canvas, IDisposable
    {
        public readonly CaptureItem Item;
        public readonly Border Surface = new Border();
        internal readonly Button Grip = new Button();
        readonly TranslateTransform clipLift = new TranslateTransform();
        readonly ScaleTransform clipScale = new ScaleTransform(1, 1);
        readonly SpringMotion clipMotion = new SpringMotion(0.25, 0.8, 0);
        readonly ClothesClip clip = new ClothesClip();
        readonly Image photo = new Image();
        readonly Button close = new Button();
        readonly Border copied = new Border();
        readonly RotateTransform tilt = new RotateTransform(0, 82, 8);
        Point position;
        readonly ScaleTransform photoScale = new ScaleTransform(1, 1);
        readonly SpringMotion xMotion = new SpringMotion(0.55, 0.78, 0);
        readonly SpringMotion drop = new SpringMotion(0.42, 0.72, -46) { Target = 0 };
        readonly SpringMotion swing = new SpringMotion(2 * Math.PI / Math.Sqrt(46), 2.6 / (2 * Math.Sqrt(46)), 16) { Target = 0 };
        readonly SpringMotion scaleMotion = new SpringMotion(0.3, 0.6, 1);
        readonly DispatcherTimer hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(480) };
        readonly DispatcherTimer flash = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        readonly Action<ShotAction, CaptureItem> act;
        readonly LightMenu menu;
        internal LightMenu ActionMenu { get { return menu; } }
        Point start;
        bool pressed, held, dragging, placed, hovering, gripHover;
        double entryTime, breezeDelay = -1, breezeAngle;
        ImageContent displayed;
        public bool Busy { get { return pressed || dragging || Releasing || menu.IsOpen; } }
        public bool Flying { get; private set; }
        public bool Detached { get; set; }
        public bool Releasing { get; private set; }
        public bool IsDisposed { get; private set; }
        public readonly double Tilt;
        public LineTheme Appearance { get; private set; }
        public int ColorIndex { get; private set; }
        internal double MountX { get { return xMotion.Target; } }

        public ShotCard(CaptureItem item, bool managed, Action<ShotAction, CaptureItem> act)
        {
            Item = item;
            Appearance = LineTheme.All[0];
            this.act = act;
            Width = 164;
            Height = 178;
            RenderTransform = tilt;
            Tilt = (item.Path.GetHashCode() & 7) * 0.7 - 2.45;
            Opacity = 0;
            Surface.RenderTransform = photoScale;
            Surface.RenderTransformOrigin = new Point(0.5, 0);
            Surface.Background = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255));
            Surface.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 225, 233));
            Surface.BorderThickness = new Thickness(1);
            Surface.CornerRadius = new CornerRadius(12);
            Surface.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.2 };
            var imageLayer = new Grid();
            photo.Stretch = Stretch.Uniform;
            photo.Margin = new Thickness(6);
            imageLayer.Children.Add(photo);
            close.Content = LightTheme.CloseIcon();
            close.Style = LightTheme.ButtonStyle;
            close.MinWidth = 0;
            close.Width = close.Height = 22;
            close.Padding = new Thickness(0);
            close.Margin = new Thickness(3);
            close.HorizontalAlignment = HorizontalAlignment.Left;
            close.VerticalAlignment = VerticalAlignment.Top;
            close.Focusable = false;
            close.Opacity = 0;
            close.ToolTip = managed ? "丢弃到回收站" : "从绳子上取下，保留原文件";
            close.Click += delegate { act(ShotAction.Remove, item); };
            imageLayer.Children.Add(close);
            Surface.Child = imageLayer;
            Children.Add(Surface);
            var clipTransform = new TransformGroup();
            clipTransform.Children.Add(clipScale); clipTransform.Children.Add(clipLift);
            clip.RenderTransform = clipTransform;
            clip.RenderTransformOrigin = new Point(0.5, 0.5);
            clip.HorizontalAlignment = HorizontalAlignment.Center;
            clip.VerticalAlignment = VerticalAlignment.Top;
            clip.Margin = new Thickness(0, 4, 0, 0);
            Grip.Width = 28; Grip.Height = 34;
            Grip.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Top'/></Border></ControlTemplate>");
            Grip.Focusable = false;
            Grip.Cursor = Cursors.Hand;
            Grip.ToolTip = new ToolTip { Content = "贴到桌面", Style = LightTheme.HintStyle,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right, HorizontalOffset = 6 };
            ToolTipService.SetInitialShowDelay(Grip, 800);
            ToolTipService.SetBetweenShowDelay(Grip, 0);
            ToolTipService.SetShowDuration(Grip, 2500);
            System.Windows.Automation.AutomationProperties.SetName(Grip, "贴到桌面");
            Grip.Content = clip;
            Grip.MouseEnter += delegate { gripHover = true; };
            Grip.MouseLeave += delegate { gripHover = false; };
            Children.Add(Grip);
            SetLeft(Grip, 68); SetTop(Grip, -4);
            copied.Width = 84;
            copied.Padding = new Thickness(10, 4, 10, 4);
            copied.Background = Brushes.White;
            copied.BorderBrush = LightTheme.Brush("#DCE1E9");
            copied.BorderThickness = new Thickness(1);
            copied.CornerRadius = new CornerRadius(12);
            copied.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.18 };
            copied.Child = new TextBlock { Text = "✓ 已复制", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = LightTheme.Brush("#20408E"), TextAlignment = TextAlignment.Center };
            copied.Opacity = 0;
            copied.IsHitTestVisible = false;
            Children.Add(copied);
            flash.Tick += delegate { flash.Stop(); copied.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.2))); };
            Surface.MouseEnter += delegate { hovering = true; close.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.18))); };
            Surface.MouseLeave += delegate { hovering = false; close.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.18))); };
            Surface.MouseLeftButtonDown += Down;
            Surface.MouseLeftButtonUp += Up;
            Surface.MouseMove += Move;
            Surface.LostMouseCapture += delegate { hold.Stop(); pressed = false; };
            hold.Tick += delegate { hold.Stop(); held = true; pressed = false; Surface.ReleaseMouseCapture(); act(ShotAction.Edit, item); };
            menu = new LightMenu("截图", "");
            AddMenu("复制图片", "copy", ShotAction.Copy, true);
            AddMenu("打开图片", "open", ShotAction.Open);
            AddMenu("编辑图片", "edit", ShotAction.Edit);
            AddMenu("保存副本…", "save", ShotAction.Save);
            AddMenu("在文件夹中显示", "folder", ShotAction.Locate);
            menu.Separator();
            AddMenu(managed ? "移到回收站" : "从绳子上取下", managed ? "trash" : "remove", ShotAction.Remove, false, managed);
            if (!managed) AddMenu("移到回收站", "trash", ShotAction.Trash, false, true);
            menu.Footer("单击复制 · 双击打开");
            Surface.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                menu.PlacementTarget = Surface;
                WindowsNative.SetForegroundWindow(new WindowInteropHelper(Window.GetWindow(this)).Handle);
                menu.IsOpen = true;
            };
            UpdateImage();
        }

        void AddMenu(string title, string icon, ShotAction action, bool primary = false, bool danger = false)
        {
            menu.AddAction(title, icon, delegate { act(action, Item); }, primary, danger);
        }

        public void ApplyTheme(LineTheme theme, int colorIndex)
        {
            Appearance = theme; ColorIndex = colorIndex;
            clip.Apply(theme, colorIndex);
        }

        public bool UpdateImage()
        {
            if (displayed == Item.Image) return false;
            displayed = Item.Image;
            menu.SetDetails(displayed.PixelWidth + " × " + displayed.PixelHeight);
            photo.Source = displayed.Thumbnail;
            double scale = Math.Min(142.0 / displayed.Thumbnail.PixelWidth, 98.0 / displayed.Thumbnail.PixelHeight);
            photo.Width = displayed.Thumbnail.PixelWidth * scale;
            photo.Height = displayed.Thumbnail.PixelHeight * scale;
            photo.Clip = new RectangleGeometry(new Rect(0, 0, photo.Width, photo.Height), 7, 7);
            Surface.Width = photo.Width + 14;
            Surface.Height = photo.Height + 14;
            SetLeft(Surface, (Width - Surface.Width) / 2);
            SetTop(Surface, 20);
            SetLeft(copied, (Width - copied.Width) / 2);
            SetTop(copied, Surface.Height + 30);
            return true;
        }

        public void Place(double x, bool immediately)
        {
            if (!placed || immediately) xMotion.Snap(x);
            else xMotion.Target = x;
            placed = true;
        }

        public void Frame(double elapsed, double sag, double lineWidth)
        {
            if (IsDisposed) return;
            xMotion.Step(elapsed);
            drop.Step(elapsed);
            swing.Step(elapsed);
            if (breezeDelay >= 0)
            {
                breezeDelay -= elapsed;
                if (breezeDelay < 0 && !Flying && !Busy) Nudge(breezeAngle);
            }
            scaleMotion.Target = pressed ? 0.95 : (hovering && !Releasing ? 1.035 : 1);
            scaleMotion.Step(elapsed);
            clipMotion.Target = gripHover || Releasing ? 1 : 0;
            clipMotion.Step(elapsed);
            clipLift.Y = -0.8 * clipMotion.Position;
            clipScale.ScaleX = clipScale.ScaleY = 1 + 0.06 * clipMotion.Position;
            photoScale.ScaleX = photoScale.ScaleY = scaleMotion.Position;
            double fraction = (xMotion.Position + 82) / lineWidth;
            position.X = xMotion.Position;
            position.Y = 18 + 4 * sag * fraction * (1 - fraction) - 8 + drop.Position;
            Canvas.SetLeft(this, position.X);
            Canvas.SetTop(this, position.Y);
            tilt.Angle = Tilt + swing.Position;
            entryTime += elapsed;
            Opacity = Flying ? 0 : Math.Min(1, entryTime / 0.16);
        }

        public void Breeze(double delay, double degrees) { breezeDelay = delay; breezeAngle = degrees; }
        public void ScrollWith(double velocity) { swing.Target = Flying || Busy ? 0 : Math.Max(-2.5, Math.Min(2.5, -velocity / 200)); }
        public void Nudge(double degrees) { if (!Flying && !IsDisposed) swing.Velocity += degrees * 8; }

        public void OpenClip()
        {
            ((ToolTip)Grip.ToolTip).IsOpen = false;
            ToolTipService.SetIsEnabled(Grip, false);
            Releasing = true; Grip.IsEnabled = false;
            clip.Open();
        }
        public void DetachPhoto()
        {
            Releasing = false; Detached = true;
            Surface.Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.12)) { BeginTime = TimeSpan.FromSeconds(0.10) });
        }
        public void ResetClip()
        {
            BeginAnimation(OpacityProperty, null);
            Surface.Opacity = 1;
            ToolTipService.SetIsEnabled(Grip, true);
            Releasing = false; Grip.IsEnabled = true;
            clip.Reset();
        }

        public void BeginFlight()
        {
            Releasing = false;
            Flying = true;
            drop.Snap(0);
            swing.Snap(0);
            entryTime = 0;
            Opacity = 0;
        }

        public void Land()
        {
            if (IsDisposed) return;
            Flying = false;
            entryTime = 0;
            Nudge(2.2);
        }

        public Rect PhotoRect()
        {
            var parent = (Canvas)Parent;
            double x = position.X + (Width - Surface.Width) / 2, y = position.Y + 20;
            return new Rect(parent.PointToScreen(new Point(x, y)), parent.PointToScreen(new Point(x + Surface.Width, y + Surface.Height)));
        }

        void Down(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.ClickCount == 2) { hold.Stop(); held = true; act(ShotAction.Open, Item); return; }
            start = e.GetPosition(this);
            pressed = true;
            held = false;
            Surface.CaptureMouse();
            hold.Start();
        }

        void Up(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            hold.Stop();
            bool copy = pressed && !held && !dragging;
            pressed = false;
            Surface.ReleaseMouseCapture();
            if (copy) act(ShotAction.Copy, Item);
        }

        void Move(object sender, MouseEventArgs e)
        {
            if (!pressed || held || dragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point position = e.GetPosition(this);
            if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            hold.Stop();
            pressed = false;
            dragging = true;
            Surface.Opacity = 0.45;
            Surface.ReleaseMouseCapture();
            var payload = new DataObject(DataFormats.FileDrop, new[] { Item.Path });
            var preview = new DragPreview(Item.Image.Thumbnail);
            try { preview.Show(); DragDrop.DoDragDrop(Surface, payload, DragDropEffects.Copy | DragDropEffects.Move); }
            finally { preview.Close(); dragging = false; Surface.Opacity = 1; }
        }

        public void FlashCopied()
        {
            copied.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.15)));
            Nudge(3);
            flash.Stop(); flash.Start();
        }
        public void Dispose() { IsDisposed = true; hold.Stop(); flash.Stop(); menu.IsOpen = false; }
    }

    internal sealed class DragPreview : Window
    {
        readonly DispatcherTimer follow = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        IntPtr handle;

        public DragPreview(System.Windows.Media.Imaging.BitmapSource image)
        {
            double scale = Math.Min(126.0 / image.PixelWidth, 86.0 / image.PixelHeight);
            Width = image.PixelWidth * scale + 12;
            Height = image.PixelHeight * scale + 12;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            IsHitTestVisible = false;
            Opacity = 0.8;
            Content = new Border { CornerRadius = new CornerRadius(10), Background = Brushes.White, Padding = new Thickness(6), Child = new Image { Source = image } };
            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                WindowsNative.PrepareOverlay(handle);
                WindowsNative.PassMouse(handle, true);
                Follow();
                follow.Start();
            };
            follow.Tick += delegate { Follow(); };
            Closed += delegate { follow.Stop(); };
        }

        void Follow()
        {
            var cursor = WindowsNative.Cursor();
            double scale = WindowsNative.MonitorScale(WindowsNative.ScreenAtPointer());
            WindowsNative.SetWindowPos(handle, new IntPtr(-1), cursor.X + 18, cursor.Y + 20, (int)(Width * scale), (int)(Height * scale), 0x10);
        }
    }
}
