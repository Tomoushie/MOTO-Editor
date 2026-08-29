// CustomMenuBarView.xaml.cs — AJOUTS (code-behind)
using Microsoft.Maui.Devices;

// Propriété : fenêtre native
private Microsoft.UI.Xaml.Window NativeWindow =>
    Application.Current.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window;

public CustomMenuBarView()
{
    InitializeComponent();
    BuildMenus();

    SizeChanged += (s, e) => UpdateDragRegion();

#if !WINDOWS
    WinControls.IsVisible = false;   // contrôles natifs ailleurs
#endif
}

// ── Contrôles de fenêtre ──
private void OnMinClicked(object s, EventArgs e)
{
#if WINDOWS
    if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
        p.Minimize();
#endif
}

private void OnMaxClicked(object s, EventArgs e)
{
#if WINDOWS
    if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
    {
        if (p.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized) p.Restore();
        else p.Maximize();
    }
#endif
}

private void OnCloseClicked(object s, EventArgs e)
{
#if WINDOWS
    NativeWindow?.Close();
#endif
}

// ── Zone de drag : bande centrale (entre menus et contrôles fenêtre) ──
private void UpdateDragRegion()
{
#if WINDOWS
    if (NativeWindow == null || Width <= 0) return;

    double density = DeviceDisplay.Current.MainDisplayInfo.Density;
    int x = (int)(MenuHost.Width * density);
    int w = (int)((Width - MenuHost.Width - WinControls.Width - 8) * density);
    int h = (int)(Height * density);

    if (w > 0)
    {
        NativeWindow.SetDragRectangles(new Windows.Graphics.RectInt32[]
        {
            new Windows.Graphics.RectInt32(x, 0, w, h)
        });
    }
#endif
}
