using SangbariInvoice.Data;

namespace SangbariInvoice.Helpers
{
    public static class InvoiceSummaryHelper
    {
        /// <summary>
        /// Loads the total amount of tool rows and net square meters for all invoices
        /// in one query, avoiding one database query per invoice.
        /// </summary>
        public static void Populate(IReadOnlyCollection<Models.Invoice> invoices)
        {
            if (invoices.Count == 0)
                return;

            var totals = new Dictionary<int, (decimal ToolAmount, decimal SquareMeter)>();

            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT InvoiceID,
                       COALESCE(SUM(
                           CASE WHEN IsTool = 1
                                THEN (CASE WHEN IsReturn = 1 THEN -1 ELSE 1 END)
                                     * Length * UnitPrice
                                ELSE 0 END), 0) AS ToolAmount,
                       COALESCE(SUM(
                           CASE WHEN IsTool = 0 AND IsQtyPrice = 0
                                THEN (CASE WHEN IsReturn = 1 THEN -1 ELSE 1 END)
                                     * COALESCE(Width, 0) * Length * Quantity
                                ELSE 0 END), 0) AS SquareMeter
                FROM InvoiceItems
                GROUP BY InvoiceID;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                totals[reader.GetInt32(0)] =
                    ((decimal)reader.GetDouble(1), (decimal)reader.GetDouble(2));
            }

            foreach (var invoice in invoices)
            {
                if (totals.TryGetValue(invoice.InvoiceID, out var total))
                {
                    invoice.ToolAmount = total.ToolAmount;
                    invoice.SquareMeter = total.SquareMeter;
                }
                else
                {
                    invoice.ToolAmount = 0;
                    invoice.SquareMeter = 0;
                }
            }
        }
    }
}
