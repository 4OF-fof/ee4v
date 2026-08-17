using UnityEngine;

namespace Ee4v.UI
{
    public sealed class UiColorPalette
    {
        public UiColorPalette(
            Color32 chromeDeep,
            Color32 tabIdle,
            Color32 field,
            Color32 panel,
            Color32 surfaceRaised,
            Color32 control,
            Color32 toolActive,
            Color32 selection,
            Color32 focus,
            Color32 error,
            Color32 textPrimary,
            Color32 textSecondary,
            Color32 textMuted,
            Color32 textSoft,
            Color32 textDisabled,
            Color32 statusIdleText,
            Color32 statusRunningText,
            Color32 statusPassedText,
            Color32 statusFailedText,
            Color32 statusSkippedText,
            Color32 statusInconclusiveText)
        {
            ChromeDeep = chromeDeep;
            TabIdle = tabIdle;
            Field = field;
            Panel = panel;
            SurfaceRaised = surfaceRaised;
            Control = control;
            ToolActive = toolActive;
            Selection = selection;
            Focus = focus;
            Error = error;
            TextPrimary = textPrimary;
            TextSecondary = textSecondary;
            TextMuted = textMuted;
            TextSoft = textSoft;
            TextDisabled = textDisabled;
            StatusIdleText = statusIdleText;
            StatusRunningText = statusRunningText;
            StatusPassedText = statusPassedText;
            StatusFailedText = statusFailedText;
            StatusSkippedText = statusSkippedText;
            StatusInconclusiveText = statusInconclusiveText;
        }

        public Color32 ChromeDeep { get; }
        public Color32 TabIdle { get; }
        public Color32 Field { get; }
        public Color32 Panel { get; }
        public Color32 SurfaceRaised { get; }
        public Color32 Control { get; }
        public Color32 ToolActive { get; }
        public Color32 Selection { get; }
        public Color32 Focus { get; }
        public Color32 Error { get; }
        public Color32 TextPrimary { get; }
        public Color32 TextSecondary { get; }
        public Color32 TextMuted { get; }
        public Color32 TextSoft { get; }
        public Color32 TextDisabled { get; }
        public Color32 StatusIdleText { get; }
        public Color32 StatusRunningText { get; }
        public Color32 StatusPassedText { get; }
        public Color32 StatusFailedText { get; }
        public Color32 StatusSkippedText { get; }
        public Color32 StatusInconclusiveText { get; }
    }

    public static class UiColorPalettes
    {
        public static readonly UiColorPalette UnityDark =
            new UiColorPalette(
                chromeDeep: new Color32(25, 25, 25, 255),
                tabIdle: new Color32(40, 40, 40, 255),
                field: new Color32(42, 42, 42, 255),
                panel: new Color32(56, 56, 56, 255),
                surfaceRaised: new Color32(60, 60, 60, 255),
                control: new Color32(88, 88, 88, 255),
                toolActive: new Color32(70, 96, 124, 255),
                selection: new Color32(44, 93, 135, 255),
                focus: new Color32(58, 121, 187, 255),
                error: new Color32(220, 92, 92, 255),
                textPrimary: new Color32(194, 194, 194, 255),
                textSecondary: new Color32(194, 194, 194, 209),
                textMuted: new Color32(194, 194, 194, 184),
                textSoft: new Color32(194, 194, 194, 199),
                textDisabled: new Color32(194, 194, 194, 92),
                statusIdleText: new Color32(208, 208, 208, 255),
                statusRunningText: new Color32(234, 201, 108, 255),
                statusPassedText: new Color32(139, 215, 164, 255),
                statusFailedText: new Color32(231, 138, 138, 255),
                statusSkippedText: new Color32(166, 185, 238, 255),
                statusInconclusiveText: new Color32(198, 162, 236, 255));

    }

    public static class UiColorTokens
    {
        public static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        public static UiColorPalette Current => UiColorPalettes.UnityDark;
        public static Color32 ChromeDeep => Current.ChromeDeep;
        public static Color32 TabIdle => Current.TabIdle;
        public static Color32 Field => Current.Field;
        public static Color32 Panel => Current.Panel;
        public static Color32 SurfaceRaised => Current.SurfaceRaised;
        public static Color32 Control => Current.Control;
        public static Color32 ToolActive => Current.ToolActive;
        public static Color32 Selection => Current.Selection;
        public static Color32 Focus => Current.Focus;
        public static Color32 Error => Current.Error;
        public static Color32 TextPrimary => Current.TextPrimary;
        public static Color32 TextSecondary => Current.TextSecondary;
        public static Color32 TextMuted => Current.TextMuted;
        public static Color32 TextSoft => Current.TextSoft;
        public static Color32 TextDisabled => Current.TextDisabled;
        public static Color32 StatusIdleText => Current.StatusIdleText;
        public static Color32 StatusRunningText => Current.StatusRunningText;
        public static Color32 StatusPassedText => Current.StatusPassedText;
        public static Color32 StatusFailedText => Current.StatusFailedText;
        public static Color32 StatusSkippedText => Current.StatusSkippedText;
        public static Color32 StatusInconclusiveText => Current.StatusInconclusiveText;
    }
}
