using SangbariInvoice.Data;
using SangbariInvoice.Helpers;

namespace SangbariInvoice.Forms
{
    public class MainForm : Form
    {
        private readonly DataGridView _grid = new();
        private readonly TextBox _txtSearch = new();
        private readonly Button _btnSearch = new();
        private readonly Button _btnNew = new();
        private readonly Button _btnOpen = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnRefresh = new();
        private readonly Button _btnExcel = new();

        public MainForm()
        {
            BuildUi();
            LoadGrid();
        }

        private void BuildUi()
        {
            Text = "نرم‌افزار فاکتور سنگبری";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.GetFont(9.5f);
            Width = 1100;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 50 };

            var lblSearch = new Label
            {
                Text = "جستجو (نام یا موبایل):",
                AutoSize = true,
                Location = new Point(810, 15)
            };

            _txtSearch.Location = new Point(620, 12);
            _txtSearch.Width = 180;

            _btnSearch.Text = "جستجو";
            _btnSearch.Location = new Point(530, 10);
            _btnSearch.Width = 80;
            _btnSearch.Height = 35;
            _btnSearch.Click += (_, _) => LoadGrid(_txtSearch.Text);

            _btnRefresh.Text = "نمایش همه";
            _btnRefresh.Location = new Point(430, 10);
            _btnRefresh.Width = 90;
            _btnRefresh.Height = 35;
            _btnRefresh.Click += (_, _) => { _txtSearch.Text = ""; LoadGrid(); };

            _btnNew.Text = "فاکتور جدید";
            _btnNew.Location = new Point(320, 10);
            _btnNew.Width = 100;
            _btnNew.Height = 35;
            _btnNew.Click += (_, _) => OpenInvoiceForm(null);

            _btnOpen.Text = "ویرایش";
            _btnOpen.Location = new Point(220, 10);
            _btnOpen.Width = 90;
            _btnOpen.Height = 35;
            _btnOpen.Click += (_, _) => OpenSelectedInvoice();

            _btnDelete.Text = "حذف";
            _btnDelete.Location = new Point(130, 10);
            _btnDelete.Width = 80;
            _btnDelete.Height = 35;
            _btnDelete.Click += (_, _) => DeleteSelectedInvoice();

            _btnExcel.Text = "خروجی اکسل";
            _btnExcel.Location = new Point(20, 10);
            _btnExcel.Width = 100;
            _btnExcel.Height = 35;
            _btnExcel.Click += (_, _) => ExportListToExcel();

