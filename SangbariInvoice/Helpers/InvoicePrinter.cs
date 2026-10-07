using System.Drawing.Printing;
using SangbariInvoice.Models;

namespace SangbariInvoice.Helpers
{
    /// <summary>
    /// Prints a single invoice as a simple right-to-left report:
    /// header (customer info), an item table, and the totals block.
    /// Supports pagination if the item list doesn't fit on one page.
    /// </summary>
    public class InvoicePrinter
    {
        private readonly Invoice _invoice;
        private int _nextItemIndex;

        private readonly Font _titleFont = AppFonts.GetFont(16, FontStyle.Bold);
        private readonly Font _headerFont = AppFonts.GetFont(10, FontStyle.Bold);
        private readonly Font _normalFont = AppFonts.GetFont(9.5f);
        private readonly Font _boldFont = AppFonts.GetFont(10, FontStyle.Bold);

        private readonly StringFormat _rtl = new(StringFormatFlags.DirectionRightToLeft)
        {
            Alignment = StringAlignment.Near
        };

        // Column layout (x-right, width) drawn from right to left.
        // ابزار / تعداد*فی / قطر are intentionally left out of the printed table
        // (they're internal entry-mode flags, not something a customer needs to see).
        private readonly (string Header, float Width)[] _columns =
        {
			("#", 35),("تاریخ", 90), ("شرح سنگ", 160), ("تعداد", 55),
            ("عرض", 60), ("طول", 60), ("مترمربع", 70), ("فی", 75), ("مجموع", 90)
        };

        public InvoicePrinter(Invoice invoice)
        {
            _invoice = invoice;
        }
        public void Print(IWin32Window owner)
        {
            var doc = BuildDocument();

            using var printDialog = new PrintDialog
            {
                Document = doc,
                UseEXDialog = true,
                AllowSomePages = false,
                AllowCurrentPage = false,
                AllowSelection = false
            };

            if (printDialog.ShowDialog(owner) == DialogResult.OK)
            {
                doc.Print();
            }
        }

        public void ShowPreview(IWin32Window owner)
        {
            var doc = BuildDocument();

            using var preview = new PrintPreviewDialog
            {
                Document = doc,
                Width = 900,
                Height = 700,
                StartPosition = FormStartPosition.CenterParent
            };


            preview.ShowDialog(owner);
        }

        private PrintDocument BuildDocument()
        {
            _nextItemIndex = 0;

            var doc = new PrintDocument();

            doc.DefaultPageSettings.Landscape = false;

            doc.BeginPrint += (_, _) =>
            {
                _nextItemIndex = 0;
            };

            doc.PrintPage += OnPrintPage;

            return doc;
        }

