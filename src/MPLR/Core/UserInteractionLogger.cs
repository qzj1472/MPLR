using System.Windows;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfControl = System.Windows.Controls.Control;
using WpfPasswordBox = System.Windows.Controls.PasswordBox;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace MPLR.Core;

internal static class UserInteractionLogger
{
    private static bool isInstalled;

    public static void Install()
    {
        if (isInstalled)
        {
            return;
        }

        isInstalled = true;
        EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
        EventManager.RegisterClassHandler(typeof(WpfButtonBase), WpfButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick), true);
        EventManager.RegisterClassHandler(typeof(WpfTextBox), WpfTextBox.TextChangedEvent, new System.Windows.Controls.TextChangedEventHandler(OnTextChanged), true);
        EventManager.RegisterClassHandler(typeof(WpfPasswordBox), WpfPasswordBox.PasswordChangedEvent, new RoutedEventHandler(OnPasswordChanged), true);
        EventManager.RegisterClassHandler(typeof(WpfSelector), WpfSelector.SelectionChangedEvent, new System.Windows.Controls.SelectionChangedEventHandler(OnSelectionChanged), true);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded), true);
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        FrameworkElement? element = FindElement(source);

        if (element == null)
        {
            return;
        }

        AppSessionLogger.Event("debug", "user_input", "mouse_down", "user clicked element", new
        {
            triggerSource = "PreviewMouseDown",
            button = e.ChangedButton.ToString(),
            clickCount = e.ClickCount,
            element = DescribeElement(element),
            originalSource = e.OriginalSource.GetType().FullName,
        });
    }

    private static void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        AppSessionLogger.Event("info", "user_action", "button_click", "button command invoked", new
        {
            triggerSource = "ButtonBase.Click",
            element = DescribeElement(element),
            command = DescribeCommand(sender),
        });
    }

    private static void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        bool sensitive = IsSensitive(textBox);

        AppSessionLogger.Event("debug", "user_input", "text_changed", "text input changed", new
        {
            triggerSource = "TextBox.TextChanged",
            element = DescribeElement(textBox),
            binding = GetBindingPath(textBox, WpfTextBox.TextProperty),
            isSensitive = sensitive,
            value = sensitive ? null : Limit(textBox.Text),
            length = textBox.Text?.Length ?? 0,
            selectionStart = textBox.SelectionStart,
            selectionLength = textBox.SelectionLength,
            changes = e.Changes.Select(change => new
            {
                change.Offset,
                change.AddedLength,
                change.RemovedLength,
            }).ToArray(),
        });
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfPasswordBox passwordBox)
        {
            return;
        }

        AppSessionLogger.Event("debug", "user_input", "password_changed", "sensitive input changed", new
        {
            triggerSource = "PasswordBox.PasswordChanged",
            element = DescribeElement(passwordBox),
            isSensitive = true,
            length = passwordBox.Password?.Length ?? 0,
        });
    }

    private static void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not WpfSelector selector)
        {
            return;
        }

        AppSessionLogger.Event("debug", "user_input", "selection_changed", "selection changed", new
        {
            triggerSource = "Selector.SelectionChanged",
            element = DescribeElement(selector),
            binding = GetBindingPath(selector, WpfSelector.SelectedIndexProperty) ?? GetBindingPath(selector, WpfSelector.SelectedItemProperty),
            added = e.AddedItems.Cast<object>().Select(SafeItemText).ToArray(),
            removed = e.RemovedItems.Cast<object>().Select(SafeItemText).ToArray(),
            selectedIndex = selector.SelectedIndex,
            selectedItem = SafeItemText(selector.SelectedItem),
        });
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        AppSessionLogger.Event("info", "navigation", "window_loaded", "window loaded", new
        {
            window = DescribeElement(window),
            title = window.Title,
            owner = window.Owner?.GetType().FullName,
        });

        window.Closed -= OnWindowClosed;
        window.Closed += OnWindowClosed;
    }

    private static void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        AppSessionLogger.Event("info", "navigation", "window_closed", "window closed", new
        {
            window = DescribeElement(window),
            title = window.Title,
        });
    }

    private static object DescribeElement(FrameworkElement element)
    {
        Window? window = Window.GetWindow(element);
        System.Windows.Point? screenPoint = null;

        try
        {
            screenPoint = element.PointToScreen(new System.Windows.Point(0, 0));
        }
        catch (InvalidOperationException)
        {
        }

        return new
        {
            type = element.GetType().FullName,
            name = string.IsNullOrWhiteSpace(element.Name) ? null : element.Name,
            automationName = EmptyToNull(AutomationProperties.GetName(element)),
            uid = EmptyToNull(element.Uid),
            tag = EmptyToNull(element.Tag?.ToString()),
            dataContext = element.DataContext?.GetType().FullName,
            window = window?.GetType().FullName,
            windowTitle = window?.Title,
            x = screenPoint?.X,
            y = screenPoint?.Y,
            width = element.ActualWidth,
            height = element.ActualHeight,
        };
    }

    private static FrameworkElement? FindElement(DependencyObject source)
    {
        DependencyObject? current = source;

        while (current != null)
        {
            if (current is FrameworkElement element && (element is WpfControl || element.Focusable || !string.IsNullOrWhiteSpace(element.Name)))
            {
                return element;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return source as FrameworkElement;
    }

    private static object? DescribeCommand(object sender)
    {
        if (sender is not WpfButtonBase button)
        {
            return null;
        }

        return new
        {
            type = button.Command?.GetType().FullName,
            binding = GetBindingPath(button, WpfButtonBase.CommandProperty),
            parameter = SafeItemText(button.CommandParameter),
        };
    }

    private static string? GetBindingPath(DependencyObject target, DependencyProperty property)
    {
        BindingExpression? expression = BindingOperations.GetBindingExpression(target, property);
        return expression?.ParentBinding.Path?.Path;
    }

    private static bool IsSensitive(FrameworkElement element)
    {
        string combined = string.Join(" ", new[]
        {
            element.Name,
            element.Uid,
            element.Tag?.ToString(),
            AutomationProperties.GetName(element),
            element.DataContext?.GetType().FullName,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return combined.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("key", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SafeItemText(object? value)
    {
        if (value == null)
        {
            return null;
        }

        string text = value.ToString() ?? string.Empty;
        return LooksSensitive(text) ? "[redacted]" : Limit(text);
    }

    private static bool LooksSensitive(string value)
    {
        return value.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("sessionid", StringComparison.OrdinalIgnoreCase);
    }

    private static string Limit(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= 1000 ? value : value[..1000] + $"...[truncated:{value.Length}]";
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