            topPanel.Controls.AddRange(new Control[]
            {
                lblSearch, _txtSearch, _btnSearch, _btnRefresh, _btnNew, _btnOpen, _btnDelete, _btnExcel
            });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.ColumnHeadersHeight = 35;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                    _grid.Columns[e.ColumnIndex].Name != "Sms1Button" &&
                    _grid.Columns[e.ColumnIndex].Name != "Sms2Button")
                {
                    OpenSelectedInvoice();
                }
            };
            _grid.CellContentClick += Grid_CellContentClick;

            Controls.Add(_grid);
            Controls.Add(topPanel);
        }

        private void LoadGrid(string? search = null)
        {
            var invoices = DatabaseHelper.GetInvoices(search);
            InvoiceSummaryHelper.Populate(invoices);

            _grid.DataSource = invoices.Select(inv => new
            {
                InvoiceId = inv.InvoiceID,
                شماره= inv.InvoiceID,
                نام_مشتری = inv.CustomerName,
                موبایل = inv.Mobile,
                تاریخ = inv.InvoiceDate,
                ابزار = inv.ToolAmount.ToString("N0"),
                متر_مربع = inv.SquareMeter.ToString("0.###"),
                جمع_کل = inv.TotalAmount.ToString("N0"),
                دریافتی = inv.ReceivedAmount.ToString("N0"),
                مانده = inv.RemainingBalance.ToString("N0"),
                SmsMobile = inv.Mobile,
                SmsBalance = inv.RemainingBalance,
                SmsDate = inv.InvoiceDate,
                SmsName = inv.CustomerName,
            }).ToList();

            foreach (var name in new[] { "InvoiceId", "SmsMobile", "SmsBalance", "SmsDate", "SmsName" })
            {
                if (_grid.Columns.Contains(name))
                    _grid.Columns[name].Visible = false;
            }

            AddSmsButtonColumn("Sms1Button", "پیامک1");
            AddSmsButtonColumn("Sms2Button", "پیامک2");
        }

        private void AddSmsButtonColumn(string name, string header)
        {
            if (_grid.Columns.Contains(name))
                return;

            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = name,
                HeaderText = header,
                Text = header,
                UseColumnTextForButtonValue = true,
                Width = 115,
                MinimumWidth = 105,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
        }

        private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = _grid.Rows[e.RowIndex];
            var columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName != "Sms1Button" && columnName != "Sms2Button")
                return;

            var mobile = Convert.ToString(row.Cells["SmsMobile"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(mobile))
            {
                MessageBox.Show("شماره موبایل مشتری ثبت نشده است.", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var customerName = Convert.ToString(row.Cells["SmsName"].Value) ?? "";
            var prompt = columnName == "Sms1Button"
                ? $"پیامک بدهی برای {customerName} به شماره {mobile} ارسال شود؟"
                : $"پیامک اطلاعات کارت و شبا برای {customerName} به شماره {mobile} ارسال شود؟";

            if (MessageBox.Show(prompt, "تأیید ارسال", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var settings = SmsAppSettings.Load();
                string message;

                if (columnName == "Sms1Button")
                {
                    var balance = Convert.ToDecimal(row.Cells["SmsBalance"].Value);
                    var date = Convert.ToString(row.Cells["SmsDate"].Value) ?? "";
                    message = "با سلام" + Environment.NewLine +
                              "مبلغ " + balance.ToString("N0") +
                              " تومان بدهی شما به سنگ المهدی تا تاریخ " + date;
                }
                else
                {
                    message = settings.BuildCardAndShebaMessage(customerName);
                }

                await HuaweiHiLinkSmsService.SendAsync(
                    settings.ModemAddress,
                    mobile,
                    message);

                MessageBox.Show("پیامک با موفقیت به مودم تحویل داده شد و پذیرش آن توسط شبکه تأیید شد.",
                    "نتیجه ارسال", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ارسال پیامک ناموفق بود: {ex.Message}", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private int? GetSelectedInvoiceId()
        {
            if (_grid.CurrentRow == null)
                return null;

            var value = _grid.CurrentRow.Cells["InvoiceId"]?.Value;
            if (value == null || !int.TryParse(Convert.ToString(value), out var invoiceId))
                return null;

            return invoiceId;
        }

        private void OpenSelectedInvoice()
        {
            var id = GetSelectedInvoiceId();
            if (id == null)
            {
                MessageBox.Show("ابتدا یک فاکتور را از لیست انتخاب کنید.", "توجه",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenInvoiceForm(id);
        }

        private void OpenInvoiceForm(int? invoiceId)
        {
            using var form = new InvoiceForm(invoiceId);
            if (form.ShowDialog(this) == DialogResult.OK)
                LoadGrid(_txtSearch.Text);
        }

        private void DeleteSelectedInvoice()
        {
            var id = GetSelectedInvoiceId();
            if (id == null)
            {
                MessageBox.Show("ابتدا یک فاکتور را از لیست انتخاب کنید.", "توجه",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show($"آیا از حذف فاکتور شماره {id} مطمئن هستید؟",
                "تایید حذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                DatabaseHelper.DeleteInvoice(id.Value);
                LoadGrid(_txtSearch.Text);
            }
        }

        private void ExportListToExcel()
        {
            var invoices = DatabaseHelper.GetInvoices(_txtSearch.Text);
            if (invoices.Count == 0)
            {
                MessageBox.Show("فهرستی برای خروجی گرفتن وجود ندارد.", "توجه",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "لیست فاکتورها.xlsx"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    ExcelExportHelper.ExportInvoiceList(invoices, sfd.FileName);
                    MessageBox.Show("خروجی اکسل با موفقیت ذخیره شد.", "موفق",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در ذخیره فایل اکسل: {ex.Message}", "خطا",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /*
         * روش‌های قبلی ارسال پیامک از طریق سامانه ملی‌پیامک عمداً غیرفعال شده‌اند.
         * ارسال جدید از طریق HuaweiHiLinkSmsService و API داخلی مودم انجام می‌شود.
         *
         * public async Task<int> SendSmsMeliPayamak1(...) { ... }
         * public async Task<int> SendSmsMeliPayamak2(...) { ... }
         */
    }
}
