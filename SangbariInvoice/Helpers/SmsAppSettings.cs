using System.Text.Json;

namespace SangbariInvoice.Helpers
{
    public sealed class SmsAppSettings
    {
        public string CardNumber { get; init; } = "";
        public string ShebaNumber { get; init; } = "";
        public string Sms2 { get; init; } = "";
        public string ModemPort { get; init; } = "COM4";
        public int ModemBaudRate { get; init; } = 115200;

        public static SmsAppSettings Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("فایل appsettings.json پیدا نشد.", path);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("Sms", out var sms))
                throw new InvalidDataException("بخش Sms در appsettings.json وجود ندارد.");

            static string ReadString(JsonElement section, string name, string fallback = "") =>
                section.TryGetProperty(name, out var value) ? value.GetString() ?? fallback : fallback;

            var settings = new SmsAppSettings
            {
                CardNumber = ReadString(sms, "CardNumber"),
                ShebaNumber = ReadString(sms, "ShebaNumber"),
                Sms2 = ReadString(sms, "Sms2"),
                ModemPort = ReadString(sms, "ModemPort", "COM4"),
                ModemBaudRate = sms.TryGetProperty("ModemBaudRate", out var baudRate) &&
                                baudRate.TryGetInt32(out var parsedBaudRate)
                    ? parsedBaudRate
                    : 115200
            };

            if (settings.ModemBaudRate <= 0)
                throw new InvalidDataException("ModemBaudRate باید بزرگ‌تر از صفر باشد.");

            return settings;
        }

        public string BuildCardAndShebaMessage(string customerName)
        {
            var message = Sms2;

            if (string.IsNullOrWhiteSpace(message))
                message = "با سلام {name}\nشماره کارت: {card}\nشماره شبا: {sheba}";

            return message
                .Replace("{name}", customerName ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{card}", CardNumber, StringComparison.OrdinalIgnoreCase)
                .Replace("{sheba}", ShebaNumber, StringComparison.OrdinalIgnoreCase);
        }
    }
}
