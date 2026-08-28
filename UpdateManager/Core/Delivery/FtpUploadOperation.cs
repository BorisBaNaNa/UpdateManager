using FluentFTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UpdateManager.Core.Operations;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Заливка содержимого Output/ на FTP через FluentFTP. Работает как фоновая операция движка
    /// (IEngineOperation): тот же UI прогресса, что у сборки/проверки. Передача в Binary-режиме —
    /// файлы патча (.lzdat) бинарные, ASCII их повредит.
    /// </summary>
    public class FtpUploadOperation : IEngineOperation
    {
        private readonly FtpConnection _conn;
        private readonly string _localDir;
        private readonly string _remotePath;

        private readonly Queue<string> _log = new Queue<string>();
        private readonly object _logLock = new object();

        private Thread _thread;
        private volatile bool _running;
        private volatile bool _cancelRequested;
        private volatile bool _succeeded;
        private volatile int _percent = -1;
        private string _details = "";
        private string _lastLoggedFile;

        public FtpUploadOperation(FtpConnection conn, string localDir, string remotePath)
        {
            _conn = conn;
            _localDir = localDir;
            _remotePath = remotePath;
        }

        public string Title { get { return "Заливка на FTP"; } }

        public bool Start()
        {
            if (_running)
                return false;

            _running = true;
            _thread = new Thread(Run) { IsBackground = true };
            _thread.Start();
            return true;
        }

        // FluentFTP синхронные методы токен отмены не принимают, поэтому прерываем кооперативно:
        // ставим флаг, а OnProgress (зовётся движком по ходу заливки) бросает исключение.
        public void Cancel()
        {
            _cancelRequested = true;
        }

        private void Run()
        {
            try
            {
                var target = (_remotePath ?? "").Trim().TrimEnd('/');
                AppendLog("Подключение к " + _conn.Host + ":" + _conn.Port + " (" + _conn.Username + ")…");

                using (var client = new FtpClient(_conn.Host, _conn.Username, _conn.Password, _conn.Port))
                {
                    client.Config.UploadDataType = FtpDataType.Binary;
                    client.Connect();
                    AppendLog("Подключено.");

                    // target="" — заливка в корень FTP. Атомарный обмен там невозможен (нельзя
                    // переименовать корень), поэтому льём напрямую как раньше.
                    if (target.Length == 0)
                        UploadInto(client, "/");
                    else
                        PublishAtomically(client, target);
                }

                _percent = 100;
                _succeeded = true;
                AppendLog("Заливка завершена.");
            }
            catch (OperationCanceledException)
            {
                _succeeded = false;
                _details = "Прервано пользователем.";
                AppendLog("Заливка прервана.");
            }
            catch (Exception ex)
            {
                _succeeded = false;
                _details = ex.Message;
                AppendLog("Ошибка заливки: " + ex.Message);
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>
        /// Атомарная публикация С СОХРАНЕНИЕМ неотслеживаемых файлов/папок. Льём патч в target_new и
        /// только после успешной заливки подменяем боевую папку переименованием (старый патч цел, если
        /// заливка оборвётся; на сервере не остаётся «полупатча»). При этом всё, что лежит в боевой папке,
        /// но НЕ входит в новый патч (напр. вручную созданная AndroidPatch — apk + latest.json), переносится
        /// в новую папку, а не удаляется вместе со старым патчем.
        ///
        /// Крах-безопасно: неотслеживаемое всегда либо в боевой папке, либо в резерве (target_old). В начале
        /// <see cref="RecoverFromInterruptedRun"/> сверяет резерв прошлого оборванного запуска с боевой папкой
        /// и возвращает недостающее — поэтому обрыв в момент подмены неотслеживаемое не теряет.
        /// </summary>
        private void PublishAtomically(FtpClient client, string target)
        {
            var staging = target + "_new";
            var backup = target + "_old";

            // Сначала — восстановление после прошлого оборванного запуска (резерв мог остаться и хранить
            // неотслеживаемое). Только ПОСЛЕ этого безопасно удалять staging.
            RecoverFromInterruptedRun(client, target, backup);

            // Хвосты прошлого оборванного запуска мешают — убираем.
            if (client.DirectoryExists(staging))
            {
                AppendLog("Удаляю остатки прошлой заливки: " + staging);
                client.DeleteDirectory(staging);
            }

            AppendLog("Заливка во временную папку " + staging + " …");
            UploadInto(client, staging);

            bool hadOld = client.DirectoryExists(target);
            if (!hadOld)
            {
                // Боевой папки ещё нет — просто публикуем.
                AppendLog("Публикация: " + staging + " → " + target);
                if (!client.MoveDirectory(staging, target, FtpRemoteExists.Overwrite))
                    throw new Exception("Не удалось переименовать " + staging + " в " + target + ".");
                return;
            }

            // Что в боевой папке НЕ относится к новому патчу — сохраняем (перенесём после подмены из резерва).
            var patchNames = TopLevelNames(client, staging);
            var untracked = UntrackedEntries(client, target, patchNames);

            // Подмена. Сначала уводим боевую папку в резерв (в нём цело и неотслеживаемое), затем staging → боевая.
            if (client.DirectoryExists(backup))
                client.DeleteDirectory(backup);
            AppendLog("Замена: " + target + " → " + backup);
            client.MoveDirectory(target, backup, FtpRemoteExists.Overwrite);

            AppendLog("Публикация: " + staging + " → " + target);
            if (!client.MoveDirectory(staging, target, FtpRemoteExists.Overwrite))
            {
                // Не удалось опубликовать новый патч — возвращаем старую папку целиком (с неотслеживаемым) на место.
                if (!client.DirectoryExists(target) && client.DirectoryExists(backup))
                    client.MoveDirectory(backup, target, FtpRemoteExists.Overwrite);
                throw new Exception("Не удалось переименовать " + staging + " в " + target + ".");
            }

            // Возвращаем неотслеживаемое из резерва в новую боевую папку (переименованием — быстро, без пере-заливки).
            foreach (var item in untracked)
            {
                AppendLog("Сохраняю: " + item.Name);
                MoveEntry(client, item, backup + "/" + item.Name, target + "/" + item.Name);
            }

            // Остаток резерва — только старые файлы патча.
            if (client.DirectoryExists(backup))
            {
                AppendLog("Удаляю старый патч: " + backup);
                client.DeleteDirectory(backup);
            }
        }

        /// <summary>
        /// Восстановление после оборванного прошлого запуска: резерв (target_old) существует, только если
        /// прошлый запуск ушёл в подмену, но не завершил её. Возвращаем в боевую папку то, чего в ней нет
        /// (неотслеживаемое цело), затем убираем резерв — начинаем с чистого состояния.
        /// </summary>
        private void RecoverFromInterruptedRun(FtpClient client, string target, string backup)
        {
            if (!client.DirectoryExists(backup))
                return;

            AppendLog("Обнаружен резерв прошлого запуска — восстанавливаю: " + backup);
            if (!client.DirectoryExists(target))
            {
                // Крах между «боевая → резерв» и «staging → боевая»: возвращаем всю папку целиком.
                client.MoveDirectory(backup, target, FtpRemoteExists.Overwrite);
                return;
            }

            // Боевая папка уже новая (крах после подмены, до возврата неотслеживаемого): вернём из резерва
            // лишь то, чего в ней нет (напр. AndroidPatch). Совпавшие по имени — устаревшие файлы патча, их не трогаем.
            var targetNames = TopLevelNames(client, target);
            foreach (var item in client.GetListing(backup))
            {
                if (targetNames.Contains(item.Name))
                    continue;
                MoveEntry(client, item, backup + "/" + item.Name, target + "/" + item.Name);
            }
            client.DeleteDirectory(backup);
        }

        // Перенести запись (файл или папку) переименованием.
        private static void MoveEntry(FtpClient client, FtpListItem item, string from, string to)
        {
            if (item.Type == FtpObjectType.Directory)
                client.MoveDirectory(from, to, FtpRemoteExists.Overwrite);
            else
                client.MoveFile(from, to, FtpRemoteExists.Overwrite);
        }

        // Имена записей верхнего уровня папки (пустой набор, если папки нет).
        private static HashSet<string> TopLevelNames(FtpClient client, string dir)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (client.DirectoryExists(dir))
                foreach (var item in client.GetListing(dir))
                    set.Add(item.Name);
            return set;
        }

        // Записи боевой папки, которых нет в новом патче, — их и сохраняем (неотслеживаемое).
        private static List<FtpListItem> UntrackedEntries(FtpClient client, string target, HashSet<string> patchNames)
        {
            var result = new List<FtpListItem>();
            foreach (var item in client.GetListing(target))
                if (!patchNames.Contains(item.Name))
                    result.Add(item);
            return result;
        }

        // Залить Output/ в указанную папку и убедиться, что все файлы дошли (иначе подменять нечем).
        private void UploadInto(FtpClient client, string remoteFolder)
        {
            var results = client.UploadDirectory(_localDir, remoteFolder, FtpFolderSyncMode.Update,
                FtpRemoteExists.Overwrite, FtpVerify.None, null, OnProgress);

            if (results == null)
                return;

            foreach (var r in results)
            {
                if (!r.IsFailed)
                    continue;
                var reason = r.Exception != null ? ": " + r.Exception.Message : "";
                throw new Exception("Не удалось залить файл " + r.Name + reason);
            }
        }

        private void OnProgress(FtpProgress p)
        {
            if (_cancelRequested)
                throw new OperationCanceledException();

            // Новый файл — логируем его имя один раз.
            if (p.LocalPath != null && p.LocalPath != _lastLoggedFile)
            {
                _lastLoggedFile = p.LocalPath;
                AppendLog("→ " + Path.GetFileName(p.LocalPath));
            }

            // Общий процент по числу файлов + прогресс текущего файла.
            if (p.FileCount > 0)
            {
                double fileProgress = p.Progress >= 0 ? Math.Min(p.Progress, 100.0) : 0.0;
                double overall = 100.0 * (p.FileIndex + fileProgress / 100.0) / p.FileCount;
                _percent = (int)Math.Max(0, Math.Min(100, overall));
            }
        }

        private void AppendLog(string line)
        {
            lock (_logLock)
                _log.Enqueue(line);
        }

        public string FetchLog()
        {
            lock (_logLock)
                return _log.Count > 0 ? _log.Dequeue() : null;
        }

        public int? FetchProgressPercentage()
        {
            int p = _percent;
            return p < 0 ? (int?)null : p;
        }

        public bool IsRunning { get { return _running; } }

        public bool Succeeded { get { return _succeeded; } }

        public string ResultDetails { get { return _details; } }
    }
}
