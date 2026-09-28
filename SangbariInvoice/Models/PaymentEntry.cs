namespace SangbariInvoice.Models
{
    /// <summary>
    /// One entry in the "وجوه دریافتی" (received payments) list of an invoice.
    /// An invoice can have any number of these; their sum is the invoice's
    /// total received amount.
    /// </summary>
    public class PaymentEntry
    {
        public string PaymentDate { get; set; } = "";

        public decimal Amount { get; set; }

        public string PaymentType { get; set; } = "";

        public string CheckNumber { get; set; } = "";

        public string CheckDueDate { get; set; } = "";

        public string Description { get; set; } = "";
    }
}
