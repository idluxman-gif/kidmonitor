using KidMonitor.Tray;

// Prevent duplicate instances
using var mutex = new Mutex(true, "KidMonitorTray_SingleInstance", out bool isNew);
if (!isNew)
    return; // Another instance is already running

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

Application.Run(new TrayApplicationContext());
