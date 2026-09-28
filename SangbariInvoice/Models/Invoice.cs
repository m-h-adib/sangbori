namespace SangbariInvoice.Models
{
    /// <summary>
    /// Represents one row of the Invoices table.
    /// Customer information is stored directly on the invoice (denormalized on purpose),
    /// exactly as requested: no separate Customers table.
    /// </summary>
    public class Invoice
    {
        public int InvoiceID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ReferrerName { get; set; } = string.Empty;

        /// <summary>Persian (Shamsi) date string, format yyyy/MM/dd</summary>
        public string InvoiceDate { get; set; } = string.Empty;

        /// <summary>Sum of LineTotal across all items (return rows already negative). Calculated, not user-entered.</summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// List of received-payment entries ("وجوه دریافتی"). The user can add as many
        /// as needed via the payments dialog; stored as JSON in a single column.
        /// </summary>
        public List<PaymentEntry> Payments { get; set; } = new();

        /// <summary>Sum of all entries in Payments. Calculated, not user-entered directly.</summary>
        public decimal ReceivedAmount => Payments.Sum(p => p.Amount);

        /// <summary>TotalAmount - ReceivedAmount. Calculated, not user-entered.</summary>
        public decimal RemainingBalance => TotalAmount - ReceivedAmount;

        public List<InvoiceItem> Items { get; set; } = new();
    }
}
