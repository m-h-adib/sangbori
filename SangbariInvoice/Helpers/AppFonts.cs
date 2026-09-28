using System.Drawing.Text;

namespace SangbariInvoice.Helpers
{
    /// <summary>
    /// Loads a nicer, Persian-friendly font for the whole app if one is available,
    /// and quietly falls back to Tahoma otherwise so the app always runs even if
    /// no font file is present.
    ///
    /// HOW TO USE A CUSTOM FONT (recommended):
    ///   1. Download a free, redistributable Persian font - "Vazirmatn" is a good,
    ///      widely-used choice: https://github.com/rastikerdar/vazirmatn
    ///      (grab the "Vazirmatn-Regular.ttf" and, if you also want bold text to
    ///      use the real bold weight, "Vazirmatn-Bold.ttf").
    ///   2. Put the .ttf file(s) in the "Fonts" folder next to this file
    ///      (SangbariInvoice/Fonts). The project file is already set up to copy
    ///      anything in that folder to the output directory.
    ///   3. Run the app - AppFonts.Initialize() (called once from Program.cs)
    ///      picks up any .ttf file it finds there automatically.
    /// If the Fonts folder is empty or missing, the app simply uses Tahoma - no
    /// crash, no missing-file error.
    /// </summary>
    public static class AppFonts
    {
        private static readonly PrivateFontCollection Collection = new();
        private static bool _loaded;

        public static void Initialize()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var fontsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
                if (Directory.Exists(fontsDir))
                {
                    foreach (var file in Directory.GetFiles(fontsDir, "*.ttf"))
                    {
                        try { Collection.AddFontFile(file); }
                        catch { /* skip any file that isn't a valid font */ }
                    }
                }
            }
            catch
            {
                // Any problem scanning the Fonts folder -> just use Tahoma below.
            }
        }

        /// <summary>
        /// Returns a Font using the loaded custom font family if one was found,
        /// otherwise Tahoma. Use this everywhere instead of "new Font("Tahoma", ...)".
        /// </summary>
        public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
        {
            if (Collection.Families.Length > 0)
            {
                try
                {
                    return new Font(Collection.Families[0], size, style);
                }
                catch
                {
                    // Some font files don't support every style (e.g. no bold face) -
                    // fall through to Tahoma in that case.
                }
            }

            return new Font("Tahoma", size, style);
        }
    }
}
