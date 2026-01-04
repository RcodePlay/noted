using Terminal.Gui;

namespace NoteD;

public class ThemeManager
{
    public static readonly Dictionary<string, Theme> Themes = new ();

    public class ThemeProxy
    {
        public required Dictionary<string, string> Main { get; set; }
        public Dictionary<string, string>? Dialog { get; set; }
    }
    
    public struct Theme
    {
        public ColorScheme Main;
        public ColorScheme Dialog;
    }

    public static ColorScheme ParseScheme(Dictionary<string, string> dict)
    {
        var scheme = new ColorScheme();

        Terminal.Gui.Attribute GetAttr(string key, Color defFg, Color defBg)
        {
            if (dict.TryGetValue(key, out var val))
            {
                var parts = val.Split('/');
                if (parts.Length == 2 &&
                    Enum.TryParse<Color>(parts[0], true, out var fg) &&
                    Enum.TryParse<Color>(parts[1], true, out var bg))
                    return Terminal.Gui.Attribute.Make(fg, bg);
            }
            return Terminal.Gui.Attribute.Make(defFg, defBg);
        }
        
        scheme.Normal = GetAttr("Normal", Color.White, Color.Blue);
        scheme.Focus = GetAttr("Focus", Color.Black, Color.Cyan);
        scheme.HotNormal = GetAttr("HotNormal", Color.BrightYellow, Color.Blue);
        scheme.HotFocus = GetAttr("HotFocus", Color.DarkGray, Color.Blue);
        return scheme;
    }

    public static void Initialize()
    {
        Themes["Matrix"] = new Theme
        {
            Main = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black),
                Focus = Application.Driver.MakeAttribute(Color.Green, Color.Black),
                HotNormal = Application.Driver.MakeAttribute(Color.White, Color.Black),
                HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black),
            },
            Dialog = Colors.Dialog
        };
        Themes["Classic"] = new Theme
        {
            Main = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Blue),
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Blue),
                HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Gray)
            },
            Dialog = new ColorScheme 
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                Focus = Application.Driver.MakeAttribute(Color.White, Color.DarkGray),
                HotNormal = Application.Driver.MakeAttribute(Color.Blue, Color.Gray),
                HotFocus = Application.Driver.MakeAttribute(Color.Blue, Color.DarkGray)
            }
        };
        Themes["Royal"] = new Theme
        {
            Main = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Blue),
                Focus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Cyan),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Cyan)
            }
        };
        Themes["Midnight"] = new Theme
        {
            Main = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black),
                Focus = Application.Driver.MakeAttribute(Color.White, Color.DarkGray),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightMagenta, Color.Black),
                HotFocus = Application.Driver.MakeAttribute(Color.BrightMagenta, Color.DarkGray)
            }
        };
        Themes["Light"] = new Theme
        {
            Main = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                Focus = Application.Driver.MakeAttribute(Color.White, Color.Blue),
                HotNormal = Application.Driver.MakeAttribute(Color.Red, Color.Gray),
                HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Blue)
            }
        };
    }

    public static void ApplyTheme(string themeName)
    {
        if (!Themes.TryGetValue(themeName, out var theme))
            return;
        
        Colors.Base = theme.Main;
        if (theme.Dialog != null)
            Colors.Dialog = Themes[themeName].Dialog;

        if (Application.Top != null)
        {
            UpdateViewColors(Application.Top, theme.Main);
            Application.Top.SetNeedsDisplay();
            Application.Refresh();
        }
    }
    
    private static void UpdateViewColors(View view, ColorScheme scheme)
    {
        view.ColorScheme = scheme;
        if (view.Subviews != null)
        {
            foreach (var subview in view.Subviews)
            {
                UpdateViewColors(subview, scheme);
            }
        }
    }
}