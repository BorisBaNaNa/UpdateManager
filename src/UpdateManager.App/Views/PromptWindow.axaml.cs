namespace UpdateManager.App.Views;

public partial class PromptWindow : DialogWindow
{
    public PromptWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Как InputBox: значение сразу выделено — можно подтвердить или перепечатать.
        ValueBox.Focus();
        ValueBox.SelectAll();
    }
}
