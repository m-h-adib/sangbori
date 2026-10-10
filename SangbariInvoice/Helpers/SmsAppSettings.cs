using System.Text.Json;

namespace SangbariInvoice.Helpers
{
    public sealed class SmsAppSettings
    {
        public string Username { get; init; } = "";
        public string Password { get; init; } = "";
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

            static string ReadString(JsonElement section, string name) =>
                section.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

            var settings = new SmsAppSettings
            {
                Username = ReadString(sms, "Username"),
                Password = ReadString(sms, "Password"),
                CardNumber = ReadString(sms, "CardNumber"),
                ShebaNumber = ReadString(sms, "ShebaNumber")
            };

            if (string.IsNullOrWhiteSpace(settings.Username) ||
                string.IsNullOrWhiteSpace(settings.Password))
                throw new InvalidDataException("نام کاربری و رمز عبور ملی‌پیامک را در appsettings.json تنظیم کنید.");

            if (string.IsNullOrWhiteSpace(settings.CardNumber) ||
                string.IsNullOrWhiteSpace(settings.ShebaNumber))
                throw new InvalidDataException("شماره کارت و شبا را در appsettings.json تنظیم کنید.");

            return settings;
        }
    }
}
