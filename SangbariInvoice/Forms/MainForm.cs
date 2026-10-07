using System.Net.Http.Json;
using System.Text.Json;
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
            Width = 1000;
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
            _grid.ColumnHeadersHeight = 35;  // ارتفاع تیتر ستون‌ها
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += (_, _) => OpenSelectedInvoice();

            Controls.Add(_grid);
            Controls.Add(topPanel);
        }

        private void LoadGrid(string? search = null)
        {
            var invoices = DatabaseHelper.GetInvoices(search);

            _grid.DataSource = invoices.Select(inv => new
            {
                شماره_فاکتور = inv.InvoiceID,
                نام_مشتری = inv.CustomerName,
                موبایل = inv.Mobile,
                تاریخ = inv.InvoiceDate,
                جمع_کل = inv.TotalAmount.ToString("N0"),
                دریافتی = inv.ReceivedAmount.ToString("N0"),
                مانده = inv.RemainingBalance.ToString("N0"),
            }).ToList();
        }

        private int? GetSelectedInvoiceId()
        {
            if (_grid.CurrentRow == null) return null;
            return Convert.ToInt32(_grid.CurrentRow.Cells[0].Value);
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
            {
                LoadGrid(_txtSearch.Text);
            }
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


        public async Task<int> SendSmsMeliPayamak1(string mobile, decimal price,string date)
        {
            var base_url = "https://rest.payamak-panel.com/api/SendSMS/SendSMS";
            var username = "9133216308";
            var password = "63bd0a4a-fa0e-442b-87e5-1258c5857180";
            var to = mobile;
            var bodyId = "553097";

            var url = base_url;

            var client = new HttpClient();
            var json = new
            {
                username = username,
                password = password,
                bodyId = bodyId,
                to = to,
                from = "50004001216307",
                text = "با سلام"+Environment.NewLine+" مبلغ " + price + " تومان بدهی شما به سنگ المهدی تا تاریخ "+ date
            };

            var response = await client.PostAsJsonAsync(url, json);

            var result = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<object>(result);
            var element = (JsonElement)data;
            var status = element.GetProperty("RetStatus").GetInt32();

            return status;
        }

        public async Task<int> SendSmsMeliPayamak2(string mobile, string card,string sheba,string name)
        {
            var base_url = "https://rest.payamak-panel.com/api/SendSMS/BaseServiceNumber";
            var username = "9133216308";
            var password = "63bd0a4a-fa0e-442b-87e5-1258c5857180";
            var to = mobile;
            var bodyId = "553096";

            var url = base_url;

            var client = new HttpClient();
            var json = new
            {
                username = username,
                password = password,
                bodyId = bodyId,
                to = to,
                text = card+";"+sheba+";"+name
            };

            var response = await client.PostAsJsonAsync(url, json);

            var result = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<object>(result);
            var element = (JsonElement)data;
            var status = element.GetProperty("RetStatus").GetInt32();

            return status;
        }
    }
}
