using System.Globalization;
using MudBlazor;

namespace GameDevManager.Web.Theme;

/// <summary>
/// Zentrales Design des GameDevManagers: Anthrazit als Grundton, Gelb als einziger
/// kräftiger Akzent, durchgehend eckige Kanten (Border-Radius 0) und Inter als Schrift.
/// Farben werden ausschließlich hier definiert — Komponenten verwenden die
/// MudBlazor-Tokens (Color.Primary, var(--mud-palette-…)) und nie feste Hex-Werte.
/// </summary>
public static class GameDevManagerTheme
{
    /// <summary>Signaturgelb der Anwendung. Nur für Akzente, nie flächig.</summary>
    public const string Accent = "#FFC300";

    private static readonly string[] UiFontStack =
        ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    private static readonly string[] MonoFontStack =
        ["JetBrains Mono", "SFMono-Regular", "Consolas", "ui-monospace", "monospace"];

    public static readonly MudTheme Instance = new()
    {
        PaletteDark = BuildDarkPalette(),
        PaletteLight = BuildLightPalette(),
        Typography = BuildTypography(),
        LayoutProperties = new LayoutProperties
        {
            // Eckig: kein Radius auf Buttons, Cards, Inputs, Dialogen, …
            DefaultBorderRadius = "0px",
            AppbarHeight = "56px",
            DrawerWidthLeft = "260px",
            DrawerMiniWidthLeft = "56px"
        },
        Shadows = new Shadow { Elevation = BuildElevations() }
    };

    /// <summary>
    /// Dunkles Standard-Design. Der Anthrazit-Verlauf geht von der Appbar (am dunkelsten)
    /// über den Seitenhintergrund zur Card-Oberfläche (am hellsten), damit sich Flächen
    /// ohne Schlagschatten voneinander absetzen.
    /// </summary>
    private static PaletteDark BuildDarkPalette() => new()
    {
        Black = "#0B0D10",
        White = "#FFFFFF",

        Primary = Accent,
        PrimaryContrastText = "#16181C",
        PrimaryDarken = "#D9A500",
        PrimaryLighten = "#FFD449",

        Secondary = "#A7AEB9",
        SecondaryContrastText = "#16181C",
        SecondaryDarken = "#8B939F",
        SecondaryLighten = "#C3C9D2",

        Tertiary = "#6E7681",
        TertiaryContrastText = "#F2F4F7",

        Info = "#58A6FF",
        InfoContrastText = "#0B0D10",
        Success = "#3FB950",
        SuccessContrastText = "#0B0D10",
        // Bewusst orange statt gelb — sonst wäre „Warnung" nicht vom Akzent zu unterscheiden.
        Warning = "#F0883E",
        WarningContrastText = "#16181C",
        Error = "#E5484D",
        ErrorContrastText = "#FFFFFF",

        Background = "#16181C",
        BackgroundGray = "#101215",
        Surface = "#1D2026",

        AppbarBackground = "#101215",
        AppbarText = "#E6E9ED",
        DrawerBackground = "#101215",
        DrawerText = "#D6DAE0",
        DrawerIcon = "#9AA1AC",

        TextPrimary = "#E6E9ED",
        TextSecondary = "#9AA1AC",
        TextDisabled = "rgba(230,233,237,0.36)",

        ActionDefault = "#9AA1AC",
        ActionDisabled = "rgba(230,233,237,0.28)",
        ActionDisabledBackground = "rgba(230,233,237,0.10)",

        Divider = "#2A2E36",
        DividerLight = "#22262D",
        LinesDefault = "#2A2E36",
        LinesInputs = "#3A414C",

        TableLines = "#2A2E36",
        TableStriped = "rgba(255,255,255,0.022)",
        TableHover = "rgba(255,195,0,0.07)",

        GrayDefault = "#6E7681",
        GrayLight = "#8B939F",
        GrayLighter = "#A7AEB9",
        GrayDark = "#4A515B",
        GrayDarker = "#343A42",

        OverlayDark = "rgba(11,13,16,0.78)",
        OverlayLight = "rgba(29,32,38,0.60)",

        HoverOpacity = 0.07,
        RippleOpacity = 0.08,
        RippleOpacitySecondary = 0.16
    };

    /// <summary>
    /// Helles Gegenstück. Die Appbar bleibt auch hier anthrazit, damit das Produkt in
    /// beiden Modi gleich wirkt. Das Gelb wird für Text/Outlines abgedunkelt, weil das
    /// Signalgelb auf Weiß nicht ausreichend kontrastiert.
    /// </summary>
    private static PaletteLight BuildLightPalette() => new()
    {
        Black = "#0B0D10",
        White = "#FFFFFF",

        Primary = "#8F6E00",
        PrimaryContrastText = "#FFFFFF",
        PrimaryDarken = "#6B5200",
        PrimaryLighten = Accent,

        Secondary = "#4A515B",
        SecondaryContrastText = "#FFFFFF",
        SecondaryDarken = "#343A42",
        SecondaryLighten = "#6E7681",

        Tertiary = "#6E7681",
        TertiaryContrastText = "#FFFFFF",

        Info = "#0B69C7",
        Success = "#1A7F37",
        Warning = "#B45309",
        Error = "#C62A2F",

        Background = "#F3F4F6",
        BackgroundGray = "#E9EBEE",
        Surface = "#FFFFFF",

        AppbarBackground = "#16181C",
        AppbarText = "#E6E9ED",
        DrawerBackground = "#FFFFFF",
        DrawerText = "#1A1D21",
        DrawerIcon = "#4A515B",

        TextPrimary = "#16181C",
        TextSecondary = "#5A616B",
        TextDisabled = "rgba(22,24,28,0.38)",

        ActionDefault = "#5A616B",
        ActionDisabled = "rgba(22,24,28,0.26)",
        ActionDisabledBackground = "rgba(22,24,28,0.08)",

        Divider = "#D8DBE0",
        DividerLight = "#E5E7EB",
        LinesDefault = "#D8DBE0",
        LinesInputs = "#B9BEC6",

        TableLines = "#D8DBE0",
        TableStriped = "rgba(22,24,28,0.02)",
        TableHover = "rgba(255,195,0,0.14)",

        GrayDefault = "#6E7681",
        GrayLight = "#8B939F",
        GrayLighter = "#C3C9D2",
        GrayDark = "#4A515B",
        GrayDarker = "#343A42",

        HoverOpacity = 0.06
    };

