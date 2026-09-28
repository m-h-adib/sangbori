using SangbariInvoice.Helpers;

namespace SangbariInvoice.Controls

{
    /// <summary>
    /// A small popup form showing a Persian (Shamsi) calendar grid.
    /// Shown with ShowDialog(); on OK, SelectedDate contains "yyyy/MM/dd".
    /// </summary>
    public class PersianCalendarPopup : Form
    {
        private readonly ComboBox _cboYear = new();
        private readonly ComboBox _cboMonth = new();
        private readonly TableLayoutPanel _daysPanel = new();

        private int _year;
        private int _month;

        public string SelectedDate { get; private set; } = "";

        public PersianCalendarPopup(string? initialDate = null)
        {
            int day;
            if (!string.IsNullOrWhiteSpace(initialDate) && PersianDateHelper.TryParse(initialDate, out _year, out _month, out day))
            {
                // parsed successfully into _year/_month
            }
            else
            {
                var today = PersianDateHelper.TodayParts();
                _year = today.Year;
                _month = today.Month;
            }

            BuildUi();
            PopulateHeaderCombos();
            BuildDaysGrid();
        }

        private void BuildUi()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Text = "انتخاب تاریخ";
            Width = 400;
            Height = 400;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = AppFonts.GetFont(9.5f);

            _cboYear.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboYear.Location = new Point(160, 10);
            _cboYear.Width = 90;
            _cboYear.SelectedIndexChanged += (_, _) =>
            {
                _year = int.Parse(_cboYear.SelectedItem!.ToString()!);
                BuildDaysGrid();
            };

            _cboMonth.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboMonth.Location = new Point(40, 10);
            _cboMonth.Width = 110;
            _cboMonth.SelectedIndexChanged += (_, _) =>
            {
                _month = _cboMonth.SelectedIndex + 1;
                BuildDaysGrid();
            };

            _daysPanel.Location = new Point(10, 45);
            _daysPanel.Size = new Size(365, 310);
            _daysPanel.ColumnCount = 7;
            _daysPanel.RowCount = 7;
            for (int i = 0; i < 7; i++)
                _daysPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
            for (int i = 0; i < 7; i++)
                _daysPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7));

            Controls.Add(_cboYear);
            Controls.Add(_cboMonth);
            Controls.Add(_daysPanel);
        }

        private void PopulateHeaderCombos()
        {
            var currentYear = PersianDateHelper.TodayParts().Year;
            for (int y = currentYear - 10; y <= currentYear + 5; y++)
                _cboYear.Items.Add(y.ToString());
            _cboYear.SelectedItem = _year.ToString();

            _cboMonth.Items.AddRange(PersianDateHelper.MonthNames);
            _cboMonth.SelectedIndex = _month - 1;
        }

        private void BuildDaysGrid()
        {
            _daysPanel.Controls.Clear();

            string[] weekDayHeaders = { "ش", "ی", "د", "س", "چ", "پ", "ج" };
            for (int i = 0; i < 7; i++)
            {
                var lbl = new Label
                {
                    Text = weekDayHeaders[i],
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = new Font(Font, FontStyle.Bold)
                };
                _daysPanel.Controls.Add(lbl, i, 0);
            }

            int offset = PersianDateHelper.FirstDayOfMonthWeekIndex(_year, _month);
            int daysInMonth = PersianDateHelper.DaysInMonth(_year, _month);

            int col = offset;
            int row = 1;

            for (int day = 1; day <= daysInMonth; day++)
            {
                int capturedDay = day;
                var btn = new Button
                {
                    Text = day.ToString(),
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                };
                btn.Click += (_, _) =>
                {
                    SelectedDate = $"{_year:0000}/{_month:00}/{capturedDay:00}";
                    DialogResult = DialogResult.OK;
                    Close();
                };
                _daysPanel.Controls.Add(btn, col, row);

                col++;
                if (col > 6) { col = 0; row++; }
            }
        }
    }
}
