using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;

    public SmtpEmailService(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string body, EmailAttachment? attachment = null)
    {
        using var client = BuildClient();
        using var message = BuildMessage(toEmail, toName, subject, body);
        using var stream = attachment is null ? null : new MemoryStream(attachment.Bytes);
        if (attachment is not null && stream is not null)
            message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));

        await client.SendMailAsync(message);
    }

    private SmtpClient BuildClient() => new(_options.Host, _options.Port)
    {
        Credentials = new NetworkCredential(_options.Username, _options.Password),
        EnableSsl = _options.EnableSsl,
        Timeout = 10_000
    };

    private MailMessage BuildMessage(string toEmail, string toName, string subject, string body)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail, toName));
        return message;
    }
}
