using System;

namespace SlipManagement2
{
    // Single source of truth for paper size profiles.
    // Previously this lookup was duplicated in FirstTimeSetupForm, PrinterSettingsForm and
    // SlipPrintEngine (Defect Log DEF-012) — three independent copies that could silently
    // drift apart if only one was edited. All three now call in here.
    public static class PaperSizeHelper
    {
        // Order matters — this is the order the options appear in every paper-size dropdown.
        public static readonly string[] ProfileNames =
        {
            "A4", "A5", "A6", "Letter", "Small151x151", "Small240x102"
        };

        public const string DefaultProfile = "Small240x102";

        // The custom dot-matrix profile is the only one whose height comes from the
        // operator-entered slip length rather than a fixed standard size.
        public const string CustomLengthProfile = "Small240x102";

        public static bool UsesCustomLength(string paperProfile)
            => paperProfile == CustomLengthProfile;

        // Returns the page dimensions in millimetres for a paper size profile.
        // slipLengthIn is only used by Small240x102; the fixed sizes ignore it.
        // An unrecognised profile falls back to the custom 240mm-wide roll rather than
        // to A4 — silently defaulting to A4 was the original "always prints A4, wastes
        // paper" complaint this system exists to fix.
        public static (double widthMm, double heightMm) GetDimensionsMm(
            string paperProfile, double slipLengthIn)
        {
            switch (paperProfile)
            {
                case "A4":           return (210,   297);
                case "A5":           return (148,   210);
                case "A6":           return (105,   148);
                case "Letter":       return (215.9, 279.4);
                case "Small151x151": return (151,   151);
                case "Small240x102":
                default:             return (240,   slipLengthIn * 25.4);
            }
        }
    }
}
