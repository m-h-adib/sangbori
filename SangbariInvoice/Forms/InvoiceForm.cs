using SangbariInvoice.Controls;
using SangbariInvoice.Data;
using SangbariInvoice.Helpers;
using SangbariInvoice.Models;

namespace SangbariInvoice.Forms
{
    public class InvoiceForm : Form
    {
        private readonly int? _invoiceId;
        private Invoice _invoice = new();

        // Customer / invoice header controls
        private readonly TextBox _txtMobile = new();
        private readonly TextBox _txtName = new();
        private readonly TextBox _txtPhone = new();
        private readonly TextBox _txtAddress = new();
        private readonly TextBox _txtReferrer = new();
        private readonly PersianDatePicker _dtInvoiceDate = new();

        // Items grid
        private readonly DataGridView _grid = new();

        // Footer / totals
        private readonly Label _lblTotal = new();
        private readonly Label _lblReceivedTotal = new();
        private readonly Label _lblRemaining = new();

        // Column order:
        // تاریخ | مرجوعی | شرح سنگ | تعداد*فی | تعداد | ابزار | عرض | طول | مترمربع | فی | مجموع
        private const int ColRowDate = 0;
        private const int ColIsReturn = 1;
        private const int ColDesc = 2;
        private const int ColIsQtyPrice = 3;
        private const int ColQuantity = 4;
        private const int ColIsTool = 5;
        private const int ColWidth = 6;
        private const int ColLength = 7;
        private const int ColSquareMeter = 8;
        private const int ColUnitPrice = 9;
        private const int ColLineTotal = 10;

        public InvoiceForm(int? invoiceId)
        {
            _invoiceId = invoiceId;
            BuildUi();

            if (_invoiceId.HasValue)
            {
                _invoice = DatabaseHelper.GetInvoiceById(_invoiceId.Value);
                LoadInvoiceIntoForm();
            }
            else
            {
                _dtInvoiceDate.DateText = PersianDateHelper.Today();
            }

            RecalculateTotals();

            FormClosing += InvoiceForm_FormClosing;
        }

        private void InvoiceForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                e.Cancel = true;

                MessageBox.Show(
                    $"خطا در ذخیره فاکتور:\n{ex.Message}",
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // -----------------------------------------------------------
        // UI construction
        // -----------------------------------------------------------
        private void BuildUi()
        {
            Text = _invoiceId.HasValue ? $"ویرایش فاکتور شماره {_invoiceId}" : "فاکتور جدید";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.GetFont(9.5f);
            Width = 1080;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;

            // ---- Header (customer info) ----
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 130 };

            AddLabelAndTextBox(headerPanel, "موبایل:", _txtMobile, 900, 15);
            _txtMobile.Leave += (_, _) => TrySearchCustomer();

            AddLabelAndTextBox(headerPanel, "نام و نام‌خانوادگی:", _txtName, 900, 45);
            AddLabelAndTextBox(headerPanel, "کد ملی:", _txtPhone, 900, 75);
            AddLabelAndTextBox(headerPanel, "آدرس:", _txtAddress, 450, 45, wide: true);
            AddLabelAndTextBox(headerPanel, "نام معرف:", _txtReferrer, 450, 75);

            var lblInvDate = new Label { Text = "تاریخ فاکتور (شمسی):", AutoSize = true, Location = new Point(430, 18) };
            _dtInvoiceDate.Location = new Point(230, 15);
            _dtInvoiceDate.Width = 200;
            headerPanel.Controls.Add(lblInvDate);
            headerPanel.Controls.Add(_dtInvoiceDate);

            var lblHint = new Label
            {
                Text = "* با تایپ شماره موبایل و خروج از فیلد، اطلاعات مشتری از فاکتورهای قبلی جستجو می‌شود.",
                AutoSize = true,
                ForeColor = Color.Gray,
                Location = new Point(30, 105)
            };
            headerPanel.Controls.Add(lblHint);

            // ---- Items grid ----
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersWidth = 30;
            _grid.ColumnHeadersHeight = 35;  // ارتفاع تیتر ستون‌ها
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            BuildGridColumns();
            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellClick += Grid_CellClick;
            _grid.PreviewKeyDown += Grid_PreviewKeyDown;
            _grid.KeyDown += Grid_KeyDown;


            var gridButtonsPanel = new Panel { Dock = DockStyle.Top, Height = 40 };
            var btnAddRow = new Button { Text = "افزودن ردیف", Location = new Point(900, 5), Width = 100,Height=35 };
            btnAddRow.Click += (_, _) => AddEmptyRow();
            var btnDeleteRow = new Button { Text = "حذف ردیف", Location = new Point(790, 5), Width = 100, Height = 35 };
            btnDeleteRow.Click += (_, _) => DeleteSelectedRow();
            gridButtonsPanel.Controls.AddRange(new Control[] { btnAddRow, btnDeleteRow });

            // ---- Footer (payments + totals + save/cancel/print/excel) ----
            var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 110 };

