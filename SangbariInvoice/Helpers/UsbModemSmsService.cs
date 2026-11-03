using System.IO.Ports;
using System.Text;

namespace SangbariInvoice.Helpers;

/// <summary>
/// Sends SMS messages through a USB GSM/3G modem using AT commands and UCS2 PDU.
/// A modem/COM port must be used by only one process at a time.
/// </summary>
public sealed class UsbModemSmsService
{
    private static readonly SemaphoreSlim PortLock = new(1, 1);
    private static int _messageReference;

    public static Task SendAsync(
        string portName,
        int baudRate,
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Send(portName, baudRate, phoneNumber, message, cancellationToken),
            cancellationToken);
    }

    private static void Send(
        string portName,
        int baudRate,
        string phoneNumber,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("شماره پورت مودم تنظیم نشده است.", nameof(portName));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("متن پیامک خالی است.", nameof(message));

        var normalizedNumber = NormalizePhoneNumber(phoneNumber);
        var pdus = BuildSubmitPdus(normalizedNumber, message);

        PortLock.Wait(cancellationToken);
        try
        {
            using var port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                Encoding = Encoding.ASCII,
                ReadTimeout = 200,
                WriteTimeout = 5000,
                DtrEnable = true,
                RtsEnable = true
            };

            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();

            ExecuteCommand(port, "AT", "OK", 5000, cancellationToken);
            ExecuteCommand(port, "ATE0", "OK", 5000, cancellationToken);
            ExecuteCommand(port, "AT+CMEE=2", "OK", 5000, cancellationToken);
            ExecuteCommand(port, "AT+CMGF=0", "OK", 5000, cancellationToken);

            foreach (var pdu in pdus)
            {
                cancellationToken.ThrowIfCancellationRequested();

                port.DiscardInBuffer();
                port.Write($"AT+CMGS={pdu.Length}\r");
                var prompt = ReadResponse(port, ">", 10000, cancellationToken);
                if (!prompt.Contains('>'))
                    throw new InvalidOperationException($"مودم آماده دریافت متن پیامک نشد: {prompt}");

                port.Write(pdu.Hex);
                port.Write(new byte[] { 0x1A }, 0, 1);

                var response = ReadResponse(port, "OK", 60000, cancellationToken);
                if (response.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                    !response.Contains("+CMGS:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"ارسال پیامک توسط مودم تأیید نشد: {response}");
                }
            }
        }
        finally
        {
            PortLock.Release();
        }
    }

    private static void ExecuteCommand(
        SerialPort port,
        string command,
        string expected,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        port.DiscardInBuffer();
        port.Write(command + "\r");
        var response = ReadResponse(port, expected, timeoutMs, cancellationToken);

        if (!response.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
            response.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"خطا در اجرای دستور مودم {command}: {response}");
        }
    }

    private static string ReadResponse(
        SerialPort port,
        string expected,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var timer = System.Diagnostics.Stopwatch.StartNew();

        while (timer.ElapsedMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = port.ReadExisting();
            if (!string.IsNullOrEmpty(chunk))
            {
                response.Append(chunk);
                var current = response.ToString();

                if (current.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
                    current.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                    current.Contains("+CMS ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            Thread.Sleep(40);
        }

        throw new TimeoutException($"پاسخی از مودم دریافت نشد. پاسخ دریافتی: {response}");
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string((phoneNumber ?? "").Where(char.IsDigit).ToArray());

        if (digits.StartsWith("0098", StringComparison.Ordinal))
            digits = digits[2..];
        else if (digits.StartsWith("0", StringComparison.Ordinal))
            digits = "98" + digits[1..];
        else if (!digits.StartsWith("98", StringComparison.Ordinal))
            digits = "98" + digits;

        if (digits.Length < 11 || digits.Length > 15)
            throw new ArgumentException("شماره موبایل معتبر نیست.", nameof(phoneNumber));

        return digits;
    }

    private static List<(string Hex, int Length)> BuildSubmitPdus(string destination, string message)
    {
        var userData = Encoding.BigEndianUnicode.GetBytes(message);
        var chunks = SplitUserData(userData, 134);
        var isMultipart = chunks.Count > 1;
        var reference = (byte)(Interlocked.Increment(ref _messageReference) % 256);
        var result = new List<(string Hex, int Length)>();

        for (var index = 0; index < chunks.Count; index++)
        {
            using var pdu = new MemoryStream();

            // SMSC length = 0: use the SMSC configured by the SIM/operator.
            pdu.WriteByte(0x00);

            // SMS-SUBMIT; set UDHI when concatenation header is present.
            pdu.WriteByte(isMultipart ? (byte)0x51 : (byte)0x11);
            pdu.WriteByte(0x00); // Message reference
            pdu.WriteByte((byte)destination.Length);
            pdu.WriteByte(0x91); // International number
            pdu.Write(ConvertSemiOctets(destination), 0, ConvertSemiOctets(destination).Length);
            pdu.WriteByte(0x00); // PID
            pdu.WriteByte(0x08); // UCS2
            pdu.WriteByte(0xAA); // Validity period

            var chunk = chunks[index];
            var userDataLength = chunk.Length;

            if (isMultipart)
            {
                // 8-bit concatenation reference: 05 00 03 ref total sequence
                pdu.WriteByte((byte)(chunk.Length + 6));
                pdu.Write(new byte[]
                {
                    0x05, 0x00, 0x03, reference,
                    (byte)chunks.Count, (byte)(index + 1)
                }, 0, 6);
                userDataLength += 6;
            }
            else
            {
                pdu.WriteByte((byte)userDataLength);
            }

            pdu.Write(chunk, 0, chunk.Length);
            var bytes = pdu.ToArray();

            // AT+CMGS length excludes the SMSC length octet and SMSC data.
            result.Add((Convert.ToHexString(bytes), bytes.Length - 1));
        }

        return result;
    }

    private static List<byte[]> SplitUserData(byte[] data, int maxBytes)
    {
        if (data.Length <= 140)
            return new List<byte[]> { data };

        var chunks = new List<byte[]>();
        var offset = 0;

        while (offset < data.Length)
        {
            var length = Math.Min(maxBytes, data.Length - offset);

            // UCS2 characters use two bytes; do not split a UTF-16 code unit pair.
            if (length < data.Length - offset && length % 2 != 0)
                length--;

            var chunk = new byte[length];
            Buffer.BlockCopy(data, offset, chunk, 0, length);
            chunks.Add(chunk);
            offset += length;
        }

        return chunks;
    }

    private static byte[] ConvertSemiOctets(string digits)
    {
        var padded = digits.Length % 2 == 0 ? digits : digits + "F";
        var result = new byte[padded.Length / 2];

        for (var i = 0; i < padded.Length; i += 2)
            result[i / 2] = Convert.ToByte($"{padded[i + 1]}{padded[i]}", 16);

        return result;
    }
}
