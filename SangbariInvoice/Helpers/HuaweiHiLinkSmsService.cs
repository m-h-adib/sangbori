using System.Net;
using System.Text;
using System.Xml.Linq;

public static class HuaweiHiLinkSmsService
{
    private static readonly HttpClient Client = new(
        new HttpClientHandler
        {
            UseCookies = false
        })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public static async Task SendAsync(
        string modemAddress,
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        await Semaphore.WaitAsync(cancellationToken);

        try
        {
            var baseAddress = modemAddress.TrimEnd('/');

            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("request",
                    new XElement("Index", "-1"),
                    new XElement("Phones",
                        new XElement("Phone", phoneNumber)),
                    new XElement("Sca"),
                    new XElement("Content", message),
                    new XElement("Length", message.Length),
                    new XElement("Reserved", "0"),
                    new XElement("Date",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                )
            );

            var xmlContent =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                xml.Root!.ToString(SaveOptions.DisableFormatting);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseAddress}/api/sms/send-sms");

            request.Headers.TryAddWithoutValidation(
                "X-Requested-With", "XMLHttpRequest");

            request.Headers.TryAddWithoutValidation(
                "Origin", baseAddress);

            request.Headers.TryAddWithoutValidation(
                "Referer", $"{baseAddress}/html/smsinbox.html");

            request.Content = new StringContent(
                xmlContent,
                Encoding.UTF8,
                "application/x-www-form-urlencoded");

            using var response = await Client.SendAsync(
                request,
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = XDocument.Parse(responseText);

            if (result.Root?.Value.Trim() != "OK")
            {
                throw new Exception(
                    $"مودم ارسال پیامک را تأیید نکرد. پاسخ: {responseText}");
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }
}