            var btnPayments = new Button
            {
                Text = "وجوه دریافتی...",
                Location = new Point(700, 8),
                Width = 170,
                Height = 30
            };
            btnPayments.Click += (_, _) => OpenPaymentsDialog();

            _lblReceivedTotal.Text = "وجه دریافتی: 0";
            _lblReceivedTotal.AutoSize = true;
            _lblReceivedTotal.Location = new Point(650, 48);

            footerPanel.Controls.AddRange(new Control[] { btnPayments, _lblReceivedTotal });

            _lblTotal.Text = "جمع کل فاکتور: 0";
            _lblTotal.AutoSize = true;
            _lblTotal.Font = AppFonts.GetFont(10f, FontStyle.Bold);
            _lblTotal.Location = new Point(450, 12);

            _lblRemaining.Text = "مانده حساب: 0";
            _lblRemaining.AutoSize = true;
            _lblRemaining.Font = AppFonts.GetFont(10f, FontStyle.Bold);
            _lblRemaining.ForeColor = Color.DarkRed;
            _lblRemaining.Location = new Point(450, 40);

            var btnPdf = new Button { Text = "خروجی Pdf", Location = new Point(520, 68), Width = 100, Height = 32 };
            btnPdf.Click += btnPdf_Click;

            var btnPrint = new Button { Text = "چاپ فاکتور", Location = new Point(410, 68), Width = 100, Height = 32 };
            btnPrint.Click += (_, _) => PrintInvoice();

            var btnExcel = new Button { Text = "خروجی اکسل", Location = new Point(300, 68), Width = 100, Height = 32 };
            btnExcel.Click += (_, _) => ExportSingleInvoiceToExcel();

            var btnSave = new Button { Text = "ذخیره فاکتور", Location = new Point(150, 68), Width = 140, Height = 32 };
            btnSave.Click += (_, _) => SaveAndClose();

            var btnCancel = new Button { Text = "انصراف", Location = new Point(30, 68), Width = 110, Height = 32 };
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            footerPanel.Controls.AddRange(new Control[]
            {
                _lblTotal, _lblRemaining, btnPrint, btnExcel, btnSave, btnCancel,btnPdf
            });

