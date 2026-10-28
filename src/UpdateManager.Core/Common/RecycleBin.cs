using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UpdateManager.Core.Common
{
    /// <summary>
    /// Удаление в Корзину Windows (SHFileOperation с FOF_ALLOWUNDO): удалённое по ошибке можно вернуть.
    /// Не помещается в Корзину — Windows сама спросит про окончательное удаление.
    /// </summary>
    public static class RecycleBin
    {
        private const uint FoDelete = 0x0003;
        private const ushort FofSilent = 0x0004;
        private const ushort FofNoConfirmation = 0x0010;
        private const ushort FofAllowUndo = 0x0040;
        private const ushort FofWantNukeWarning = 0x4000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileOpStruct
        {
            public IntPtr Hwnd;
            public uint Func;
            public string From;
            public string To;
            public ushort Flags;
            public bool AnyOperationsAborted;
            public IntPtr NameMappings;
            public string ProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref ShFileOpStruct operation);

        /// <summary>Отправить папку в Корзину. Бросает, если не вышло или пользователь отменил.</summary>
        public static void DeleteDirectory(string path)
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            if (!Directory.Exists(full))
                throw new DirectoryNotFoundException("Папка не найдена: " + full);

            var operation = new ShFileOpStruct
            {
                Func = FoDelete,
                From = full + "\0\0", // список путей — через \0, в конце двойной \0
                Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofWantNukeWarning
            };

            var result = SHFileOperation(ref operation);
            if (operation.AnyOperationsAborted)
                throw new OperationCanceledException("Удаление отменено.");
            if (result != 0 || Directory.Exists(full))
                throw new IOException("Не удалось удалить папку " + full + " (код " + result + "). " +
                    "Возможно, файлы открыты в другой программе.");
        }
    }
}