        private void OnPrintPage(object? sender, PrintPageEventArgs e)
        {
            var g = e.Graphics!;
            var bounds = e.MarginBounds;
            float right = bounds.Right+15;
            //float y = bounds.Top;
            float y = 50;

            if (_nextItemIndex == 0)
            {
                // ---- Title & header block (only on first page) ----
                g.DrawString("فاکتور فروش سنگ", _titleFont, Brushes.Black,
                    new RectangleF(bounds.Left, y, bounds.Width, 30), _rtl);
                y += 40;

                g.DrawString($"شماره فاکتور: {_invoice.InvoiceID}       تاریخ: {_invoice.InvoiceDate}", _headerFont, Brushes.Black,
                    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                y += 25;

                //g.DrawString($"تاریخ: {_invoice.InvoiceDate}", _normalFont, Brushes.Black,
                //    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                //y += 22;

                g.DrawString($"نام مشتری: {_invoice.CustomerName}    تلفن همراه: {_invoice.Mobile}    کد ملی: {_invoice.Phone}", _normalFont, Brushes.Black,
                    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                y += 25;

                //g.DrawString($"موبایل: {_invoice.Mobile}    کد ملی: {_invoice.Phone}", _normalFont, Brushes.Black,
                //    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                //y += 20;

                g.DrawString($"معرف: {_invoice.ReferrerName}   آدرس: {_invoice.Address}", _normalFont, Brushes.Black,
                    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                y += 25;

                //if (!string.IsNullOrWhiteSpace(_invoice.ReferrerName))
                //{
                //    g.DrawString($"معرف: {_invoice.ReferrerName}", _normalFont, Brushes.Black,
                //        new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                //    y += 20;
                //}

                y += 10;
            }

            // ---- Table header ----
            float rowHeight = 22;
            DrawTableRow(g, right, y, _columns.Select(c => c.Header).ToArray(), _headerFont, header: true);
            y += rowHeight;

            // ---- Table rows (with pagination) ----
            while (_nextItemIndex < _invoice.Items.Count)
            {
                if (y + rowHeight > bounds.Bottom - 110)
                {
                    e.HasMorePages = true;
                    return;
                }

                var item = _invoice.Items[_nextItemIndex];
                var cells = new[]
                {
                    (_nextItemIndex+1)+"",
                    item.RowDate,
                    //item.IsReturn ? "بله" : "خیر",
                    item.StoneDescription,
                    item.IsTool ? "-" : item.Quantity.ToString("0.###"),
                    (item.IsQtyPrice || item.IsTool) ? "-" : (item.Width ?? 0).ToString("0.###"),
                    item.IsQtyPrice ? "-" : item.Length.ToString("0.###"),
                    (item.IsQtyPrice || item.IsTool) ? "-" : item.SquareMeter.ToString("0.###"),
                    item.UnitPrice.ToString("N0"),
                    item.LineTotal.ToString("N0"),
                };
                DrawTableRow(g, right, y, cells, _normalFont, header: false);
                y += rowHeight;
                _nextItemIndex++;
            }

            // ---- Totals block (only after the last item, on the last page) ----
            y += 15;
            g.DrawLine(Pens.Black, bounds.Left, y, bounds.Right, y);
            y += 8;

            g.DrawString($"جمع کل فاکتور: {_invoice.TotalAmount:N0}", _boldFont, Brushes.Black,
                new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
            y += 22;

            if (_invoice.Payments.Count == 0)
            {
                g.DrawString("وجه دریافتی: 0", _normalFont, Brushes.Black,
                    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                y += 20;
            }
            else
            {
                g.DrawString("وجوه دریافتی:", _normalFont, Brushes.Black,
                    new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                y += 20;
                foreach (var payment in _invoice.Payments)
                {
                    //var typeLabel = payment.PaymentType == "چک"
                    //    ? (string.IsNullOrWhiteSpace(payment.CheckNumber) ? "چک" : $"چک شماره {payment.CheckNumber}")
                    //    : "نقد";
                    
                    var line = $"— تاریخ: {payment.PaymentDate}    مبلغ: {payment.Amount:N0}";
                    if (!string.IsNullOrWhiteSpace(payment.PaymentType))
                        line += $"    {payment.PaymentType}";
                    if (!string.IsNullOrWhiteSpace(payment.CheckDueDate))
                        line += $"    تاریخ وصول: {payment.CheckDueDate}";
                    if (!string.IsNullOrWhiteSpace(payment.Description))
                        line += $"    {payment.Description}";

                    g.DrawString(line, _normalFont, Brushes.Black, new RectangleF(bounds.Left, y, bounds.Width, 20), _rtl);
                    y += 20;
                }
                y += 4;
            }

            var text = $"مانده حساب: {_invoice.RemainingBalance:N0}";

            var rect = new RectangleF(
                bounds.Left,
                y,
                bounds.Width,
                20
            );

            g.DrawString(
                text,
                _boldFont,
                Brushes.Black,
                rect,
                _rtl
            );

            // خط زیر متن با فاصله بیشتر
            using var underlinePen = new Pen(Color.Black, 1);

            var textSize = g.MeasureString(text, _boldFont);

            float lineY = y + textSize.Height - 1 + 4; // ← عدد 4 فاصله خط از متن

            g.DrawLine(
                underlinePen,
                bounds.Right - textSize.Width,
                lineY,
                bounds.Right,
                lineY
            );

            e.HasMorePages = false;
        }

        private void DrawTableRow(Graphics g, float right, float y, string[] cells, Font font, bool header)
        {
            float x = right;
            for (int i = 0; i < _columns.Length; i++)
            {
                float w = _columns[i].Width;
                var rect = new RectangleF(x - w, y, w, 22);
                g.DrawRectangle(Pens.Gray, rect.X, rect.Y, rect.Width, rect.Height);

                var text = i < cells.Length ? cells[i] : "";
                g.DrawString(text, font, Brushes.Black, rect, new StringFormat(_rtl)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });

                x -= w;
            }
        }



        public void ExportPdf(IWin32Window owner)
        {
            using var saveDialog = new SaveFileDialog
            {
                Title = "ذخیره فاکتور به صورت PDF",
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"فاکتور-{_invoice.InvoiceID}.pdf",
                AddExtension = true,
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog(owner) != DialogResult.OK)
                return;

            var doc = BuildDocument();

            try
            {
                doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
                doc.PrinterSettings.PrintToFile = true;
                doc.PrinterSettings.PrintFileName = saveDialog.FileName;

                doc.Print();

                MessageBox.Show(
                    "فاکتور با موفقیت به صورت PDF ذخیره شد.",
                    "ذخیره PDF",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"خطا در ایجاد فایل PDF:\n{ex.Message}",
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                doc.Dispose();
            }
        }
    }
}
