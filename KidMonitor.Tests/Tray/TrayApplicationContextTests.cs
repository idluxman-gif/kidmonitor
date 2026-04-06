using System.Reflection;
using System.Windows.Forms;
using KidMonitor.Tray;

namespace KidMonitor.Tests.Tray;

public sealed class TrayApplicationContextTests
{
    [Fact]
    public void Constructor_AddsPairWithParentAppMenuItem()
    {
        using var context = new TrayApplicationContext();

        var trayIcon = GetRequiredField<NotifyIcon>(context, "_trayIcon");
        var menu = Assert.IsType<ContextMenuStrip>(trayIcon.ContextMenuStrip);

        Assert.Contains(
            menu.Items.OfType<ToolStripMenuItem>(),
            item => string.Equals(item.Text, "Pair with parent app", StringComparison.Ordinal));
    }

    private static T GetRequiredField<T>(object instance, string fieldName)
        where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field.GetValue(instance);
        return Assert.IsType<T>(value);
    }
}
