using System.Text.Json;

namespace SangbariInvoice.Helpers
{
    public sealed class SmsAppSettings
    {
        public string CardNumber { get; init; } = "";
        public string ShebaNumber { get; init; } = "";

        public static SmsAppSettings Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("فایل appsettings.json پیدا نشد.", path);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("Sms", out var sms))
                throw new InvalidDataException("بخش Sms در appsettings.json وجود ندارد.");

            var card = sms.TryGetProperty("CardNumber", out var cardValue) ? cardValue.GetString() : null;
            var sheba = sms.TryGetProperty("ShebaNumber", out var shebaValue) ? shebaValue.GetString() : null;

            if (string.IsNullOrWhiteSpace(card) || string.IsNullOrWhiteSpace(sheba))
                throw new InvalidDataException("شماره کارت و شبا را در appsettings.json تنظیم کنید.");

            return new SmsAppSettings { CardNumber = card, ShebaNumber = sheba };
        }
    }
}
