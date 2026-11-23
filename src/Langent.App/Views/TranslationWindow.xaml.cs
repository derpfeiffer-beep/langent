using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Langent.App.Native;
using Langent.App.Services;
using Langent.Core.Settings;

namespace Langent.App.Views;

public partial class TranslationWindow : Window
{
    private readonly TranslationViewModel _viewModel;
    private readonly AppSettings _settings;
    private readonly Action _persistSettings;

    public TranslationWindow(TranslationViewModel viewModel, AppSettings settings, Action persistSettings)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settings = settings;
        _persistSettings = persistSettings;
        DataContext = viewModel;

        Width = Math.Max(MinWidth, settings.Window.Width);
        Height = Math.Max(MinHeight, settings.Window.Height);

        viewModel.HideWindow = () => Dispatcher.Invoke(Hide);
        viewModel.ShowWindow = () => Dispatcher.Invoke(() =>
        {
            Show();
            Activate();
        });
        viewModel.CloseWindow = () => Dispatcher.Invoke(Close);

        TitleBar.MouseLeftButtonDown += OnTitleBarMouseDown;
        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTheme();
        PositionNearCursor();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TranslationBox.Focus();
        _viewModel.Begin();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.Cancel();
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            TargetCombo.Focus();
            e.Handled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Cancel();
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Cancel();
        _settings.Window = new WindowSettings { Width = (int)Width, Height = (int)Height };
        try
        {
            _persistSettings();
        }
        catch (Exception)
        {
            // Persisting the window size must not break closing.
        }
    }

    private void ApplyTheme()
    {
        var light = ThemeService.IsLightTheme();

        Set("Bg", light ? "#F3F3F3" : "#1E1E1E");
        Set("Panel", light ? "#FFFFFF" : "#2A2A2A");
        Set("Fg", light ? "#1A1A1A" : "#EAEAEA");
        Set("Muted", light ? "#666666" : "#9A9A9A");
        Set("Border", light ? "#CCCCCC" : "#3A3A3A");
        Set("Accent", light ? "#2D7DD2" : "#4C9AF0");
    }

    private void Set(string key, string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        Resources[key] = brush;
    }

    private void PositionNearCursor()
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        var toDevice = source.CompositionTarget.TransformToDevice;
        var scaleX = toDevice.M11;
        var scaleY = toDevice.M22;

        var widthPx = Width * scaleX;
        var heightPx = Height * scaleY;
        var work = info.rcWork;

        var leftPx = Math.Clamp(cursor.X + 8.0, work.Left, Math.Max(work.Left, work.Right - widthPx));
        var topPx = Math.Clamp(cursor.Y + 8.0, work.Top, Math.Max(work.Top, work.Bottom - heightPx));

        Left = leftPx / scaleX;
        Top = topPx / scaleY;
    }
}
