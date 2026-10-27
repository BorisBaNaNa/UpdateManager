using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using UpdateManager.Core.Security;

namespace UpdateManager.Core.Operations
{
    /// <summary>
    /// Проверка канала Android: по публичному адресу скачать каждый выложенный файл и сверить байты
    /// с папкой билда, а у подписанных — что .sig на сервере сходится с публичным ключом проекта.
    /// Ловит недовыкладку, старый файл на сервере и чужой .sig. Адреса file:/// читаются с диска (тесты).
    /// </summary>
    public class AndroidChannelVerifier : IEngineOperation
    {
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

        private readonly string _channelUrl;
        private readonly string _buildFolder;
        private readonly IList<string> _files;
        private readonly IList<string> _signed;
        private readonly string _publicKeyXml;

        private readonly Queue<string> _log = new Queue<string>();
        private readonly object _logLock = new object();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        private Thread _thread;
        private volatile bool _running;
        private volatile bool _succeeded;
        private volatile int _percent = -1;
        private string _details = "";

        /// <param name="channelUrl">Публичный адрес папки канала (со «/» в конце).</param>
        /// <param name="files">Выложенные файлы (относительные пути с «/»), включая .sig.</param>
        /// <param name="signed">Подписанные файлы из них.</param>
        public AndroidChannelVerifier(string channelUrl, string buildFolder, IList<string> files, IList<string> signed,
            string publicKeyXml)
        {
            _channelUrl = channelUrl.EndsWith("/") ? channelUrl : channelUrl + "/";
            _buildFolder = buildFolder;
            _files = files;
            _signed = signed;
            _publicKeyXml = publicKeyXml;
        }

        public string Title { get { return "Проверка канала Android (скачивание)"; } }

        public bool Start()
        {
            if (_running)
                return false;

            _running = true;
            _thread = new Thread(Run) { IsBackground = true };
            _thread.Start();
            return true;
        }

        public void Cancel()
        {
            _cancel.Cancel();
        }

        private void Run()
        {
            try
            {
                using (var http = new HttpClient { Timeout = RequestTimeout })
                {
                    var downloaded = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < _files.Count; i++)
                    {
                        var file = _files[i];
                        AppendLog("↓ " + file);
                        var remote = Download(http, file);
                        var local = File.ReadAllBytes(Path.Combine(_buildFolder, file));
                        if (!remote.SequenceEqual(local))
                            throw new InvalidOperationException("На сервере не тот файл: " + file +
                                " (" + remote.Length + " байт, в папке билда " + local.Length + "). Выложите заново.");
                        downloaded[file] = remote;
                        _percent = (int)(100.0 * (i + 1) / _files.Count);
                    }

                    foreach (var file in _signed)
                    {
                        byte[] signature;
                        if (!downloaded.TryGetValue(file + FileSigner.SignatureExtension, out signature))
                            throw new InvalidOperationException("Нет подписи " + file + FileSigner.SignatureExtension +
                                " в выкладке — клиент не примет обновление.");
                        if (!FileSigner.Verify(downloaded[file], System.Text.Encoding.UTF8.GetString(signature), _publicKeyXml))
                            throw new InvalidOperationException("Подпись " + file + FileSigner.SignatureExtension +
                                " на сервере не сходится с публичным ключом проекта.");
                        AppendLog("Подпись сходится: " + file);
                    }
                }

                _succeeded = true;
                AppendLog("Канал в порядке: файлов " + _files.Count + ", подписей " + _signed.Count + ".");
            }
            catch (OperationCanceledException)
            {
                _succeeded = false;
                _details = _cancel.IsCancellationRequested ? "Прервано пользователем."
                    : "Таймаут " + (int)RequestTimeout.TotalSeconds + " с: сервер не ответил (" + _channelUrl + ").";
            }
            catch (HttpRequestException ex)
            {
                _succeeded = false;
                _details = "Сервер недоступен или файл не найден: " + ex.Message;
            }
            catch (Exception ex)
            {
                _succeeded = false;
                _details = ex.Message;
            }
            finally
            {
                _running = false;
            }
        }

        private byte[] Download(HttpClient http, string file)
        {
            var url = new Uri(_channelUrl + string.Join("/", file.Split('/').Select(Uri.EscapeDataString)));
            if (url.IsFile)
            {
                if (!File.Exists(url.LocalPath))
                    throw new HttpRequestException("нет файла " + url);
                return File.ReadAllBytes(url.LocalPath);
            }

            using (var response = http.GetAsync(url, _cancel.Token).GetAwaiter().GetResult())
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException((int)response.StatusCode + " " + response.ReasonPhrase + " — " + url);
                return response.Content.ReadAsByteArrayAsync(_cancel.Token).GetAwaiter().GetResult();
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
            var p = _percent;
            return p >= 0 ? p : (int?)null;
        }

        public bool IsRunning
        {
            get { return _running; }
        }

        public bool Succeeded
        {
            get { return _succeeded; }
        }

        public string ResultDetails
        {
            get { return _succeeded ? "" : _details; }
        }
    }
}
