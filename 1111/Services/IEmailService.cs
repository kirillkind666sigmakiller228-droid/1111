namespace _1111.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string message);
}
