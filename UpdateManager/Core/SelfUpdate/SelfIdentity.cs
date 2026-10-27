using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace UpdateManager.Core.SelfUpdate
{
    /// <summary>
    /// «Кто я»: продукт/разработчик/версия запущенного UpdateManager. Строки берутся из атрибутов
    /// сборки (AssemblyProduct/AssemblyCompany), а не дублируются в коде — переименование
    /// в AssemblyInfo не сломает распознавание.
    /// </summary>
    public static class SelfIdentity
    {
        private static readonly Assembly Self = typeof(SelfIdentity).Assembly;

        public static string ProductName
        {
            get { return Attribute<AssemblyProductAttribute>()?.Product ?? ""; }
        }

        public static string CompanyName
        {
            get { return Attribute<AssemblyCompanyAttribute>()?.Company ?? ""; }
        }

        /// <summary>FileVersion запущенного exe (как её видит VersionDetector в билде).</summary>
        public static string Version
        {
            get { return Attribute<AssemblyFileVersionAttribute>()?.Version ?? ""; }
        }

        /// <summary>Папка, из которой запущено приложение (корень для клиентского движка).</summary>
        public static string AppDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>Полный путь к запущенному exe (его перезапускает self-patcher).</summary>
        public static string ExecutablePath
        {
            get { return Self.Location; }
        }

        /// <summary>Указанный exe — это сборка UpdateManager (тот же продукт и разработчик)?</summary>
        public static bool IsSelf(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath) || ProductName.Length == 0)
                return false;

            FileVersionInfo info;
            try
            {
                info = FileVersionInfo.GetVersionInfo(exePath);
            }
            catch (Exception)
            {
                return false;
            }

            return string.Equals(info.ProductName, ProductName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(info.CompanyName, CompanyName, StringComparison.OrdinalIgnoreCase);
        }

        private static T Attribute<T>() where T : Attribute
        {
            return (T)System.Attribute.GetCustomAttribute(Self, typeof(T));
        }
    }
}
