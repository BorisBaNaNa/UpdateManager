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
        /// Атомарная публикация: льём в target_new, и только после успешной заливки подменяем
        /// боевую папку одним переименованием. Старый патч остаётся целым, если заливка оборвётся,
        /// а сама подмена не оставляет «полупатча» на сервере (пункт «оборванная заливка»).
        /// </summary>
        private void PublishAtomically(FtpClient client, string target)
        {
            var staging = target + "_new";
            var backup = target + "_old";

            // Хвосты прошлого оборванного запуска мешают — убираем.
            if (client.DirectoryExists(staging))
            {
                AppendLog("Удаляю остатки прошлой заливки: " + staging);
                client.DeleteDirectory(staging);
            }

            AppendLog("Заливка во временную папку " + staging + " …");
            UploadInto(client, staging);

            // Подмена. Сначала уводим боевую папку в backup, затем staging → боевая.
            bool hadOld = client.DirectoryExists(target);
            if (hadOld)
            {
                if (client.DirectoryExists(backup))
                    client.DeleteDirectory(backup);
                AppendLog("Замена: " + target + " → " + backup);
                client.MoveDirectory(target, backup, FtpRemoteExists.Overwrite);
            }

            AppendLog("Публикация: " + staging + " → " + target);
            if (!client.MoveDirectory(staging, target, FtpRemoteExists.Overwrite))
            {
                // Не удалось опубликовать новый патч — возвращаем старый на место.
                if (hadOld && !client.DirectoryExists(target) && client.DirectoryExists(backup))
                    client.MoveDirectory(backup, target, FtpRemoteExists.Overwrite);
                throw new Exception("Не удалось переименовать " + staging + " в " + target + ".");
            }

            // Новый патч на месте — удаляем старую копию.
            if (hadOld && client.DirectoryExists(backup))
            {
                AppendLog("Удаляю старый патч: " + backup);
                client.DeleteDirectory(backup);
            }
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
