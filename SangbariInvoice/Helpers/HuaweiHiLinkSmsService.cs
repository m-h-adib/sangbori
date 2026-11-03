using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace SangbariInvoice.Helpers;

/// <summary>
/// Sends SMS through the XML HTTP API exposed by Huawei HiLink modems (including E303).
/// </summary>
public static class HuaweiHiLinkSmsService
{
    private static readonly SemaphoreSlim SendLock = new(1, 1);
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task SendAsync(
        string modemAddress,
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modemAddress))
            throw new ArgumentException("آدرس مودم تنظیم نشده است.", nameof(modemAddress));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("شماره موبایل وارد نشده است.", nameof(phoneNumber));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("متن پیامک خالی است.", nameof(message));

        var baseUri = BuildBaseUri(modemAddress);
        var phone = NormalizePhoneNumber(phoneNumber);

        await SendLock.WaitAsync(cancellationToken);
        try
        {
            var (session, token) = await GetSessionAndTokenAsync(baseUri, cancellationToken);
            await SendMessageAsync(baseUri, session, token, phone, message, cancellationToken);
        }
        finally
        {
            SendLock.Release();
        }
    }

    private static Uri BuildBaseUri(string address)
    {
        var normalized = address.Trim().TrimEnd('/');
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            normalized = "http://" + normalized;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            throw new ArgumentException("آدرس مودم معتبر نیست.", nameof(address));

        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }

    private static async Task<(string Session, string Token)> GetSessionAndTokenAsync(
        Uri baseUri, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(
            new Uri(baseUri, "api/webserver/SesTokInfo"), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = XDocument.Parse(body);
        var session = xml.Descendants().FirstOrDefault(x =>
            x.Name.LocalName.Equals("SesInfo", StringComparison.OrdinalIgnoreCase))?.Value;
        var token = xml.Descendants().FirstOrDefault(x =>
            x.Name.LocalName.Equals("TokInfo", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(session))
            session = response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? cookies.FirstOrDefault()?.Split(';')[0]
                : null;

        if (string.IsNullOrWhiteSpace(token) &&
            response.Headers.TryGetValues("__RequestVerificationToken", out var tokens))
            token = tokens.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "مودم Session یا Token معتبر برنگرداند. آدرس مودم و دسترسی به پنل HiLink را بررسی کنید. پاسخ: " + body);

        return (session.Trim(), token.Trim());
    }

    private static async Task SendMessageAsync(
        Uri baseUri, string session, string token, string phone, string message,
        CancellationToken cancellationToken)
    {
        var requestXml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("request",
                new XElement("Index", "-1"),
                new XElement("Phones", new XElement("Phone", phone)),
                new XElement("Sca"),
                new XElement("Content", message),
                new XElement("Length", "-1"),
                new XElement("Reserved", "1"),
                new XElement("Date", "-1"),
                new XElement("SendType", "0")));

        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri(baseUri, "api/sms/send-sms"));
        request.Headers.TryAddWithoutValidation("Cookie", session);
        request.Headers.TryAddWithoutValidation("__RequestVerificationToken", token);
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        request.Headers.TryAddWithoutValidation("Origin", baseUri.GetLeftPart(UriPartial.Authority));
        request.Headers.TryAddWithoutValidation("Referer", new Uri(baseUri, "html/smsinbox.html").ToString());
        request.Content = new StringContent(
            requestXml.ToString(SaveOptions.DisableFormatting),
            new UTF8Encoding(false),
            "text/xml");

        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = XDocument.Parse(body).Root?.Value?.Trim();
        if (!string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("مودم ارسال پیامک را نپذیرفت. پاسخ مودم: " + body);
    }

    private static string NormalizePhoneNumber(string input)
    {
        var value = input.Trim().Replace(" ", "").Replace("-", "");
        if (value.StartsWith("00", StringComparison.Ordinal))
            value = "+" + value[2..];

        if (value.StartsWith("09", StringComparison.Ordinal) && value.Length == 11)
            value = "+98" + value[1..];
        else if (value.StartsWith("9", StringComparison.Ordinal) && value.Length == 10)
            value = "+98" + value;
        else if (value.StartsWith("98", StringComparison.Ordinal))
            value = "+" + value;

        var digits = value.StartsWith("+", StringComparison.Ordinal) ? value[1..] : value;
        if (digits.Length < 8 || digits.Length > 15 || digits.Any(c => !char.IsDigit(c)))
            throw new ArgumentException("شماره موبایل معتبر نیست.", nameof(input));

        return value;
    }
}
