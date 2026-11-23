using System.Windows;
using Langent.App.Services;

namespace Langent.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        KeyBox.PasswordChanged += (_, _) => _viewModel.ApiKeyInput = KeyBox.Password;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyBrushes(Resources);
    }
}
