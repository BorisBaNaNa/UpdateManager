using CommunityToolkit.Mvvm.ComponentModel;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// VM модального окна: просит окно закрыться с результатом (true = OK). Окно — <see cref="Views.DialogWindow"/>.
/// </summary>
public abstract class DialogViewModel : ObservableObject
{
    public event EventHandler<bool>? CloseRequested;

    protected void Close(bool result) => CloseRequested?.Invoke(this, result);

    /// <summary>
    /// Можно ли закрыть окно крестиком прямо сейчас. false — закрытие отменяется
    /// (например, операция ещё идёт и вместо закрытия прерывается).
    /// </summary>
    public virtual bool CanCloseByUser() => true;
}
