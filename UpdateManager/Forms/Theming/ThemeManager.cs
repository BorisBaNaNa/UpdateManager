using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UpdateManager.Forms.Theming
{
    /// <summary>
    /// Темизация WinForms «вручную»: рекурсивно красит контролы формы по палитре, меню — через
    /// общий рендерер, тёмную рамку окна — через DWM. Режим (System/Light/Dark) хранится в профиле
    /// пользователя. System читает текущую тему Windows из реестра.
    /// </summary>
    public static class ThemeManager
    {
        private const string AppFolderName = "UpdateManager";
        private const string FileName = "theme.txt";
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private static AppTheme _mode = AppTheme.System;
        private static readonly HashSet<ListView> _themedHeaders = new HashSet<ListView>();
        private static readonly HashSet<Button> _disabledTextButtons = new HashSet<Button>();

        public static AppTheme Mode { get { return _mode; } }

        /// <summary>Вызвать в Program.Main до создания форм: грузит режим и ставит рендерер меню.</summary>
        public static void Init()
        {
            _mode = LoadMode();
            ToolStripManager.Renderer = new ThemedToolStripRenderer(Effective());
        }

        /// <summary>Действующая палитра (System разрешается через тему Windows).</summary>
        public static ThemePalette Effective()
        {
            switch (_mode)
            {
                case AppTheme.Light: return ThemePalette.Light;
                case AppTheme.Dark: return ThemePalette.Dark;
                default: return WindowsUsesLightTheme() ? ThemePalette.Light : ThemePalette.Dark;
            }
        }

        /// <summary>Сменить режим: сохранить, переставить рендерер, переприменить ко всем открытым окнам.</summary>
        public static void SetMode(AppTheme mode)
        {
            _mode = mode;
            SaveMode(mode);
            ToolStripManager.Renderer = new ThemedToolStripRenderer(Effective());
            foreach (Form f in Application.OpenForms)
                Apply(f);
        }

        /// <summary>Подключить форму — тема применится на её Load.</summary>
        public static void Register(Form form)
        {
            form.Load += (s, e) => Apply(form);
        }

        public static void Apply(Form form)
        {
            var p = Effective();
            form.BackColor = p.Window;
            form.ForeColor = p.Text;
            ApplyToControls(form.Controls, p);
            ApplyTitleBar(form, p.IsDark);
            form.Invalidate(true);
        }

        private static void ApplyToControls(Control.ControlCollection controls, ThemePalette p)
        {
            foreach (Control c in controls)
            {
                bool recurse = ApplyToControl(c, p);
                if (recurse && c.Controls.Count > 0)
                    ApplyToControls(c.Controls, p);
            }
        }

        // true = красить детей рекурсивно; false = это «лист» (внутренности трогать нельзя).
        private static bool ApplyToControl(Control c, ThemePalette p)
        {
            if (c is MenuStrip)
            {
                c.BackColor = p.MenuBack;
                c.ForeColor = p.Text;
                return false; // пункты красит рендерер
            }

            var button = c as Button;
            if (button != null)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = p.ControlBack;
                button.ForeColor = p.Text;
                button.FlatAppearance.BorderColor = p.Border;
                // У disabled-кнопки WinForms рисует текст «вытравленным» цветом от фона (наш ForeColor
                // игнорируется) — в тёмной теме он сливается. Дорисовываем читаемый текст поверх.
                if (_disabledTextButtons.Add(button))
                    button.Paint += OnButtonPaint;
                return false;
            }

            var lv = c as ListView;
            if (lv != null)
            {
                lv.BackColor = p.ControlBack;
                lv.ForeColor = p.Text;
                EnableThemedHeader(lv);
                return false;
            }

            if (c is TextBoxBase || c is ComboBox || c is ListBox || c is NumericUpDown || c is TreeView)
            {
                c.BackColor = p.ControlBack;
                c.ForeColor = p.Text;
                return false; // у этих свои внутренние контролы — не рекурсим
            }

            // Label, CheckBox, RadioButton, GroupBox, Panel, ProgressBar и пр.
            c.BackColor = p.Window;
            c.ForeColor = p.Text;
            return true;
        }

        // Поверх «вытравленного» disabled-текста рисуем читаемый приглушённым цветом.
        private static void OnButtonPaint(object sender, PaintEventArgs e)
        {
            var button = (Button)sender;
            if (button.Enabled)
                return;

            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle,
                Effective().DisabledText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // Заголовки колонок ListView системой не темизируются — рисуем сами (подписка один раз).
        private static void EnableThemedHeader(ListView lv)
        {
            if (lv.View != View.Details || !_themedHeaders.Add(lv))
                return;

            lv.OwnerDraw = true;
            lv.DrawColumnHeader += (s, e) =>
            {
                var p = Effective();
                using (var back = new SolidBrush(p.MenuBack))
                    e.Graphics.FillRectangle(back, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, lv.Font, e.Bounds, p.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
                using (var pen = new Pen(p.Border))
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            };
            lv.DrawItem += (s, e) => { e.DrawDefault = true; };
            lv.DrawSubItem += (s, e) => { e.DrawDefault = true; };
        }

        private static void ApplyTitleBar(Form form, bool dark)
        {
            if (!form.IsHandleCreated)
                return;

            int value = dark ? 1 : 0;
            foreach (int attr in new[] { DWMWA_USE_IMMERSIVE_DARK_MODE, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD })
            {
                try { DwmSetWindowAttribute(form.Handle, attr, ref value, sizeof(int)); }
                catch { /* старая Windows без поддержки — рамка останется системной */ }
            }
        }

        private static bool WindowsUsesLightTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    if (value != null)
                        return Convert.ToInt32(value) != 0;
                }
            }
            catch { /* нет ключа — считаем светлой */ }
            return true;
        }

        // --- хранение режима в %AppData%\UpdateManager\theme.txt ---

        private static string FilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName, FileName);
        }

        private static AppTheme LoadMode()
        {
            try
            {
                var path = FilePath();
                if (File.Exists(path))
                {
                    AppTheme mode;
                    if (Enum.TryParse(File.ReadAllText(path).Trim(), out mode))
                        return mode;
                }
            }
            catch { }
            return AppTheme.System;
        }

        private static void SaveMode(AppTheme mode)
        {
            try
            {
                var path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, mode.ToString());
            }
            catch { }
        }
    }
}
