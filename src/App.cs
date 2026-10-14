using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("PicRope · 图绳")]
[assembly: AssemblyProduct("PicRope")]
[assembly: AssemblyDescription("图绳：把截图挂在屏幕上沿，随手复制、拖拽和保存。")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

namespace ScreenLine
{
    internal sealed class DesktopController : IDisposable
    {
        public readonly Gallery Gallery;
        public readonly CapturePipeline Captures;
        public readonly ShelfWindow Shelf;
        readonly WindowsEvents events;
        readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
        LightNotice notice;
        readonly MenuItem collect;
        internal readonly LightMenu TrayMenu;
        internal AppearanceWindow Appearance { get; private set; }
        internal readonly List<ImageEditorWindow> Editors = new List<ImageEditorWindow>();
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public DesktopController(StateStore store, string screenshots)
        {
            Gallery = new Gallery(store);
            Captures = new CapturePipeline(Gallery, Application.Current.Dispatcher, screenshots);
            Shelf = new ShelfWindow(Gallery, Act, EnableClipboard);
            events = new WindowsEvents();
            events.ToggleRequested += Shelf.Toggle;
            events.ExitRequested += Exit;
            events.ClipboardChanged += Captures.ClipboardChanged;
            Captures.Arrived += Shelf.ShowNewCapture;
            Captures.Failed += Notice;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PicRope.Icon"))
                tray.Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
            tray.Text = "PicRope · 图绳";
            TrayMenu = new LightMenu("图绳", "");
            var show = TrayMenu.AddAction("显示 / 收起", "show", delegate { Shelf.Toggle(); }, true);
            if (events.ShortcutAvailable) show.InputGestureText = "Ctrl+Alt+T";
            else Notice("Ctrl+Alt+T 已被占用，可点击托盘图标打开。");
            collect = TrayMenu.AddAction("收集剪贴板截图", "clipboard", delegate { Captures.CollectClipboard = collect.IsChecked; Shelf.Update(); });
            collect.IsCheckable = true;
            collect.IsChecked = Captures.CollectClipboard;
            TrayMenu.AddAction("外观…", "appearance", ShowAppearance);
            TrayMenu.AddAction("添加图片…", "add", async delegate
            {
                var picker = new OpenFileDialog { Title = "添加图片", Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", Multiselect = true };
                if (picker.ShowDialog() != true) return;
                foreach (string file in picker.FileNames)
                    if (!await Captures.Import(file, null, true)) Notice("无法读取图片：" + Path.GetFileName(file));
            });
            TrayMenu.AddAction("打开收集文件夹", "folder", delegate
            {
                try { WindowsShell.OpenInbox(store); }
                catch (Exception error) { store.Log(error); Notice("无法打开收集文件夹：" + error.Message); }
            });
            TrayMenu.AddAction("全部取下（保留文件）", "clear", delegate { Gallery.Clear(); });
            TrayMenu.Separator();
            MenuItem startup = null;
            startup = TrayMenu.AddAction("开机启动", "startup", delegate
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (startup.IsChecked) key.SetValue("PicRope", Quote(Assembly.GetEntryAssembly().Location));
                    else key.DeleteValue("PicRope", false);
                }
            });
            startup.IsCheckable = true;
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) startup.IsChecked = key != null && key.GetValue("PicRope") != null;
            TrayMenu.AddAction("退出", "exit", Exit);
            tray.MouseUp += delegate(object sender, Forms.MouseEventArgs args) { if (args.Button == Forms.MouseButtons.Right) ShowTrayMenu(); };
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs args) { if (args.Button == Forms.MouseButtons.Left) Shelf.Toggle(); };
            tray.Visible = true;
        }

        async void Exit()
        {
            foreach (var editor in Editors.ToArray()) if (!await editor.ConfirmClose()) return;
            Application.Current.Shutdown();
        }

        public async Task Start()
        {
            Shelf.Show();
            Shelf.Reveal(true, false, 4);
            await Captures.Start();
        }

        void EnableClipboard()
        {
            Captures.CollectClipboard = true;
            collect.IsChecked = true;
        }

        void ShowAppearance()
        {
            if (Appearance != null) { Appearance.Activate(); return; }
            Appearance = new AppearanceWindow(Shelf.Appearance, Gallery, delegate(LineTheme theme)
            {
                Gallery.Store.State.Theme = theme.Id;
                Gallery.Store.Save();
                Shelf.ApplyTheme(theme);
                Shelf.Reveal(true, false, 3);
            });
            Appearance.Owner = Shelf;
            Shelf.ChoosingAppearance = true;
            Shelf.Reveal(true, false, 0);
            Appearance.Closed += delegate { Appearance = null; Shelf.ChoosingAppearance = false; };
            Appearance.Show();
        }

        internal void ShowTrayMenu()
        {
            TrayMenu.SetDetails(Gallery.Items.Count + " 张图片");
            WindowsNative.SetForegroundWindow(Shelf.Handle);
            var cursor = WindowsNative.Cursor();
            var point = Shelf.PointFromScreen(new Point(cursor.X, cursor.Y));
            TrayMenu.PlacementTarget = Shelf;
            TrayMenu.Placement = PlacementMode.RelativePoint;
            TrayMenu.HorizontalOffset = point.X - 16;
            TrayMenu.VerticalOffset = point.Y - 16;
            TrayMenu.IsOpen = true;
        }

        void Notice(string message)
        {
            if (notice != null) notice.Close();
            notice = new LightNotice(message);
            var current = notice;
            current.Closed += delegate { if (notice == current) notice = null; };
            notice.Show();
        }

        static string Quote(string path) { return "\"" + path + "\""; }

        public async void Act(ShotAction action, CaptureItem item)
        {
            try
            {
                if (!File.Exists(item.Path)) { Gallery.Prune(); return; }
                switch (action)
                {
                    case ShotAction.Copy:
                        var payload = new DataObject();
                        byte[] png;
                        using (var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            var image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                            payload.SetImage(image);
                            png = ImageContent.EncodeClipboard(image);
                        }
                        payload.SetData("PNG", new MemoryStream(png));
                        payload.SetFileDropList(new StringCollection { item.Path });
                        await WindowsClipboard.Write(payload);
                        Shelf.ShowCopied(item.Path);
                        break;
                    case ShotAction.Open:
                        Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
                        Shelf.FollowImageViewer(item.Path);
                        break;
                    case ShotAction.Edit:
                        var existing = Editors.Find(editor => FileRules.SameFile(editor.ImagePath, item.Path));
                        if (existing != null)
                        {
                            existing.Present();
                            break;
                        }
                        var window = new ImageEditorWindow(item.Path, Gallery.Store, RefreshEditedImage, Captures.IgnoreCurrentClipboard);
                        Editors.Add(window);
                        window.Closed += delegate
                        {
                            Editors.Remove(window);
                            if (!window.ReturnBounds.IsEmpty) Shelf.ShowEditedImage(window.ImagePath, window.ReturnBounds, window.ReturnPicture);
                        };
                        Shelf.Reveal(false, false, 0);
                        window.Present();
                        break;
                    case ShotAction.Save:
                        var dialog = new SaveFileDialog { Title = "保存副本", FileName = Path.GetFileName(item.Path), Filter = "图片|*" + Path.GetExtension(item.Path), OverwritePrompt = true };
                        if (dialog.ShowDialog() == true && !FileRules.SameFile(item.Path, dialog.FileName)) File.Copy(item.Path, dialog.FileName, true);
                        break;
                    case ShotAction.Locate:
                        Process.Start("explorer.exe", "/select," + Quote(item.Path));
                        break;
                    case ShotAction.Remove:
                        if (FileRules.IsInside(item.Path, Gallery.Store.Inbox))
                            FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        Shelf.ShowDiscard(item);
                        Gallery.Remove(item);
                        break;
                    case ShotAction.Trash:
                        FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        Shelf.ShowDiscard(item);
                        Gallery.Remove(item);
                        break;
                }
            }
            catch (Exception error) { Gallery.Store.Log(error); Notice("操作未完成：" + error.Message); }
        }

        public void Dispose()
        {
            if (Appearance != null) Appearance.Close();
            if (notice != null) notice.Close();
            foreach (var editor in Editors.ToArray()) editor.CloseWithoutPrompt();
            TrayMenu.IsOpen = false;
            Captures.Dispose();
            events.Dispose();
            tray.Visible = false;
            var icon = tray.Icon;
            tray.Dispose();
            icon.Dispose();
            Shelf.Close();
        }

        async Task RefreshEditedImage(string path)
        {
            ImageContent image = await Task.Run(() => ImageContent.Read(path));
            var item = Gallery.Items.Find(candidate => FileRules.SameFile(candidate.Path, path));
            if (item == null) Gallery.Add(path, image);
            else
            {
                item.Image = image;
                item.Written = File.GetLastWriteTimeUtc(path);
                Gallery.Notify();
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        public static void Main(string[] arguments)
        {
            if (Array.IndexOf(arguments, "--quit") >= 0) { WindowsNative.RequestExit(); return; }
            bool first;
            using (var instance = new Mutex(true, @"Local\TangHao.ScreenLine.Windows", out first))
            {
                if (!first) { WindowsNative.ShowExisting(); return; }
                var store = new StateStore(StateStore.DefaultRoot);
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                LightTheme.Install(app);
                DesktopController controller = null;
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args) { store.Log(args.Exception); args.Handled = true; };
                app.Startup += async delegate { controller = new DesktopController(store, WindowsNative.ScreenshotFolder()); await controller.Start(); };
                app.Exit += delegate { if (controller != null) controller.Dispose(); };
                app.Run();
            }
        }
    }
}
