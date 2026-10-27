using System;
using System.Net.Sockets;
using FluentFTP.Exceptions;
using UpdateManager.Core.Common;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Ошибка FTP-доставки, текст которой уже написан для пользователя (что делали + почему не вышло).
    /// <see cref="FtpErrorText.Describe"/> отдаёт его как есть, не разворачивая вложенные.
    /// </summary>
    public class FtpDeliveryException : Exception
    {
        public FtpDeliveryException(string message, Exception inner = null)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Короткое понятное объяснение ошибки FTP: одна фраза по-русски + ответ сервера в скобках.
    /// Вместо обёрток FluentFTP ("Error while uploading… See InnerException") — настоящая причина.
    /// </summary>
    public static class FtpErrorText
    {
        public static string Describe(Exception ex)
        {
            if (ex == null)
                return "";
            if (ex is FtpDeliveryException)
                return ex.Message;

            var command = Find<FtpCommandException>(ex);
            if (command != null)
                return ByCode(command.CompletionCode, command.Message);

            var socket = Find<SocketException>(ex);
            if (socket != null)
                return BySocket(socket);

            if (Find<TimeoutException>(ex) != null)
                return "Сервер не ответил вовремя (таймаут). Проверьте адрес и порт, запущен ли сервер.";

            return ExceptionText.Full(ex);
        }

        private static string ByCode(string code, string serverMessage)
        {
            // FluentFTP форматирует Message как "Code: 553 Message: Could not create file." — берём текст сервера.
            var text = (serverMessage ?? "").Trim();
            const string marker = "Message:";
            var at = text.IndexOf(marker, StringComparison.Ordinal);
            if (text.StartsWith("Code:", StringComparison.Ordinal) && at >= 0)
                text = text.Substring(at + marker.Length).Trim();

            var server = " (" + code + " " + text + ")";
            switch (code)
            {
                case "530":
                    return "Неверный логин или пароль" + server + ".";
                case "553":
                    return "Сервер не разрешил создать файл — у пользователя нет прав на запись в эту папку" + server + ".";
                case "550":
                    return "Нет доступа или папка/файл не найдены — проверьте путь и права пользователя" + server + ".";
                case "552":
                case "452":
                    return "На сервере закончилось место или превышена квота" + server + ".";
                case "425":
                case "426":
                    return "Не удалось открыть канал передачи данных — проверьте пассивный режим и брандмауэр" + server + ".";
                case "421":
                    return "Сервер закрыл соединение (перегружен или слишком много подключений)" + server + ".";
                default:
                    return "Сервер отказал" + server + ".";
            }
        }

        private static string BySocket(SocketException ex)
        {
            switch (ex.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    return "Сервер отказал в подключении — на этом адресе/порту FTP не запущен.";
                case SocketError.HostNotFound:
                case SocketError.NoData:
                    return "Сервер не найден — проверьте адрес.";
                case SocketError.TimedOut:
                case SocketError.HostUnreachable:
                case SocketError.NetworkUnreachable:
                    return "Сервер недоступен — проверьте адрес, сеть и брандмауэр.";
                case SocketError.ConnectionReset:
                case SocketError.ConnectionAborted:
                    return "Соединение с сервером оборвалось.";
                default:
                    return "Нет связи с сервером: " + ex.Message;
            }
        }

        private static T Find<T>(Exception ex) where T : Exception
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is T match)
                    return match;
            }
            return null;
        }
    }
}
