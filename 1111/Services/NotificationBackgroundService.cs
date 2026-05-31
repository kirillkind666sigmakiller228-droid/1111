using _1111.Data;
using _1111.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace _1111.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendNotificationsAsync();
                
                // Run every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in notification background service");
                // Wait 1 minute before retrying after error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Notification Background Service is stopping.");
    }

    private async Task CheckAndSendNotificationsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var nowUtc = DateTime.UtcNow;
        
        // Get all users with upcoming bookings that haven't had notifications sent
        var upcomingBookings = await dbContext.Bookings
            .Include(b => b.Computer)
            .Where(b =>
                (b.Status == BookingStatus.Confirmed.ToString() || b.Status == BookingStatus.Active.ToString()) &&
                b.StartTimeUtc > nowUtc &&
                b.StartTimeUtc <= nowUtc.AddMinutes(30) &&
                !b.IsNotificationSent)
            .ToListAsync();

        // Group by user to avoid duplicate emails
        var bookingsByUser = upcomingBookings.GroupBy(b => b.UserId);

        foreach (var userBookings in bookingsByUser)
        {
            var userId = userBookings.Key;
            var user = await userManager.FindByIdAsync(userId);
            
            if (user?.Email == null) continue;

            foreach (var booking in userBookings)
            {
                var timeUntilStart = booking.StartTimeUtc - nowUtc;
                var minutesUntilStart = (int)timeUntilStart.TotalMinutes;
                
                // Generate adaptive message based on remaining time
                string message;
                string emailSubject;
                string emailContent;
                
                if (minutesUntilStart == 30)
                {
                    message = $"Напоминание: Ваша бронь в CYBERZONE начнется через 30 минут!";
                    emailSubject = "Напоминание о брони (30 минут) - CYBERZONE";
                    emailContent = $@"
                        <div style='background: #e3f2fd; padding: 25px; border-radius: 8px; border-left: 4px solid #2196f3; margin-bottom: 20px;'>
                            <h2 style='color: #1976d2; margin-top: 0;'>Напоминание о брони</h2>
                            <p style='margin: 15px 0;'>Уважаемый(ая) {user.UserName},</p>
                            <p style='margin: 15px 0;'>Напоминаем, что ваша бронь в игровом клубе CYBERZONE начнется через 30 минут.</p>
                        </div>";
                }
                else if (minutesUntilStart > 1 && minutesUntilStart < 30)
                {
                    message = $"Ваша бронь начнется совсем скоро — через {minutesUntilStart} мин. Поторопитесь!";
                    emailSubject = $"Срочное напоминание о брони ({minutesUntilStart} минут) - CYBERZONE";
                    emailContent = $@"
                        <div style='background: #fff3e0; padding: 25px; border-radius: 8px; border-left: 4px solid #ff9800; margin-bottom: 20px;'>
                            <h2 style='color: #f57c00; margin-top: 0;'>Срочное напоминание</h2>
                            <p style='margin: 15px 0;'>Уважаемый(ая) {user.UserName},</p>
                            <p style='margin: 15px 0; font-weight: bold;'>Ваша бронь начнется совсем скоро — через {minutesUntilStart} минут. Поторопитесь!</p>
                        </div>";
                }
                else if (minutesUntilStart <= 1)
                {
                    message = "Ваша бронь начинается прямо сейчас!";
                    emailSubject = "СРОЧНО: Ваша бронь начинается сейчас! - CYBERZONE";
                    emailContent = $@"
                        <div style='background: #ffebee; padding: 25px; border-radius: 8px; border-left: 4px solid #f44336; margin-bottom: 20px;'>
                            <h2 style='color: #d32f2f; margin-top: 0;'>СРОЧНО: Время начинать!</h2>
                            <p style='margin: 15px 0;'>Уважаемый(ая) {user.UserName},</p>
                            <p style='margin: 15px 0; font-weight: bold; color: #d32f2f;'>Ваша бронь в CYBERZONE начинается прямо сейчас!</p>
                        </div>";
                }
                else
                {
                    continue; // Skip if outside notification range
                }

                var pcName = booking.Computer?.Name ?? "Unknown";
                var zoneName = booking.Computer?.ZoneCategory ?? "General";

                // Create internal notification
                dbContext.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Message = message,
                    CreatedAt = nowUtc,
                    IsRead = false
                });

                // Send email notification
                try
                {
                    var fullEmailMessage = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                        <title>Напоминание о брони - CYBERZONE</title>
                    </head>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px; text-align: center; margin-bottom: 30px;'>
                            <h1 style='color: white; margin: 0; font-size: 28px;'>CYBERZONE</h1>
                            <p style='color: #e0e0e0; margin: 5px 0 0 0; font-size: 16px;'>Gaming Club</p>
                        </div>
                        
                        {emailContent}
                        
                        <div style='background: #ffffff; padding: 25px; border-radius: 8px; border: 1px solid #e0e0e0; margin: 20px 0;'>
                            <h3 style='color: #667eea; margin-top: 0;'>Детали брони:</h3>
                            <table style='width: 100%; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Рабочая станция:</strong></td>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>ПК №{booking.ComputerId}: {pcName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Зона:</strong></td>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>{zoneName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Время начала:</strong></td>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>{booking.StartTimeUtc.ToLocalTime():dd.MM.yyyy HH:mm}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Длительность:</strong></td>
                                    <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>{booking.Hours} час(ов)</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>Статус:</strong></td>
                                    <td style='padding: 8px 0; color: #28a745; font-weight: bold;'>{booking.Status}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <div style='background: #fff3cd; padding: 20px; border-radius: 8px; border-left: 4px solid #ffc107; margin: 20px 0;'>
                            <p style='margin: 0; color: #856404;'><strong>Важная информация:</strong></p>
                            <ul style='margin: 10px 0 0 20px; color: #856404;'>
                                <li>Пожалуйста, придите за 5-10 минут до начала времени брони</li>
                                <li>При себе необходимо иметь документ удостоверяющий личность</li>
                                <li>Наша команда готова предоставить вам лучший игровой опыт</li>
                            </ul>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px; padding: 20px; border-top: 1px solid #e0e0e0;'>
                            <p style='color: #666; margin: 0;'>С уважением,</p>
                            <p style='color: #667eea; margin: 5px 0; font-weight: bold;'>Команда CYBERZONE</p>
                            <p style='color: #999; margin: 10px 0 0 0; font-size: 12px;'>Это автоматическое уведомление, пожалуйста не отвечайте на это письмо</p>
                        </div>
                    </body>
                    </html>";

                    await emailService.SendEmailAsync(user.Email, emailSubject, fullEmailMessage);
                    _logger.LogInformation("Adaptive reminder email sent to {Email} for booking {BookingId} ({Minutes} minutes)", user.Email, booking.Id, minutesUntilStart);
                    
                    // Mark notification as sent to prevent duplicates
                    booking.IsNotificationSent = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send adaptive reminder email to {Email} for booking {BookingId}", user.Email, booking.Id);
                }
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Created {Count} new adaptive notifications", dbContext.ChangeTracker.Entries<Notification>().Count());
        }
    }
}
