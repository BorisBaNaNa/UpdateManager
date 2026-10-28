using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Список недавно открытых проектов. Это настройка УРОВНЯ ПРИЛОЖЕНИЯ
    /// (а не проекта), поэтому хранится в профиле пользователя:
    /// %AppData%\UpdateManager\recent.xml.
    /// </summary>
    public class RecentProjectsStore
    {
        /// <summary>Сколько проектов помним максимум — три последних, чтобы список не разрастался.</summary>
        public const int MaxItems = 3;

        private const string AppFolderName = "UpdateManager";
        private const string FileName = "recent.xml";
        private const string RootElement = "RecentProjects";
        private const string ItemElement = "Project";

        private readonly string _filePath;

        public RecentProjectsStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName, FileName))
        {
        }

        /// <summary>Хранилище в произвольном файле (тесты).</summary>
        public RecentProjectsStore(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// Прочитать список путей (первый = самый свежий), не больше MaxItems — файл от прежних версий
        /// мог помнить больше. Нет файла — пустой список.
        /// </summary>
        public List<string> Load()
        {
            if (!File.Exists(_filePath))
                return new List<string>();

            return XDocument.Load(_filePath).Root
                .Elements(ItemElement)
                .Select(e => e.Value)
                .Take(MaxItems)
                .ToList();
        }

        /// <summary>
        /// Добавить проект наверх списка: убираем дубликат, ставим первым,
        /// обрезаем до MaxItems и сохраняем.
        /// </summary>
        public void Add(string projectPath)
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, projectPath);
            if (list.Count > MaxItems)
                list = list.Take(MaxItems).ToList();

            Save(list);
        }

        /// <summary>Убрать проект из списка (например, мёртвую запись на удалённую/перенесённую папку).</summary>
        public void Remove(string projectPath)
        {
            var list = Load();
            if (list.RemoveAll(p => string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase)) > 0)
                Save(list);
        }

        private void Save(List<string> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            new XDocument(
                new XElement(RootElement,
                    list.Select(p => new XElement(ItemElement, p))))
                .Save(_filePath);
        }
    }
}
