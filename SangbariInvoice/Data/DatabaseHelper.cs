using System.Text.Json;
using Microsoft.Data.Sqlite;
using SangbariInvoice.Models;

namespace SangbariInvoice.Data
{
    public static class DatabaseHelper
    {
        // The database file is created next to the executable.
        private static readonly string DbPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "invoices.db");

        private static string ConnectionString => $"Data Source={DbPath}";

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            // Enforce FK constraints (off by default in SQLite) and enable cascade delete.
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return conn;
        }

        /// <summary>Creates the database file and tables on first run, and migrates
        /// databases created by earlier versions of the app. Safe to call every startup.</summary>
        public static void Initialize()
        {
            using var conn = GetConnection();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Invoices (
                        InvoiceID         INTEGER PRIMARY KEY AUTOINCREMENT,
                        CustomerName      TEXT NOT NULL,
                        Mobile            TEXT,
                        Phone             TEXT,
                        Address           TEXT,
                        ReferrerName      TEXT,
                        InvoiceDate       TEXT NOT NULL,
                        TotalAmount       REAL NOT NULL DEFAULT 0,
                        ReceivedAmount    REAL NOT NULL DEFAULT 0,
                        ReceivedDate      TEXT,
                        RemainingBalance  REAL NOT NULL DEFAULT 0,
                        PaymentsJson      TEXT
                    );

                    CREATE TABLE IF NOT EXISTS InvoiceItems (
                        ItemID            INTEGER PRIMARY KEY AUTOINCREMENT,
                        InvoiceID         INTEGER NOT NULL,
                        RowDate           TEXT,
                        StoneDescription  TEXT,
                        Quantity          REAL NOT NULL DEFAULT 0,
                        IsTool            INTEGER NOT NULL DEFAULT 0,
                        IsQtyPrice        INTEGER NOT NULL DEFAULT 0,
                        IsReturn          INTEGER NOT NULL DEFAULT 0,
                        Width             REAL,
                        Length            REAL NOT NULL DEFAULT 0,
                        SquareMeter       REAL NOT NULL DEFAULT 0,
                        UnitPrice         REAL NOT NULL DEFAULT 0,
                        LineTotal         REAL NOT NULL DEFAULT 0,
                        FOREIGN KEY (InvoiceID) REFERENCES Invoices (InvoiceID) ON DELETE CASCADE
                    );

                    CREATE INDEX IF NOT EXISTS idx_invoices_mobile ON Invoices (Mobile);
                    CREATE INDEX IF NOT EXISTS idx_items_invoiceid ON InvoiceItems (InvoiceID);
                ";
                cmd.ExecuteNonQuery();
            }

            // ---- Migrations for databases created by earlier versions of the app ----
            // (New columns needed by later features. Old columns, like the removed
            // "Diameter" text field, are simply left in place, unused, if present -
            // no destructive migration is performed.)
            AddColumnIfMissing(conn, "Invoices", "PaymentsJson", "TEXT");
            AddColumnIfMissing(conn, "InvoiceItems", "IsQtyPrice", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(conn, "InvoiceItems", "IsReturn", "INTEGER NOT NULL DEFAULT 0");
        }

        private static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
        {
            using (var check = conn.CreateCommand())
            {
                check.CommandText = $"PRAGMA table_info({table});";
                using var reader = check.ExecuteReader();
                int nameOrdinal = reader.GetOrdinal("name");
                while (reader.Read())
                {
                    var existingName = reader.GetString(nameOrdinal);
                    if (string.Equals(existingName, column, StringComparison.OrdinalIgnoreCase))
                        return; // column already exists - nothing to do
                }
            }

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            alter.ExecuteNonQuery();
        }

        // ---------------------------------------------------------------
        // Customer auto-fill: look up the most recent invoice for a mobile
        // number and return its customer fields.
        // ---------------------------------------------------------------
        public static Invoice? FindLatestCustomerByMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return null;

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT CustomerName, Mobile, Phone, Address, ReferrerName
                FROM Invoices
                WHERE Mobile = $mobile
                ORDER BY InvoiceID DESC
                LIMIT 1;";
            cmd.Parameters.AddWithValue("$mobile", mobile.Trim());

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new Invoice
            {
                CustomerName = reader.GetString(0),
                Mobile = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Phone = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Address = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ReferrerName = reader.IsDBNull(4) ? "" : reader.GetString(4),
            };
        }

        // ---------------------------------------------------------------
        // Listing / searching invoices for the main grid
        // ---------------------------------------------------------------
        public static List<Invoice> GetInvoices(string? searchText = null)
        {
            var list = new List<Invoice>();

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                cmd.CommandText = @"SELECT InvoiceID, CustomerName, Mobile, InvoiceDate,
                                            TotalAmount, ReceivedAmount, RemainingBalance
                                     FROM Invoices
                                     ORDER BY InvoiceID DESC;";
            }
            else
            {
                cmd.CommandText = @"SELECT InvoiceID, CustomerName, Mobile, InvoiceDate,
                                            TotalAmount, ReceivedAmount, RemainingBalance
                                     FROM Invoices
                                     WHERE CustomerName LIKE $q OR Mobile LIKE $q
                                     ORDER BY InvoiceID DESC;";
                cmd.Parameters.AddWithValue("$q", $"%{searchText.Trim()}%");
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var totalAmount = (decimal)reader.GetDouble(4);
                var receivedAmount = (decimal)reader.GetDouble(5);
                list.Add(new Invoice
                {
                    InvoiceID = reader.GetInt32(0),
                    CustomerName = reader.GetString(1),
                    Mobile = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    InvoiceDate = reader.GetString(3),
                    TotalAmount = totalAmount,
                    Payments = receivedAmount == 0
                        ? new List<PaymentEntry>()
                        : new List<PaymentEntry> { new PaymentEntry { Amount = receivedAmount } },
                });
            }

            return list;
        }

        // ---------------------------------------------------------------
        // Load one invoice with all its items
        // ---------------------------------------------------------------
        public static Invoice GetInvoiceById(int invoiceId)
        {
            using var conn = GetConnection();

            Invoice invoice;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT InvoiceID, CustomerName, Mobile, Phone, Address,
                                            ReferrerName, InvoiceDate, TotalAmount,
                                            ReceivedAmount, ReceivedDate, PaymentsJson
                                     FROM Invoices WHERE InvoiceID = $id;";
                cmd.Parameters.AddWithValue("$id", invoiceId);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException($"Invoice {invoiceId} not found.");

                var legacyReceived = (decimal)reader.GetDouble(8);
                var legacyReceivedDate = reader.IsDBNull(9) ? "" : reader.GetString(9);
                var paymentsJson = reader.IsDBNull(10) ? null : reader.GetString(10);

                invoice = new Invoice
                {
                    InvoiceID = reader.GetInt32(0),
                    CustomerName = reader.GetString(1),
                    Mobile = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Address = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ReferrerName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    InvoiceDate = reader.GetString(6),
                    TotalAmount = (decimal)reader.GetDouble(7),
                    Payments = DeserializePayments(paymentsJson, legacyReceived, legacyReceivedDate),
                };
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ItemID, InvoiceID, RowDate, StoneDescription,
                                            Quantity, IsTool, IsQtyPrice, IsReturn, Width, Length, UnitPrice
                                     FROM InvoiceItems WHERE InvoiceID = $id
                                     ORDER BY RowDate ASC;";
                cmd.Parameters.AddWithValue("$id", invoiceId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var item = new InvoiceItem
                    {
                        ItemID = reader.GetInt32(0),
                        InvoiceID = reader.GetInt32(1),
                        RowDate = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        StoneDescription = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Quantity = (decimal)reader.GetDouble(4),
                        IsTool = reader.GetInt32(5) == 1,
                        IsQtyPrice = reader.GetInt32(6) == 1,
                        IsReturn = reader.GetInt32(7) == 1,
                        Width = reader.IsDBNull(8) ? (decimal?)null : (decimal)reader.GetDouble(8),
                        Length = (decimal)reader.GetDouble(9),
                        UnitPrice = (decimal)reader.GetDouble(10),
                    };
                    item.Recalculate();
                    invoice.Items.Add(item);
                }
            }

            return invoice;
        }

        /// <summary>
        /// Parses the JSON payments list. For invoices saved before the payments
        /// list existed, falls back to turning the old single ReceivedAmount/ReceivedDate
        /// pair into one payment entry, so old data still shows up correctly.
        /// </summary>
        private static List<PaymentEntry> DeserializePayments(string? json, decimal legacyAmount, string legacyDate)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<PaymentEntry>>(json);
                    if (list != null) return list;
                }
                catch
                {
                    // Corrupt/unreadable JSON - fall through to the legacy handling below.
                }
            }

            if (legacyAmount != 0)
            {
                return new List<PaymentEntry>
                {
                    new PaymentEntry { Amount = legacyAmount, PaymentDate = legacyDate }
                };
            }

            return new List<PaymentEntry>();
        }

        // ---------------------------------------------------------------
        // Save (insert or update) an invoice together with all its items.
        // Items are replaced wholesale on every save - simplest correct
        // approach for a small per-invoice item grid.
        // ---------------------------------------------------------------
        public static int SaveInvoice(Invoice invoice)
        {
            // Make sure calculated fields are current before saving.
            foreach (var item in invoice.Items)
                item.Recalculate();

            invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);

            using var conn = GetConnection();
            using var transaction = conn.BeginTransaction();

            if (invoice.InvoiceID == 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO Invoices
                        (CustomerName, Mobile, Phone, Address, ReferrerName, InvoiceDate,
                         TotalAmount, ReceivedAmount, ReceivedDate, RemainingBalance, PaymentsJson)
                    VALUES
                        ($name, $mobile, $phone, $address, $referrer, $invDate,
                         $total, $received, $recDate, $remaining, $paymentsJson);
                    SELECT last_insert_rowid();";
                AddInvoiceParams(cmd, invoice);
                invoice.InvoiceID = Convert.ToInt32((long)cmd.ExecuteScalar()!);
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE Invoices SET
                        CustomerName = $name, Mobile = $mobile, Phone = $phone,
                        Address = $address, ReferrerName = $referrer, InvoiceDate = $invDate,
                        TotalAmount = $total, ReceivedAmount = $received,
                        ReceivedDate = $recDate, RemainingBalance = $remaining,
                        PaymentsJson = $paymentsJson
                    WHERE InvoiceID = $id;";
                AddInvoiceParams(cmd, invoice);
                cmd.Parameters.AddWithValue("$id", invoice.InvoiceID);
                cmd.ExecuteNonQuery();

                using var delCmd = conn.CreateCommand();
                delCmd.Transaction = transaction;
                delCmd.CommandText = "DELETE FROM InvoiceItems WHERE InvoiceID = $id;";
                delCmd.Parameters.AddWithValue("$id", invoice.InvoiceID);
                delCmd.ExecuteNonQuery();
            }

            foreach (var item in invoice.Items)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO InvoiceItems
                        (InvoiceID, RowDate, StoneDescription, Quantity,
                         IsTool, IsQtyPrice, IsReturn, Width, Length, SquareMeter, UnitPrice, LineTotal)
                    VALUES
                        ($invId, $rowDate, $desc, $qty,
                         $isTool, $isQtyPrice, $isReturn, $width, $length, $sqm, $price, $lineTotal);";
                cmd.Parameters.AddWithValue("$invId", invoice.InvoiceID);
                cmd.Parameters.AddWithValue("$rowDate", item.RowDate ?? "");
                cmd.Parameters.AddWithValue("$desc", item.StoneDescription ?? "");
                cmd.Parameters.AddWithValue("$qty", (double)item.Quantity);
                cmd.Parameters.AddWithValue("$isTool", item.IsTool ? 1 : 0);
                cmd.Parameters.AddWithValue("$isQtyPrice", item.IsQtyPrice ? 1 : 0);
                cmd.Parameters.AddWithValue("$isReturn", item.IsReturn ? 1 : 0);
                cmd.Parameters.AddWithValue("$width", item.IsQtyPrice || item.Width == null
                    ? DBNull.Value : (double)item.Width.Value);
                cmd.Parameters.AddWithValue("$length", (double)item.Length);
                cmd.Parameters.AddWithValue("$sqm", (double)item.SquareMeter);
                cmd.Parameters.AddWithValue("$price", (double)item.UnitPrice);
                cmd.Parameters.AddWithValue("$lineTotal", (double)item.LineTotal);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return invoice.InvoiceID;
        }

        private static void AddInvoiceParams(SqliteCommand cmd, Invoice invoice)
        {
            cmd.Parameters.AddWithValue("$name", invoice.CustomerName ?? "");
            cmd.Parameters.AddWithValue("$mobile", invoice.Mobile ?? "");
            cmd.Parameters.AddWithValue("$phone", invoice.Phone ?? "");
            cmd.Parameters.AddWithValue("$address", invoice.Address ?? "");
            cmd.Parameters.AddWithValue("$referrer", invoice.ReferrerName ?? "");
            cmd.Parameters.AddWithValue("$invDate", invoice.InvoiceDate ?? "");
            cmd.Parameters.AddWithValue("$total", (double)invoice.TotalAmount);
            cmd.Parameters.AddWithValue("$received", (double)invoice.ReceivedAmount);

            var lastPaymentDate = invoice.Payments.Count > 0 ? invoice.Payments[^1].PaymentDate : null;
            cmd.Parameters.AddWithValue("$recDate", string.IsNullOrWhiteSpace(lastPaymentDate)
                ? DBNull.Value : lastPaymentDate);

            cmd.Parameters.AddWithValue("$remaining", (double)invoice.RemainingBalance);
            cmd.Parameters.AddWithValue("$paymentsJson", JsonSerializer.Serialize(invoice.Payments));
        }

        public static void DeleteInvoice(int invoiceId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            // ON DELETE CASCADE (with foreign_keys=ON) removes the matching InvoiceItems too.
            cmd.CommandText = "DELETE FROM Invoices WHERE InvoiceID = $id;";
            cmd.Parameters.AddWithValue("$id", invoiceId);
            cmd.ExecuteNonQuery();
        }
    }
}
