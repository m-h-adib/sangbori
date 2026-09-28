using SangbariInvoice.Data;
using SangbariInvoice.Forms;
using SangbariInvoice.Helpers;

namespace SangbariInvoice
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Load the bundled Persian font (if any) and use it as the default
            // font for every control that doesn't set its own Font explicitly.
            AppFonts.Initialize();
            Application.SetDefaultFont(AppFonts.GetFont(9.5f));

            // Make sure the database and tables exist before any form opens.
            DatabaseHelper.Initialize();

            Application.Run(new MainForm());
        }
    }
}
