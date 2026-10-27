using Avalonia.Controls;

namespace UpdateManager.App.Views;

public partial class OperationWindow : DialogWindow
{
    public OperationWindow()
    {
        InitializeComponent();
        // Лог растёт снизу — держим прокрутку в конце.
        LogBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
        };
    }
}
