using System.Windows;
namespace EriMixer;
public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? instanceMutex;
    private System.Threading.EventWaitHandle? showRequest;
    private System.Threading.RegisteredWaitHandle? showListener;
    private bool ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool startup = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
        instanceMutex = new System.Threading.Mutex(true, @"Local\EriMixer.Instance", out ownsMutex);
        if (!ownsMutex)
        {
            if (!startup)
            {
                try { using var existing = System.Threading.EventWaitHandle.OpenExisting(@"Local\EriMixer.Show"); existing.Set(); }
                catch (System.Threading.WaitHandleCannotBeOpenedException) { }
            }
            Shutdown();
            return;
        }
        showRequest = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, @"Local\EriMixer.Show");
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EriMixer");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "last-error.txt"), args.Exception.ToString());
            }
            catch { }
        };
        ThemeManager.Initialize();
        base.OnStartup(e);
        var mixer = new MainWindow();
        MainWindow = mixer;
        // Create the native handle and shortcut hook without flashing a window at sign-in.
        new System.Windows.Interop.WindowInteropHelper(mixer).EnsureHandle();
        showListener = System.Threading.ThreadPool.RegisterWaitForSingleObject(showRequest,
            (_, _) => Dispatcher.BeginInvoke(new Action(mixer.ShowStandalone)), null,
            System.Threading.Timeout.Infinite, false);
        if (!startup) mixer.ShowStandalone();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        showListener?.Unregister(null);
        showRequest?.Dispose();
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
