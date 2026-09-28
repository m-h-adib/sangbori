using ClosedXML.Excel;
using SangbariInvoice.Models;

namespace SangbariInvoice.Helpers
{
    public static class ExcelExportHelper
    {
        /// <summary>
        /// Exports the invoice list (as shown on the main grid, after any search filter)
        /// to a single-sheet Excel file, formatted right-to-left.
        /// </summary>
        public static void ExportInvoiceList(List<Invoice> invoices, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("فاکتورها");
            ws.RightToLeft = true;

            string[] headers =
            {
                "شماره فاکتور", "نام مشتری", "موبایل", "تاریخ فاکتور",
                "جمع کل", "دریافتی", "مانده حساب"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var inv in invoices)
            {
                ws.Cell(row, 1).Value = inv.InvoiceID;
                ws.Cell(row, 2).Value = inv.CustomerName;
                ws.Cell(row, 3).Value = inv.Mobile;
                ws.Cell(row, 4).Value = inv.InvoiceDate;
                ws.Cell(row, 5).Value = (double)inv.TotalAmount;
                ws.Cell(row, 6).Value = (double)inv.ReceivedAmount;
                ws.Cell(row, 7).Value = (double)inv.RemainingBalance;
                row++;
            }

            ws.Range(2, 5, row - 1, 7).Style.NumberFormat.Format = "#,##0";
            ws.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// Exports a single invoice, header info + item lines, to its own Excel file -
        /// handy for a customer who wants a spreadsheet copy of one invoice.
        /// </summary>
        public static void ExportSingleInvoice(Invoice invoice, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"فاکتور {invoice.InvoiceID}");
            ws.RightToLeft = true;

            ws.Cell(1, 1).Value = "نام مشتری:";
            ws.Cell(1, 2).Value = invoice.CustomerName;
            ws.Cell(2, 1).Value = "موبایل:";
            ws.Cell(2, 2).Value = invoice.Mobile;
            ws.Cell(3, 1).Value = "آدرس:";
            ws.Cell(3, 2).Value = invoice.Address;
            ws.Cell(4, 1).Value = "تاریخ فاکتور:";
            ws.Cell(4, 2).Value = invoice.InvoiceDate;
            ws.Row(1).Style.Font.Bold = true;
            ws.Row(2).Style.Font.Bold = true;
            ws.Row(3).Style.Font.Bold = true;
            ws.Row(4).Style.Font.Bold = true;

            string[] headers =
            {
                "تاریخ", "مرجوعی", "شرح سنگ", "تعداد*فی", "تعداد", "ابزار",
                "عرض", "طول", "مترمربع", "فی", "مجموع"
            };

            int headerRow = 6;
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = headerRow + 1;
            foreach (var item in invoice.Items)
            {
                ws.Cell(row, 1).Value = item.RowDate;
                ws.Cell(row, 2).Value = item.IsReturn ? "بله" : "خیر";
                ws.Cell(row, 3).Value = item.StoneDescription;
                ws.Cell(row, 4).Value = item.IsQtyPrice ? "بله" : "خیر";
                ws.Cell(row, 5).Value = (double)item.Quantity;
                ws.Cell(row, 6).Value = item.IsTool ? "بله" : "خیر";
                ws.Cell(row, 7).Value = (item.IsQtyPrice || item.IsTool) ? null : (double?)item.Width;
                ws.Cell(row, 8).Value = (double)item.Length;
                ws.Cell(row, 9).Value = (double)item.SquareMeter;
                ws.Cell(row, 10).Value = (double)item.UnitPrice;
                ws.Cell(row, 11).Value = (double)item.LineTotal;
                row++;
            }

            row += 1;
            if (invoice.Payments.Count > 0)
            {
                ws.Cell(row, 1).Value = "وجوه دریافتی:";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;

                string[] paymentHeaders = { "تاریخ", "نوع پرداخت", "شماره چک", "توضیحات", "مبلغ" };
                for (int i = 0; i < paymentHeaders.Length; i++)
                {
                    var cell = ws.Cell(row, i + 1);
                    cell.Value = paymentHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                row++;

                foreach (var payment in invoice.Payments)
                {
                    ws.Cell(row, 1).Value = payment.PaymentDate;
                    ws.Cell(row, 2).Value = payment.PaymentType;
                    ws.Cell(row, 3).Value = payment.CheckNumber;
                    ws.Cell(row, 4).Value = payment.Description;
                    ws.Cell(row, 5).Value = (double)payment.Amount;
                    row++;
                }
                row++;
            }

            ws.Cell(row, 10).Value = "جمع کل:";
            ws.Cell(row, 11).Value = (double)invoice.TotalAmount;
            row++;
            ws.Cell(row, 10).Value = "دریافتی:";
            ws.Cell(row, 11).Value = (double)invoice.ReceivedAmount;
            row++;
            ws.Cell(row, 10).Value = "مانده:";
            ws.Cell(row, 11).Value = (double)invoice.RemainingBalance;
            ws.Range(row - 2, 10, row, 11).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }
    }
}
