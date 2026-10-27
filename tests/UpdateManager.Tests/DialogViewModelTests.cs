using UpdateManager.App.ViewModels;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;

namespace UpdateManager.Tests;

/// <summary>Логика диалогов, которую показывают окна: состояние словами, подписи кнопок.</summary>
public sealed class DialogViewModelTests
{
    [Fact]
    public void Operation_NotStarted_IsFailedWithReasonAboveLog()
    {
        var vm = new OperationViewModel(new NotStartingOperation());

        vm.Start();

        Assert.True(vm.IsFailed);
        Assert.False(vm.IsRunning);
        Assert.Equal("Завершилось с ошибкой", vm.StateText);
        Assert.Equal("Уже идёт другая сборка.", vm.FailureText);
        Assert.Equal("Закрыть", vm.ButtonText);
    }

    [Theory]
    [InlineData(true, "Доставить")]
    [InlineData(false, "Сохранить")]
    public void Delivery_ActionNamesWhatHappens(bool deliverNow, string expected)
    {
        var vm = new DeliveryViewModel(new DeliveryConfig(), new FakeDialogService(), deliverNow);

        Assert.Equal(expected, vm.ActionText);
    }

    [Fact]
    public void Settings_ShowsResultingDownloadUrl()
    {
        var vm = new ProjectSettingsViewModel(new ProjectSettings
        {
            BaseDownloadURL = "https://updates.example.com/MyApp/"
        });
        Assert.Equal("Клиенты будут качать патч с https://updates.example.com/MyApp/", vm.DownloadUrlText);

        vm.UploadDirectory = "Other";
        Assert.Equal("Клиенты будут качать патч с https://updates.example.com/Other/", vm.DownloadUrlText);

        vm.Server = " ";
        Assert.True(vm.HasNoServer);
        Assert.StartsWith("Адрес сервера не задан", vm.DownloadUrlText);
    }

    private sealed class NotStartingOperation : IEngineOperation
    {
        public string Title => "Создание патча";
        public bool Start() => false;
        public void Cancel() { }
        public string FetchLog() => null!;
        public int? FetchProgressPercentage() => null;
        public bool IsRunning => false;
        public bool Succeeded => false;
        public string ResultDetails => "Уже идёт другая сборка.";
    }
}
