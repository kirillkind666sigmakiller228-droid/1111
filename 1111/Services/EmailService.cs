using System.Net;
using System.Net.Mail;

namespace _1111.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string message)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            
            var fromAddress = new MailAddress(smtpSettings["FromEmail"]!, smtpSettings["FromName"]);
            var toAddress = new MailAddress(toEmail);
            
            using var smtpClient = new SmtpClient
            {
                Host = smtpSettings["Server"]!,
                Port = int.Parse(smtpSettings["Port"]!),
                EnableSsl = bool.Parse(smtpSettings["EnableSsl"]!),
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpSettings["Username"]!, smtpSettings["Password"]!),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var mailMessage = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = message,
                IsBodyHtml = true,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };

            await smtpClient.SendMailAsync(mailMessage);
            
            _logger.LogInformation("Email sent successfully to {Email} with subject {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("RESET PASSWORD EMAIL ERROR: " + ex.Message);
            Console.WriteLine("FULL ERROR: " + ex.ToString());
            _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", toEmail, subject);
            return false;
        }
    }
}
