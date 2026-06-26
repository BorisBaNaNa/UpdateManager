using System.Drawing;

namespace UpdateManager.Forms.Theming
{
    /// <summary>Режим темы: следовать Windows, светлая или тёмная.</summary>
    public enum AppTheme { System, Light, Dark }

    /// <summary>Набор цветов темы. Light повторяет системный вид, Dark — тёмная палитра.</summary>
    public class ThemePalette
    {
        public Color Window;       // фон формы
        public Color ControlBack;  // фон полей ввода/списков
        public Color Text;         // основной текст
        public Color DisabledText; // текст отключённых элементов
        public Color Border;       // рамки кнопок/разделители
        public Color Accent;       // выделение
        public Color AccentText;   // текст на выделении
        public Color MenuBack;     // фон меню
        public Color MenuHover;    // подсветка пункта меню
        public bool IsDark;        // тёмная ли (для рамки окна)

        public static readonly ThemePalette Light = new ThemePalette
        {
            Window = SystemColors.Control,
            ControlBack = SystemColors.Window,
            Text = SystemColors.ControlText,
            DisabledText = SystemColors.GrayText,
            Border = SystemColors.ControlDark,
            Accent = SystemColors.Highlight,
            AccentText = SystemColors.HighlightText,
            MenuBack = SystemColors.Control,
            MenuHover = SystemColors.MenuHighlight,
            IsDark = false
        };

        public static readonly ThemePalette Dark = new ThemePalette
        {
            Window = Color.FromArgb(32, 32, 32),
            ControlBack = Color.FromArgb(45, 45, 48),
            Text = Color.FromArgb(240, 240, 240),
            DisabledText = Color.FromArgb(140, 140, 140),
            Border = Color.FromArgb(63, 63, 70),
            Accent = Color.FromArgb(0, 122, 204),
            AccentText = Color.White,
            MenuBack = Color.FromArgb(45, 45, 48),
            MenuHover = Color.FromArgb(62, 62, 66),
            IsDark = true
        };
    }
}
