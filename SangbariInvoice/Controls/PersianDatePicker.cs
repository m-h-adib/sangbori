using SangbariInvoice.Helpers;

namespace SangbariInvoice.Controls
{
    /// <summary>
    /// A read-only textbox paired with a small button that opens a Persian calendar
    /// popup. This replaces free-typed date fields everywhere in the app.
    /// </summary>
    public class PersianDatePicker : UserControl
    {
        private readonly TextBox _txt = new();
        private readonly Button _btn = new();

        public event EventHandler? DateChanged;

        public string DateText
        {
            get => _txt.Text;
            set { _txt.Text = value; DateChanged?.Invoke(this, EventArgs.Empty); }
        }

        public PersianDatePicker()
        {
            Height = 24;
            Width = 200;

            _txt.ReadOnly = true;
            _txt.Dock = DockStyle.Fill;
            _txt.TextAlign = HorizontalAlignment.Right;
            _txt.BackColor = Color.White;
            _txt.Cursor = Cursors.Hand;
            _txt.Click += (_, _) => OpenPicker();

            _btn.Text = "📅";
            _btn.Dock = DockStyle.Right;
            _btn.Width = 30;
            _btn.FlatStyle = FlatStyle.Flat;
            _btn.Click += (_, _) => OpenPicker();

            Controls.Add(_txt);
            Controls.Add(_btn);
        }

        private void OpenPicker()
        {
            using var popup = new PersianCalendarPopup(_txt.Text);
            if (popup.ShowDialog(FindForm()) == DialogResult.OK)
            {
                DateText = popup.SelectedDate;
            }
        }

        public bool IsValidDate => PersianDateHelper.IsValid(_txt.Text);
    }
}
