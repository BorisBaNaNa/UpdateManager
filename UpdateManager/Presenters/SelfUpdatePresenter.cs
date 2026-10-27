using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UpdateManager.Core.SelfUpdate;
using UpdateManager.Views;

namespace UpdateManager.Presenters
{
    /// <summary>
    /// Самообновление UpdateManager: тихая проверка при старте + ручная из меню,
    /// скачивание в окне прогресса и перезапуск через self-patcher.
    /// </summary>
    public class SelfUpdatePresenter
    {
        private readonly ISelfUpdateView _view;
        private readonly SelfUpdateConfig _config;

        private bool _checking; // идёт проверка — вторую параллельно не запускаем

        public SelfUpdatePresenter(ISelfUpdateView view, SelfUpdateConfig config)
        {
            _view = view;
            _config = config;

            _view.ViewShown += OnViewShown;
            _view.CheckSelfUpdateRequested += OnCheckRequested;
        }

        private void OnViewShown(object sender, EventArgs e)
        {
            _view.SetSelfUpdateAvailable(_config.IsEnabled);

            // Под отладчиком не проверяем сами: иначе обновление перезатрёт bin\Debug.
            if (_config.IsEnabled && !Debugger.IsAttached)
                StartCheck(manual: false);
        }

        private void OnCheckRequested(object sender, EventArgs e)
        {
            if (!_config.IsEnabled)
            {
                _view.ShowError("Адрес сервера обновлений не задан (SelfUpdate.VersionInfoUrl в UpdateManager.exe.config).");
                return;
            }

            if (Debugger.IsAttached && !_view.Confirm(
                "Программа запущена под отладчиком: обновление заменит файлы в папке сборки.\n" +
                "Всё равно проверить?"))
                return;

            StartCheck(manual: true);
        }

        private void StartCheck(bool manual)
        {
            if (_checking)
                return;

            _checking = true;
            _view.SetSelfUpdateAvailable(false);

            // Проверка блокирующая (сеть + хеши файлов) — в фоне, итог обратно в UI-поток.
            Task.Run(() => new SelfUpdateChecker(_config).Check())
                .ContinueWith(t =>
                {
                    _checking = false;
                    _view.SetSelfUpdateAvailable(true);

                    var result = t.IsFaulted
                        ? new SelfUpdateCheckResult
                        {
                            Status = SelfUpdateStatus.Failed,
                            NewVersion = "",
                            Error = t.Exception.GetBaseException().Message
                        }
                        : t.Result;
                    OnChecked(result, manual);
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnChecked(SelfUpdateCheckResult result, bool manual)
        {
            switch (result.Status)
            {
                case SelfUpdateStatus.UpToDate:
                    if (manual)
                        _view.ShowInfo("У вас последняя версия UpdateManager (" + SelfIdentity.Version + ").");
                    break;

                case SelfUpdateStatus.Failed:
                    // При старте сервер может быть просто недоступен — не мешаем работать.
                    if (manual)
                        _view.ShowError("Не удалось проверить обновления:\n" + result.Error);
                    break;

                case SelfUpdateStatus.UpdateAvailable:
                    // Фоновая проверка закончилась посреди сборки/доставки — не всплываем поверх.
                    if (!manual && _view.IsBusy)
                        return;

                    var version = result.NewVersion.Length > 0 ? "Доступна версия " + result.NewVersion : "Доступно обновление";
                    if (_view.Confirm(version + " (у вас " + SelfIdentity.Version + ").\nОбновить сейчас?"))
                        Update();
                    break;
            }
        }

        private void Update()
        {
            var operation = new SelfUpdateOperation(_config);
            _view.ShowOperation(operation);

            if (operation.AlreadyUpToDate)
            {
                _view.ShowInfo("Обновлять нечего — у вас уже последняя версия.");
                return;
            }

            if (!operation.Succeeded)
                return; // ошибку (в т.ч. подписи) окно прогресса уже показало

            if (!_view.Confirm("Обновление скачано. Перезапустить UpdateManager сейчас?\n" +
                "(Если нет — обновиться можно позже: «Справка → Проверить обновления…».)"))
                return;

            try
            {
                operation.ApplyAndRestart();
            }
            catch (Exception ex)
            {
                _view.ShowError(ex.Message);
                return;
            }

            _view.CloseApplication(); // self-patcher ждёт выхода, чтобы подменить файлы
        }
    }
}
