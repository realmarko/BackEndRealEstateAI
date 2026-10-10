namespace RealEstate.Api.Services;

public record EmailAttachment(byte[] Bytes, string FileName, string ContentType);

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string body, EmailAttachment? attachment = null);
}
