namespace SangbariInvoice.Models
{
    /// <summary>
    /// Represents one row of the InvoiceItems table (one line of the stone-cutting invoice).
    ///
    /// A row is in exactly one of three modes:
    ///   - Normal (IsTool = false, IsQtyPrice = false):
    ///       SquareMeter = Width * Length * Quantity, LineTotal = SquareMeter * UnitPrice
    ///   - Tool / "ابزار" (IsTool = true):
    ///       LineTotal = Length * UnitPrice directly (Width and Quantity are not used)
    ///   - Quantity*Price / "تعداد*فی" (IsQtyPrice = true):
    ///       LineTotal = Quantity * UnitPrice directly (Width/Length/SquareMeter are not used)
    /// IsTool and IsQtyPrice are mutually exclusive on the same row.
    ///
    /// If IsReturn ("مرجوعی") is set, the row's LineTotal is stored as a negative
    /// number so it subtracts from the invoice's grand total.
    /// </summary>
    public class InvoiceItem
    {
        public int ItemID { get; set; }
        public int InvoiceID { get; set; }

        /// <summary>Persian (Shamsi) date string, format yyyy/MM/dd</summary>
        public string RowDate { get; set; } = string.Empty;

        /// <summary>"مرجوعی" - when true, this row's LineTotal is negative.</summary>
        public bool IsReturn { get; set; }

        public string StoneDescription { get; set; } = string.Empty;
        public decimal Quantity { get; set; }

        /// <summary>
        /// True  => "ابزار" row: SquareMeter = Length * Width, Quantity not used.
        /// Mutually exclusive with IsQtyPrice.
        /// </summary>
        public bool IsTool { get; set; }

        /// <summary>
        /// True  => "تعداد*فی" row: LineTotal = Quantity * UnitPrice directly,
        /// Width/Length/SquareMeter not used. Mutually exclusive with IsTool.
        /// </summary>
        public bool IsQtyPrice { get; set; }

        public decimal? Width { get; set; }
        public decimal Length { get; set; }
        public decimal UnitPrice { get; set; }

        /// <summary>Calculated field - see Recalculate().</summary>
        public decimal SquareMeter { get; private set; }

        /// <summary>Calculated field - see Recalculate(). Negative when IsReturn is set.</summary>
        public decimal LineTotal { get; private set; }

        /// <summary>
        /// Recomputes SquareMeter and LineTotal from the current field values.
        /// Call this any time Quantity, IsTool, IsQtyPrice, IsReturn, Width, Length or UnitPrice changes.
        /// </summary>
        public void Recalculate()
        {
            decimal baseTotal;

            if (IsQtyPrice)
            {
                SquareMeter = 0;
                baseTotal = Quantity * UnitPrice;
            }
            else if (IsTool)
            {
                SquareMeter = 0;
                baseTotal = Length * UnitPrice;
            }
            else
            {
                SquareMeter = (Width ?? 0) * Length * Quantity;
                baseTotal = SquareMeter * UnitPrice;
            }

            LineTotal = IsReturn ? -Math.Abs(baseTotal) : baseTotal;
        }
    }
}
