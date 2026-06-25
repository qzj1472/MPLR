using MPLR.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
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

    private void NumberInputPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = sender is not System.Windows.Controls.TextBox textBox || !IsAllowedNumberInput(textBox, e.Text);
    }

    private void NumberInputPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Space)
        {
            e.Handled = true;
        }
    }

    private void NumberInputPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox ||
            !e.DataObject.GetDataPresent(System.Windows.DataFormats.Text) ||
            e.DataObject.GetData(System.Windows.DataFormats.Text) is not string text ||
            !IsAllowedNumberInput(textBox, text))
        {
            e.CancelCommand();
        }
    }

    private static bool IsAllowedNumberInput(System.Windows.Controls.TextBox textBox, string input)
    {
        string value = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, input);

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        bool allowDecimal = string.Equals((textBox.Tag as string) ?? string.Empty, "Decimal", StringComparison.OrdinalIgnoreCase);

        return allowDecimal
            ? double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _)
            : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }
}