            Controls.Add(_grid);
            Controls.Add(gridButtonsPanel);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);
        }

        private void AddLabelAndTextBox(Panel parent, string labelText, TextBox box, int x, int y, bool wide = false)
        {
            var lbl = new Label { Text = labelText, AutoSize = true, Location = new Point(x - 20, y + 3) };
            box.Location = new Point(x - (wide ? 420 : 220), y);
            box.Width = wide ? 400 : 200;
            box.TextAlign = HorizontalAlignment.Right;
            parent.Controls.Add(lbl);
            parent.Controls.Add(box);
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "تاریخ",
                Name = "RowDate",
                ReadOnly = true,
                DefaultCellStyle = { BackColor = Color.WhiteSmoke }
            });

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "مرجوعی",
                Name = "IsReturn"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "شرح سنگ",
                Name = "StoneDescription"
            });

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "تعداد*فی",
                Name = "IsQtyPrice"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "تعداد",
                Name = "Quantity"
            });

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "ابزار",
                Name = "IsTool"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "عرض",
                Name = "Width"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "طول",
                Name = "Length"
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "مترمربع",
                Name = "SquareMeter",
                ReadOnly = true,
                DefaultCellStyle =
        {
            BackColor = Color.WhiteSmoke,
            Format = "#,###0.###"
        }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "فی",
                Name = "UnitPrice",
                DefaultCellStyle =
        {
            Format = "#,###0.###"
        }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "مجموع",
                Name = "LineTotal",
                ReadOnly = true,
                DefaultCellStyle =
        {
            BackColor = Color.WhiteSmoke,
            Format = "#,###0.###"
        }
            });
        }

        // -----------------------------------------------------------
        // Customer lookup by mobile number
        // -----------------------------------------------------------
        private void TrySearchCustomer()
        {
            var mobile = _txtMobile.Text.Trim();
            if (mobile.Length == 0) return;

            var found = DatabaseHelper.FindLatestCustomerByMobile(mobile);
            if (found == null) return; // new customer - nothing to fill in

            if (string.IsNullOrWhiteSpace(_txtName.Text)) _txtName.Text = found.CustomerName;
            if (string.IsNullOrWhiteSpace(_txtPhone.Text)) _txtPhone.Text = found.Phone;
            if (string.IsNullOrWhiteSpace(_txtAddress.Text)) _txtAddress.Text = found.Address;
            if (string.IsNullOrWhiteSpace(_txtReferrer.Text)) _txtReferrer.Text = found.ReferrerName;
        }

        // -----------------------------------------------------------
        // Loading an existing invoice into the form
        // -----------------------------------------------------------
        private void LoadInvoiceIntoForm()
        {
            _txtMobile.Text = _invoice.Mobile;
            _txtName.Text = _invoice.CustomerName;
            _txtPhone.Text = _invoice.Phone;
            _txtAddress.Text = _invoice.Address;
            _txtReferrer.Text = _invoice.ReferrerName;
            _dtInvoiceDate.DateText = _invoice.InvoiceDate;

            foreach (var item in _invoice.Items)
                AddRow(item);

            FormatNumericCells();
        }

        // -----------------------------------------------------------
        // Received payments dialog
        // -----------------------------------------------------------
        private void OpenPaymentsDialog()
        {
            using var dlg = new PaymentsForm(_invoice.Payments);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _invoice.Payments = dlg.Payments;
                RecalculateTotals();
            }
        }

        // -----------------------------------------------------------
        // Grid row management
        // -----------------------------------------------------------
        private void AddEmptyRow()
        {
            Save();

            string rowDate = PersianDateHelper.Today();

            if (_grid.Rows.Count > 0)
            {
                var lastRow = _grid.Rows[_grid.Rows.Count - 1];

                if (!lastRow.IsNewRow &&
                    lastRow.Cells["RowDate"].Value != null)
                {
                    rowDate = lastRow.Cells["RowDate"].Value.ToString()!;
                }
            }

            var item = new InvoiceItem
            {
                RowDate = rowDate
            };

            AddRow(item);
        }

        private void AddRow(InvoiceItem item)
        {
            int rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];

            row.Cells[ColRowDate].Value = item.RowDate;
            row.Cells[ColIsReturn].Value = item.IsReturn;
            row.Cells[ColDesc].Value = item.StoneDescription;
            row.Cells[ColIsQtyPrice].Value = item.IsQtyPrice;
            row.Cells[ColQuantity].Value = item.Quantity == 0 ? "" : item.Quantity.ToString("0.###");
            row.Cells[ColIsTool].Value = item.IsTool;
            row.Cells[ColWidth].Value = item.Width is null or 0 ? "" : item.Width.Value.ToString("0.###");
            row.Cells[ColLength].Value = item.Length == 0 ? "" : item.Length.ToString("0.###");
            row.Cells[ColUnitPrice].Value = item.UnitPrice == 0 ? "" : item.UnitPrice.ToString("N0");

            ApplyRowModeState(row);
            RecalculateRow(rowIndex);
        }
        private void FormatNumericCells()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                    continue;

                if (decimal.TryParse(
                    row.Cells["UnitPrice"].Value?.ToString(),
                    out var unitPrice))
                {
                    row.Cells["UnitPrice"].Value = unitPrice;
                }

                if (decimal.TryParse(
                    row.Cells["LineTotal"].Value?.ToString(),
                    out var lineTotal))
                {
                    row.Cells["LineTotal"].Value = lineTotal;
                }
            }
        }
        private void DeleteSelectedRow()
        {
            if (_grid.CurrentRow != null && !_grid.CurrentRow.IsNewRow)
            {
                _grid.Rows.Remove(_grid.CurrentRow);
                RecalculateTotals();
            }
        }

        // Commit checkbox edits immediately so IsTool/IsQtyPrice/IsReturn toggles recalc right away.
        private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (_grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (e.ColumnIndex == ColIsTool || e.ColumnIndex == ColIsQtyPrice)
            {
                ApplyRowModeState(_grid.Rows[e.RowIndex]);
                RecalculateRow(e.RowIndex);
            }
            else if (e.ColumnIndex == ColIsReturn)
            {
                RecalculateRow(e.RowIndex);
            }
        }

        // Clicking the read-only RowDate cell opens the Persian calendar popup.
        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColRowDate) return;

            var row = _grid.Rows[e.RowIndex];
            var current = row.Cells[ColRowDate].Value?.ToString() ?? "";

            using var popup = new Controls.PersianCalendarPopup(current);
            if (popup.ShowDialog(this) == DialogResult.OK)
            {
                row.Cells[ColRowDate].Value = popup.SelectedDate;
            }
        }

        // Tabbing out of the last editable column (فی) starts a new row instead
        // of stopping on the read-only مجموع cell - same effect as clicking
        // "افزودن ردیف" when you're on the last row.
        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            //if (e.KeyCode != Keys.Tab || e.Shift) return;
            //if (_grid.CurrentCell == null || _grid.CurrentCell.ColumnIndex != ColUnitPrice) return;

            //_grid.EndEdit();

            //int rowIndex = _grid.CurrentCell.RowIndex;
            //RecalculateRow(rowIndex);

            //if (rowIndex == _grid.Rows.Count-1)
            //{
            //    AddEmptyRow();
            //}

            //int nextRow = rowIndex + 1;
            //if (nextRow < _grid.Rows.Count)
            //{
            //    _grid.CurrentCell = _grid.Rows[nextRow].Cells[ColDesc];
            //}

            //e.Handled = true;
            //e.SuppressKeyPress = true;
            if (e.KeyCode != Keys.Tab || e.Shift)
                return;

            if (_grid.CurrentCell == null)
                return;

            if (_grid.CurrentCell.ColumnIndex != ColLineTotal)
                return;

            int rowIndex = _grid.CurrentCell.RowIndex;

            _grid.EndEdit();

            RecalculateRow(rowIndex);

            if (rowIndex == _grid.Rows.Count - 1)
            {
                
                AddEmptyRow();
            }

            int nextRow = rowIndex + 1;

            if (nextRow < _grid.Rows.Count)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                _grid.CurrentCell =
                    _grid.Rows[nextRow].Cells[ColDesc];

                _grid.Focus();
                _grid.BeginEdit(true);
            }
        }

        private void Grid_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && !e.Shift)
            {
                if (_grid.CurrentCell?.ColumnIndex == ColLineTotal)
                {
                    e.IsInputKey = true;
                }
            }
        }

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (e.ColumnIndex == ColQuantity || e.ColumnIndex == ColWidth ||
                e.ColumnIndex == ColLength || e.ColumnIndex == ColUnitPrice)
            {
                RecalculateRow(e.RowIndex);
               
            }

          
            if (_grid.Columns[e.ColumnIndex].Name == "UnitPrice")
            {
                var cell = _grid.Rows[e.RowIndex].Cells["UnitPrice"];

                if (decimal.TryParse(cell.Value?.ToString(), out var value))
                {
                    cell.Value = value;
                }
            }
        }

        // Applies the enabled/disabled state of Width/Quantity and the
        // mutual-exclusivity of the ابزار / تعداد*فی checkboxes for one row.
        private void ApplyRowModeState(DataGridViewRow row)
        {
            bool isTool = row.Cells[ColIsTool].Value is true;
            bool isQtyPrice = row.Cells[ColIsQtyPrice].Value is true;

            // A row can only be in one of the two special modes at a time.
            row.Cells[ColIsQtyPrice].ReadOnly = isTool;
            row.Cells[ColIsQtyPrice].Style.BackColor = isTool ? Color.LightGray : Color.White;
            row.Cells[ColIsTool].ReadOnly = isQtyPrice;
            row.Cells[ColIsTool].Style.BackColor = isQtyPrice ? Color.LightGray : Color.White;

            var widthCell = row.Cells[ColWidth];
            var lengthCell = row.Cells[ColLength];
            var qtyCell = row.Cells[ColQuantity];

            if (isQtyPrice)
            {
                // فقط تعداد × فی؛ عرض و طول لازم نیستند.
                widthCell.ReadOnly = true;
                widthCell.Style.BackColor = Color.LightGray;
                widthCell.Value = "";

                lengthCell.ReadOnly = true;
                lengthCell.Style.BackColor = Color.LightGray;
                lengthCell.Value = "";

                qtyCell.ReadOnly = false;
                qtyCell.Style.BackColor = Color.White;
            }
            else if (isTool)
            {
                // فقط طول × فی؛ عرض و تعداد لازم نیستند.
                widthCell.ReadOnly = true;
                widthCell.Style.BackColor = Color.LightGray;
                widthCell.Value = "";

                lengthCell.ReadOnly = false;
                lengthCell.Style.BackColor = Color.White;

                qtyCell.ReadOnly = true;
                qtyCell.Style.BackColor = Color.LightGray;
                qtyCell.Value = "";
            }
            else
            {
                // حالت عادی: عرض × طول × تعداد.
                widthCell.ReadOnly = false;
                widthCell.Style.BackColor = Color.White;

                lengthCell.ReadOnly = false;
                lengthCell.Style.BackColor = Color.White;

                qtyCell.ReadOnly = false;
                qtyCell.Style.BackColor = Color.White;
            }
        }

        private void RecalculateRow(int rowIndex)
        {
            var row = _grid.Rows[rowIndex];
            if (row.IsNewRow) return;

            bool isTool = row.Cells[ColIsTool].Value is true;
            bool isQtyPrice = row.Cells[ColIsQtyPrice].Value is true;
            bool isReturn = row.Cells[ColIsReturn].Value is true;

            decimal qty = ParseDecimal(row.Cells[ColQuantity].Value);
            decimal width = ParseDecimal(row.Cells[ColWidth].Value);
            decimal length = ParseDecimal(row.Cells[ColLength].Value);
            decimal price = ParseDecimal(row.Cells[ColUnitPrice].Value);

            decimal sqm;
            decimal baseTotal;

            if (isQtyPrice)
            {
                sqm = 0;
                baseTotal = qty * price;
            }
            else if (isTool)
            {
                sqm = 0;
                baseTotal = length * price;
            }
            else
            {
                sqm = width * length * qty;
                baseTotal = sqm * price;
            }

            decimal lineTotal = isReturn ? -Math.Abs(baseTotal) : baseTotal;

            row.Cells[ColSquareMeter].Value = (isQtyPrice || isTool) ? "-" : sqm.ToString("0.###");
            row.Cells[ColLineTotal].Value = lineTotal;

            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            decimal total = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                total += ParseDecimal(row.Cells[ColLineTotal].Value);
            }

            decimal received = _invoice.Payments.Sum(p => p.Amount);
            decimal remaining = total - received;

            _lblTotal.Text = $"جمع کل فاکتور: {total:N0}";
            _lblReceivedTotal.Text = $"وجه دریافتی: {received:N0}";
            _lblRemaining.Text = $"مانده حساب: {remaining:N0}";
        }

        private static decimal ParseDecimal(object? value)
        {
            if (value == null) return 0;
            var text = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(text)) return 0;
            return decimal.TryParse(text, out var result) ? result : 0;
        }

        // -----------------------------------------------------------
        // Build an Invoice object from the current form state (used by
        // Save, Print, and Excel export so they all agree on the data).
        // -----------------------------------------------------------
        private Invoice BuildInvoiceFromForm()
        {
            var invoice = new Invoice
            {
                InvoiceID = _invoice.InvoiceID,
                CustomerName = _txtName.Text.Trim(),
                Mobile = _txtMobile.Text.Trim(),
                Phone = _txtPhone.Text.Trim(),
                Address = _txtAddress.Text.Trim(),
                ReferrerName = _txtReferrer.Text.Trim(),
                InvoiceDate = _dtInvoiceDate.DateText.Trim(),
                Payments = _invoice.Payments,
            };

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

              
                bool isTool = row.Cells[ColIsTool].Value is true;
                bool isQtyPrice = row.Cells[ColIsQtyPrice].Value is true;
                //bool isReturn = row.Cells[ColIsReturn].Value is true;

                var item = new InvoiceItem
                {
                    RowDate = row.Cells[ColRowDate].Value?.ToString() ?? "",
                    //IsReturn = isReturn,
                    StoneDescription = row.Cells[ColDesc].Value?.ToString() ?? "",
                    IsQtyPrice = isQtyPrice,
                    Quantity = ParseDecimal(row.Cells[ColQuantity].Value),
                    IsTool = isTool,
                    Width = (isQtyPrice || isTool) ? null : ParseDecimal(row.Cells[ColWidth].Value),
                    Length = ParseDecimal(row.Cells[ColLength].Value),
                    UnitPrice = ParseDecimal(row.Cells[ColUnitPrice].Value),
                };
                item.Recalculate();
                invoice.Items.Add(item);
            }

            invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
            return invoice;
        }

        // -----------------------------------------------------------
        // Save
        // -----------------------------------------------------------
        private void SaveAndClose()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("نام مشتری را وارد کنید.", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_dtInvoiceDate.IsValidDate)
            {
                MessageBox.Show("تاریخ فاکتور را انتخاب کنید.", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _invoice = BuildInvoiceFromForm();
            DatabaseHelper.SaveInvoice(_invoice);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("نام مشتری را وارد کنید.", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_dtInvoiceDate.IsValidDate)
            {
                MessageBox.Show("تاریخ فاکتور را انتخاب کنید.", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _invoice = BuildInvoiceFromForm();
            DatabaseHelper.SaveInvoice(_invoice);

            //DialogResult = DialogResult.OK;
            //Close();
        }

        // -----------------------------------------------------------
        // Print
        // -----------------------------------------------------------
        private void PrintInvoice()
        {
            var invoice = BuildInvoiceFromForm();

            if (invoice.Items.Count == 0)
            {
                MessageBox.Show(
                    "این فاکتور هیچ ردیفی ندارد.",
                    "توجه",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            var printer = new InvoicePrinter(invoice);
            printer.ShowPreview(this);
        }

        // -----------------------------------------------------------
        // Excel export (this invoice only)
        // -----------------------------------------------------------
        private void ExportSingleInvoiceToExcel()
        {
            var invoice = BuildInvoiceFromForm();

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Invoice_{(invoice.InvoiceID == 0 ? "New" : invoice.InvoiceID.ToString())}.xlsx"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    ExcelExportHelper.ExportSingleInvoice(invoice, sfd.FileName);
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


        private void btnPdf_Click(object sender, EventArgs e)
        {
            var invoice = BuildInvoiceFromForm();

            if (invoice.Items.Count == 0)
            {
                MessageBox.Show(
                    "این فاکتور هیچ ردیفی ندارد.",
                    "توجه",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            var printer = new InvoicePrinter(invoice);

            printer.ExportPdf(this);
        }
    }
}
