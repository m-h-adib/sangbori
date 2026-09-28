using SangbariInvoice.Controls;
using SangbariInvoice.Helpers;
using SangbariInvoice.Models;

namespace SangbariInvoice.Forms
{
    /// <summary>
    /// Small dialog for managing an invoice's list of received payments
    /// ("وجوه دریافتی").
    /// </summary>
    public class PaymentsForm : Form
    {
        private readonly DataGridView _grid = new();
        private readonly Label _lblSum = new();

        private const int ColDate = 0;
        private const int ColType = 1;
        private const int ColAmount = 2;
        private const int ColCheckNumber = 3;
        private const int ColCheckDueDate = 4;
        private const int ColDescription = 5;

        private const string TypeCash = "نقد";
        private const string TypeCheck = "چک";

        public List<PaymentEntry> Payments { get; private set; } = new();

        public PaymentsForm(List<PaymentEntry> existing)
        {
            BuildUi();

            foreach (var p in existing)
                AddRow(p);

            if (_grid.Rows.Count == 0)
                AddRow(new PaymentEntry());

            UpdateSum();
        }

        private void BuildUi()
        {
            Text = "مدیریت وجوه دریافتی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.GetFont(9.5f);
            Width = 800;
            Height = 470;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            _grid.Location = new Point(20, 15);
            _grid.Size = new Size(740, 300);
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersWidth = 30;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // تاریخ دریافت
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "تاریخ دریافت",
                Name = "PaymentDate",
                ReadOnly = true,
                DefaultCellStyle = { BackColor = Color.WhiteSmoke }
            });