    /// <summary>
    /// Inter über alle Stufen, engere Laufweite in den Überschriften. Buttons bleiben in
    /// Groß-/Kleinschreibung (kein Uppercase) — das ist der ruhigere, modernere Look.
    /// </summary>
    private static Typography BuildTypography() => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = UiFontStack,
            FontSize = "0.875rem",
            FontWeight = "400",
            LineHeight = "1.5",
            LetterSpacing = "normal"
        },
        H1 = Heading<H1Typography>("2.5rem", "700", "1.15", "-0.022em"),
        H2 = Heading<H2Typography>("2rem", "700", "1.18", "-0.02em"),
        H3 = Heading<H3Typography>("1.625rem", "600", "1.22", "-0.018em"),
        H4 = Heading<H4Typography>("1.375rem", "600", "1.28", "-0.015em"),
        H5 = Heading<H5Typography>("1.125rem", "600", "1.35", "-0.012em"),
        H6 = Heading<H6Typography>("1rem", "600", "1.4", "-0.01em"),
        Subtitle1 = new Subtitle1Typography
        {
            FontFamily = UiFontStack,
            FontSize = "0.9375rem",
            FontWeight = "500",
            LineHeight = "1.5",
            LetterSpacing = "normal"
        },
        Subtitle2 = new Subtitle2Typography
        {
            FontFamily = UiFontStack,
            FontSize = "0.8125rem",
            FontWeight = "500",
            LineHeight = "1.5",
            LetterSpacing = "normal"
        },
        Body1 = new Body1Typography
        {
            FontFamily = UiFontStack,
            FontSize = "0.9375rem",
            FontWeight = "400",
            LineHeight = "1.6",
            LetterSpacing = "normal"
        },
        Body2 = new Body2Typography
        {
            FontFamily = UiFontStack,
            FontSize = "0.8125rem",
            FontWeight = "400",
            LineHeight = "1.55",
            LetterSpacing = "normal"
        },
        Button = new ButtonTypography
        {
            FontFamily = UiFontStack,
            FontSize = "0.8125rem",
            FontWeight = "500",
            LineHeight = "1.75",
            LetterSpacing = "0.01em",
            TextTransform = "none"
        },
        Caption = new CaptionTypography
        {
            FontFamily = UiFontStack,
            FontSize = "0.75rem",
            FontWeight = "400",
            LineHeight = "1.45",
            LetterSpacing = "normal"
        },
        // Einzige Stelle mit Versalien: kleine technische Labels (Sektionsköpfe, Meta-Zeilen).
        Overline = new OverlineTypography
        {
            FontFamily = UiFontStack,
            FontSize = "0.6875rem",
            FontWeight = "600",
            LineHeight = "1.6",
            LetterSpacing = "0.09em",
            TextTransform = "uppercase"
        }
    };

    private static T Heading<T>(string size, string weight, string lineHeight, string tracking)
        where T : BaseTypography, new() => new()
    {
        FontFamily = UiFontStack,
        FontSize = size,
        FontWeight = weight,
        LineHeight = lineHeight,
        LetterSpacing = tracking
    };

    /// <summary>
    /// Flache Elevations: harte, dunkle Schatten mit wenig Streuung statt weicher Wolken —
    /// passend zu den eckigen Kanten. Index 0 ist immer schattenlos, MudBlazor erwartet 26 Stufen.
    /// </summary>
    private static string[] BuildElevations()
    {
        var elevations = new string[26];
        elevations[0] = "none";

        for (var i = 1; i < elevations.Length; i++)
        {
            var offsetY = Math.Min(1 + (i / 4), 8);
            var blur = Math.Min(2 + (i * 2), 32);
            var alpha = Math.Min(0.18 + (i * 0.014), 0.55);

            elevations[i] = string.Create(
                CultureInfo.InvariantCulture,
                $"0px {offsetY}px {blur}px rgba(0,0,0,{alpha:0.00})");
        }

        return elevations;
    }

    /// <summary>Schriftstapel für monospaced Ausgaben (GUIDs, IDs, JSON-Vorschauen).</summary>
    public static string MonoFontFamily => string.Join(", ", MonoFontStack.Select(Quote));

    private static string Quote(string family) => family.Contains(' ') ? $"'{family}'" : family;
}
