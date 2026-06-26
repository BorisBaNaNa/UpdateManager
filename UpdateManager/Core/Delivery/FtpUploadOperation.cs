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

        private readonly Queue<string> _log = new Queue<string>();
        private readonly object _logLock = new object();

        private Thread _thread;
        private volatile bool _running;
        private volatile bool _succeeded;
        private volatile int _percent = -1;
        private string _details = "";
        private string _lastLoggedFile;

        public FtpUploadOperation(FtpConnection conn, string localDir)
        {
            _conn = conn;
            _localDir = localDir;
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
                var remote = string.IsNullOrWhiteSpace(_conn.RemotePath) ? "/" : _conn.RemotePath;
                AppendLog("Подключение к " + _conn.Host + ":" + _conn.Port + " (" + _conn.Username + ")…");

                using (var client = new FtpClient(_conn.Host, _conn.Username, _conn.Password, _conn.Port))
                {
                    client.Config.UploadDataType = FtpDataType.Binary;
                    client.Connect();
                    AppendLog("Подключено. Заливка в " + remote + " …");

                    client.UploadDirectory(_localDir, remote, FtpFolderSyncMode.Update,
                        FtpRemoteExists.Overwrite, FtpVerify.None, null, OnProgress);
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
