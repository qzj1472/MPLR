using Wpf.Ui.Controls;

namespace MPLR.Views;

public partial class ScreenRecordListWindow : FluentWindow
{
    public ScreenRecordListWindow()
    {
        InitializeComponent();
    }

    private void CloseButtonClick(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
