using System.Drawing;
using System.Windows.Forms;

namespace UpdateManager.Forms.Theming
{
    /// <summary>Цвета меню для рендерера — фон, подсветка, рамки из палитры.</summary>
    internal class ThemedColorTable : ProfessionalColorTable
    {
        private readonly ThemePalette _p;

        public ThemedColorTable(ThemePalette p)
        {
            _p = p;
            UseSystemColors = false;
        }

        public override Color ToolStripGradientBegin { get { return _p.MenuBack; } }
        public override Color ToolStripGradientMiddle { get { return _p.MenuBack; } }
        public override Color ToolStripGradientEnd { get { return _p.MenuBack; } }
        public override Color MenuStripGradientBegin { get { return _p.MenuBack; } }
        public override Color MenuStripGradientEnd { get { return _p.MenuBack; } }
        public override Color ToolStripDropDownBackground { get { return _p.MenuBack; } }
        public override Color ImageMarginGradientBegin { get { return _p.MenuBack; } }
        public override Color ImageMarginGradientMiddle { get { return _p.MenuBack; } }
        public override Color ImageMarginGradientEnd { get { return _p.MenuBack; } }
        public override Color MenuItemSelected { get { return _p.MenuHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return _p.MenuHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return _p.MenuHover; } }
        public override Color MenuItemPressedGradientBegin { get { return _p.MenuBack; } }
        public override Color MenuItemPressedGradientEnd { get { return _p.MenuBack; } }
        public override Color MenuItemBorder { get { return _p.Border; } }
        public override Color MenuBorder { get { return _p.Border; } }
        public override Color SeparatorDark { get { return _p.Border; } }
        public override Color SeparatorLight { get { return _p.Border; } }
    }

    /// <summary>Рендерер меню: фон из ColorTable, текст пунктов — цветом палитры (любой пункт, в т.ч. динамический).</summary>
    internal class ThemedToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemePalette _p;

        public ThemedToolStripRenderer(ThemePalette p) : base(new ThemedColorTable(p))
        {
            _p = p;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _p.Text;
            base.OnRenderItemText(e);
        }
    }
}
