using MPLR.ViewModels;
using Wpf.Ui.Controls;

namespace MPLR.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow()
    {
        DataContext = ViewModel = new();
        InitializeComponent();
    }
}