            // نوع پرداخت
            var typeColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = "نوع پرداخت",
                Name = "PaymentType",
                FlatStyle = FlatStyle.Flat,
                DropDownWidth = 90
            };

            typeColumn.Items.Add(TypeCash);
            typeColumn.Items.Add(TypeCheck);

            _grid.Columns.Add(typeColumn);

            // مبلغ
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "مبلغ",
                Name = "Amount"
            });

            // شماره چک
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "شماره چک",
                Name = "CheckNumber"
            });

            // تاریخ وصول چک
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "تاریخ وصول چک",
                Name = "CheckDueDate",
                ReadOnly = true,
                DefaultCellStyle = { BackColor = Color.LightGray }
            });

            // توضیحات
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "توضیحات",
                Name = "Description"
            });

            _grid.CellClick += Grid_CellClick;
            _grid.CellEndEdit += (_, _) => UpdateSum();
            _grid.CellValueChanged += Grid_CellValueChanged;

            _grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var btnAdd = new Button
            {
                Text = "افزودن پرداخت",
                Location = new Point(660, 325),
                Width = 100
            };

            btnAdd.Click += (_, _) => AddRow(new PaymentEntry());

            var btnDelete = new Button
            {
                Text = "حذف پرداخت",
                Location = new Point(550, 325),
                Width = 100
            };

            btnDelete.Click += (_, _) =>
            {
                if (_grid.CurrentRow != null && !_grid.CurrentRow.IsNewRow)
                {
                    _grid.Rows.Remove(_grid.CurrentRow);
                    UpdateSum();
                }
            };

            _lblSum.Location = new Point(20, 365);
            _lblSum.AutoSize = true;
            _lblSum.Font = AppFonts.GetFont(10.5f, FontStyle.Bold);

            var btnOk = new Button
            {
                Text = "تایید",
                Location = new Point(660, 405),
                Width = 100,
                Height = 32
            };

            btnOk.Click += (_, _) => CommitAndClose();

            var btnCancel = new Button
            {
                Text = "انصراف",
                Location = new Point(550, 405),
                Width = 100,
                Height = 32
            };

            btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(_grid);
            Controls.Add(btnAdd);
            Controls.Add(btnDelete);
            Controls.Add(_lblSum);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }

        private void AddRow(PaymentEntry entry)
        {
            int idx = _grid.Rows.Add();
            var row = _grid.Rows[idx];

            row.Cells[ColDate].Value =
                string.IsNullOrWhiteSpace(entry.PaymentDate)
                    ? PersianDateHelper.Today()
                    : entry.PaymentDate;

            row.Cells[ColType].Value =
                string.IsNullOrWhiteSpace(entry.PaymentType)
                    ? TypeCash
                    : entry.PaymentType;

            row.Cells[ColAmount].Value =
                entry.Amount == 0
                    ? ""
                    : entry.Amount.ToString("0.##");

            row.Cells[ColCheckNumber].Value =
                entry.CheckNumber ?? "";

            row.Cells[ColCheckDueDate].Value =
                entry.CheckDueDate ?? "";

            row.Cells[ColDescription].Value =
                entry.Description ?? "";

            ApplyRowTypeState(row);
        }

        private void Grid_CellClick(
            object? sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            var row = _grid.Rows[e.RowIndex];

            // تاریخ دریافت
            if (e.ColumnIndex == ColDate)
            {
                var current =
                    row.Cells[ColDate].Value?.ToString() ?? "";

                using var popup =
                    new PersianCalendarPopup(current);

                if (popup.ShowDialog(this) == DialogResult.OK)
                {
                    row.Cells[ColDate].Value =
                        popup.SelectedDate;
                }

                return;
            }

            // تاریخ وصول چک
            if (e.ColumnIndex == ColCheckDueDate)
            {
                var type =
                    row.Cells[ColType].Value?.ToString() ?? TypeCash;

                // فقط برای چک
                if (type != TypeCheck)
                    return;

                var current =
                    row.Cells[ColCheckDueDate].Value?.ToString() ?? "";

                using var popup =
                    new PersianCalendarPopup(current);

                if (popup.ShowDialog(this) == DialogResult.OK)
                {
                    row.Cells[ColCheckDueDate].Value =
                        popup.SelectedDate;
                }
            }
        }

        private void Grid_CellValueChanged(
            object? sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (e.ColumnIndex == ColType)
            {
                ApplyRowTypeState(
                    _grid.Rows[e.RowIndex]);
            }
        }

        private void ApplyRowTypeState(DataGridViewRow row)
        {
            var type =
                row.Cells[ColType].Value?.ToString()
                ?? TypeCash;

            bool isCheck = type == TypeCheck;

            // شماره چک
            var checkCell =
                row.Cells[ColCheckNumber];

            checkCell.ReadOnly = !isCheck;
            checkCell.Style.BackColor =
                isCheck ? Color.White : Color.LightGray;

            if (!isCheck)
                checkCell.Value = "";

            // تاریخ وصول چک
            var dueDateCell =
                row.Cells[ColCheckDueDate];

            dueDateCell.ReadOnly = !isCheck;
            dueDateCell.Style.BackColor =
                isCheck ? Color.WhiteSmoke : Color.LightGray;

            if (!isCheck)
                dueDateCell.Value = "";
        }

        private void UpdateSum()
        {
            decimal sum = 0;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                    continue;

                sum += ParseDecimal(
                    row.Cells[ColAmount].Value);
            }

            _lblSum.Text =
                $"مجموع وجوه دریافتی: {sum:N0}";
        }

        private void CommitAndClose()
        {
            var list = new List<PaymentEntry>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                    continue;

                var amount =
                    ParseDecimal(row.Cells[ColAmount].Value);

                var dateText =
                    row.Cells[ColDate].Value?.ToString() ?? "";

                var typeText =
                    row.Cells[ColType].Value?.ToString()
                    ?? TypeCash;

                var checkNumber =
                    row.Cells[ColCheckNumber].Value?.ToString()
                    ?? "";

                var checkDueDate =
                    row.Cells[ColCheckDueDate].Value?.ToString()
                    ?? "";

                var description =
                    row.Cells[ColDescription].Value?.ToString()
                    ?? "";

                if (amount == 0 &&
                    string.IsNullOrWhiteSpace(dateText) &&
                    string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                list.Add(new PaymentEntry
                {
                    PaymentDate = dateText,
                    Amount = amount,
                    PaymentType = typeText,

                    CheckNumber =
                        typeText == TypeCheck
                            ? checkNumber
                            : "",

                    CheckDueDate =
                        typeText == TypeCheck
                            ? checkDueDate
                            : "",

                    Description = description
                });
            }

            Payments = list;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static decimal ParseDecimal(object? value)
        {
            if (value == null)
                return 0;

            var text = value.ToString()?.Trim();

            if (string.IsNullOrEmpty(text))
                return 0;

            return decimal.TryParse(
                text,
                out var result)
                ? result
                : 0;
        }
    }
}