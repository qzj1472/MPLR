using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.System;

namespace TiktokLiveRec.Views;

[ObservableObject]
public partial class AboutContentDialog : ContentDialog
{
    public AboutContentDialog()
    {
        DataContext = this;
        InitializeComponent();
    }

    [RelayCommand]
    private async Task OpenHyperlink(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            _ = await Launcher.LaunchUriAsync(uri);
        }
    }
}
