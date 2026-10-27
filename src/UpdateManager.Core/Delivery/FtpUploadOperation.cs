using FluentFTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UpdateManager.Core.Operations;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Заливка папки (Output/ патча или набор файлов Android) на FTP через FluentFTP. Работает как фоновая
    /// операция движка (IEngineOperation): тот же UI прогресса, что у сборки/проверки. Передача в
    /// Binary-режиме — файлы патча (.lzdat) и apk бинарные, ASCII их повредит.
    /// </summary>
    public class FtpUploadOperation : IEngineOperation
    {
        private readonly FtpConnection _conn;
        private readonly string _localDir;
        private readonly string _remotePath;
        private readonly HashSet<string> _keepNames;
        private readonly string _title;

        private readonly Queue<string> _log = new Queue<string>();
        private readonly object _logLock = new object();

        private Thread _thread;
        private volatile bool _running;
        private volatile bool _cancelRequested;
        private volatile bool _succeeded;
        private volatile int _percent = -1;
        private string _details = "";
        private string _lastLoggedFile;

        // Полный протокол FTP (команды/ответы сервера) — в окно не выводится, при ошибке пишется в файл
        // FtpTraceFile для разбора. Пишется из потока заливки.
        private readonly List<string> _ftpTrace = new List<string>();

        /// <summary>Файл с протоколом FTP последней неудачной заливки.</summary>
        public static readonly string FtpTraceFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UpdateManager", "logs", "ftp-last-error.log");

        /// <param name="keepNames">Записи верхнего уровня боевой папки, которые сохраняются при замене
        /// (папка канала Android внутри папки патча). Всё остальное старое удаляется.</param>
        public FtpUploadOperation(FtpConnection conn, string localDir, string remotePath,
            IEnumerable<string> keepNames = null, string title = "Заливка на FTP")
        {
            _conn = conn;
            _localDir = localDir;
            _remotePath = remotePath;
            _keepNames = new HashSet<string>(keepNames ?? new string[0], StringComparer.OrdinalIgnoreCase);
            _title = title;
        }

        public string Title { get { return _title; } }

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
                    client.LegacyLogger = OnFtpTrace; // пароль FluentFTP в протоколе маскирует
                    try
                    {
                        client.Connect();
                    }
                    catch (Exception ex)
                    {
                        throw new FtpDeliveryException("Не удалось подключиться к " + _conn.Host + ":" + _conn.Port +
                            ". " + FtpErrorText.Describe(ex), ex);
                    }
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
                // В лог не пишем — окно операции само выведет ResultDetails под «завершилась с ошибкой».
                _details = FtpErrorText.Describe(ex);
                if (SaveFtpTrace())
                    _details += "\nПротокол FTP: " + FtpTraceFile;
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>
        /// Атомарная публикация. Льём всё в target_new и только после успешной заливки подменяем боевую
        /// папку переименованием (старое цело, если заливка оборвётся; на сервере не остаётся «полупатча»,
        /// а у Android подписанный манифест и его .sig появляются разом с остальными файлами). Старое
        /// содержимое боевой папки удаляется — кроме записей из keepNames (папка канала Android внутри папки
        /// патча): они переносятся в новую папку.
        ///
        /// Крах-безопасно: сохраняемое всегда либо в боевой папке, либо в резерве (target_old). В начале
        /// <see cref="RecoverFromInterruptedRun"/> возвращает его из резерва прошлого оборванного запуска.
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

            // Создаём временную папку заранее: нет прав — одна понятная ошибка, а не отказ на каждом файле.
            try
            {
                client.CreateDirectory(staging, true);
            }
            catch (Exception ex)
            {
                throw new FtpDeliveryException("Не удалось создать временную папку " + staging + ". " +
                    FtpErrorText.Describe(ex) + "\nДля публикации нужна запись в " + ParentOf(staging) +
                    " (рядом с папкой патча создаются " + staging + " и " + backup + ").", ex);
            }

            AppendLog("Заливка во временную папку " + staging + " …");
            UploadInto(client, staging);

            bool hadOld = client.DirectoryExists(target);
            if (!hadOld)
            {
                // Боевой папки ещё нет — просто публикуем.
                AppendLog("Публикация: " + staging + " → " + target);
                if (!client.MoveDirectory(staging, target, FtpRemoteExists.Overwrite))
                    throw new FtpDeliveryException("Не удалось переименовать " + staging + " в " + target + ".");
                return;
            }

            // Что сохранить из боевой папки (папка канала Android), если новая выкладка не принесла своё.
            var patchNames = TopLevelNames(client, staging);
            var untracked = KeptEntries(client, target, patchNames);

            // Подмена. Сначала уводим боевую папку в резерв (в нём цело и сохраняемое), затем staging → боевая.
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
                throw new FtpDeliveryException("Не удалось переименовать " + staging + " в " + target + ".");
            }

            // Возвращаем сохраняемое из резерва в новую боевую папку (переименованием — быстро, без пере-заливки).
            foreach (var item in untracked)
            {
                AppendLog("Сохраняю: " + item.Name);
                MoveEntry(client, item, backup + "/" + item.Name, target + "/" + item.Name);
            }

            // Остаток резерва — старое содержимое: удаляется.
            if (client.DirectoryExists(backup))
            {
                AppendLog("Удаляю старое: " + backup);
                client.DeleteDirectory(backup);
            }
        }

        /// <summary>
        /// Восстановление после оборванного прошлого запуска: резерв (target_old) существует, только если
        /// прошлый запуск ушёл в подмену, но не завершил её. Возвращаем в боевую папку сохраняемое, которого
        /// в ней нет, затем убираем резерв — начинаем с чистого состояния.
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

            // Боевая папка уже новая (крах после подмены, до возврата сохраняемого): вернём из резерва
            // сохраняемое (напр. AndroidPatch), которого в ней нет. Прочее в резерве — старое, удаляется.
            foreach (var item in KeptEntries(client, backup, TopLevelNames(client, target)))
                MoveEntry(client, item, backup + "/" + item.Name, target + "/" + item.Name);
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

        // Записи папки из keepNames, которых нет в новой выкладке, — их и сохраняем.
        private List<FtpListItem> KeptEntries(FtpClient client, string dir, HashSet<string> newNames)
        {
            var result = new List<FtpListItem>();
            foreach (var item in client.GetListing(dir))
                if (_keepNames.Contains(item.Name) && !newNames.Contains(item.Name))
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

            // Причина — по первому неудачному файлу; остальные обычно падают по той же причине.
            var failed = results.FindAll(r => r.IsFailed);
            if (failed.Count == 0)
                return;

            var first = failed[0];
            var more = failed.Count > 1 ? " (и ещё файлов: " + (failed.Count - 1) + ")" : "";
            throw new FtpDeliveryException("Не удалось залить " + first.Name + more + ". " +
                (first.Exception != null ? FtpErrorText.Describe(first.Exception) : ""), first.Exception);
        }

        // "/a/b_new" → "/a"; "/b_new" → "/".
        private static string ParentOf(string path)
        {
            var i = path.TrimEnd('/').LastIndexOf('/');
            return i <= 0 ? "/" : path.Substring(0, i);
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

        private void OnFtpTrace(FtpTraceLevel level, string message)
        {
            _ftpTrace.Add(message ?? "");
        }

        // Записать протокол FTP в файл. false — не вышло (не критично, ошибку покажем и без него).
        private bool SaveFtpTrace()
        {
            if (_ftpTrace.Count == 0)
                return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FtpTraceFile));
                File.WriteAllLines(FtpTraceFile, _ftpTrace);
                return true;
            }
            catch (Exception)
            {
                return false;
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
