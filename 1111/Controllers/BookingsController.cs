using _1111.Data;
using _1111.Models;
using _1111.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace _1111.Controllers;

[Authorize]
public class BookingsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Reserve(string? search, string? zoneCategory, string? status)
    {
        await EnsureComputersSeededAsync();

        var computers = dbContext.Computers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            computers = computers.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Cpu.ToLower().Contains(term) ||
                c.Gpu.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(zoneCategory))
        {
            var normalizedCategory = zoneCategory.ToLower();
            computers = computers.Where(c => c.ZoneCategory.ToLower() == normalizedCategory);
        }

        ViewData["Search"] = search;
        ViewData["ZoneCategory"] = zoneCategory;
        ViewData["Status"] = status;

        var computersList = await computers.OrderBy(c => c.Id).ToListAsync();
        var nowUtc = DateTime.UtcNow;
        var activePcIds = await dbContext.Bookings
            .Where(b => b.Status == BookingStatus.Active.ToString() && b.StartTimeUtc <= nowUtc && b.EndTimeUtc > nowUtc)
            .Select(b => b.ComputerId)
            .Distinct()
            .ToListAsync();
        var activeLookup = activePcIds.ToHashSet();
        foreach (var computer in computersList)
        {
            computer.IsAvailable = !activeLookup.Contains(computer.Id);
        }

        if (status is "available")
        {
            computersList = computersList.Where(c => c.IsAvailable).ToList();
        }
        else if (status is "occupied")
        {
            computersList = computersList.Where(c => !c.IsAvailable).ToList();
        }

        return View(computersList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(int pcId, DateTime startTime, DateTime endTime)
    {
        await EnsureComputersSeededAsync();

        var computer = await dbContext.Computers.FirstOrDefaultAsync(c => c.Id == pcId);
        if (computer is null)
        {
            return NotFound(new { success = false, message = "PC not found." });
        }

        if (endTime <= startTime)
        {
            return BadRequest(new { success = false, message = "End time must be later than start time." });
        }

        var startUtc = DateTime.SpecifyKind(startTime, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endTime, DateTimeKind.Local).ToUniversalTime();
        var hours = (int)(endUtc - startUtc).TotalHours;
        if (hours < 1)
        {
            return BadRequest(new { success = false, message = "Minimum booking duration is 1 hour." });
        }

        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { success = false, message = "User is not authenticated." });
        }

        var activeOrConfirmedCount = await dbContext.Bookings.CountAsync(b =>
            b.UserId == userId &&
            (b.Status == BookingStatus.Confirmed.ToString() || b.Status == BookingStatus.Active.ToString()) &&
            b.StartTimeUtc > DateTime.UtcNow);
        if (activeOrConfirmedCount >= 3)
        {
            TempData["Error"] = "Вы достигли лимита активных бронирований (макс. 3).";
            return BadRequest(new { success = false, message = TempData["Error"]?.ToString() });
        }

        var hasOverlap = await dbContext.Bookings.AnyAsync(b =>
            b.ComputerId == pcId &&
            (b.Status == BookingStatus.Confirmed.ToString() || b.Status == BookingStatus.Active.ToString()) &&
            b.StartTimeUtc < endUtc &&
            b.EndTimeUtc > startUtc);

        if (hasOverlap)
        {
            return BadRequest(new { success = false, message = "This time slot is already taken." });
        }

        var booking = new Booking
        {
            ComputerId = computer.Id,
            UserId = userId,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
            Hours = hours,
            Status = BookingStatus.Confirmed.ToString()
        };

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        // Send confirmation email
        var user = await userManager.FindByIdAsync(userId);
        var startLocal = startUtc.ToLocalTime();
        var endLocal = endUtc.ToLocalTime();
        
        if (user?.Email != null)
        {
            // Send email asynchronously without blocking the main thread
            _ = Task.Run(async () =>
            {
                var emailMessage = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>Подтверждение брони - CYBERZONE</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px; text-align: center; margin-bottom: 30px;'>
                        <h1 style='color: white; margin: 0; font-size: 28px;'>CYBERZONE</h1>
                        <p style='color: #e0e0e0; margin: 5px 0 0 0; font-size: 16px;'>Gaming Club</p>
                    </div>
                    
                    <div style='background: #f8f9fa; padding: 25px; border-radius: 8px; border-left: 4px solid #667eea;'>
                        <h2 style='color: #333; margin-top: 0;'>Подтверждение брони</h2>
                        <p style='margin: 15px 0;'>Уважаемый(ая) {user.UserName},</p>
                        <p style='margin: 15px 0;'>Мы рады подтвердить успешное создание вашей брони в нашем игровом клубе CYBERZONE.</p>
                    </div>
                    
                    <div style='background: #ffffff; padding: 25px; border-radius: 8px; border: 1px solid #e0e0e0; margin: 20px 0;'>
                        <h3 style='color: #667eea; margin-top: 0;'>Детали брони:</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Рабочая станция:</strong></td>
                                <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>{computer.Name}</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Дата и время:</strong></td>
                                <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>{startLocal:dd.MM.yyyy HH:mm} - {endLocal:HH:mm}</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'><strong>Длительность:</strong></td>
                                <td style='padding: 8px 0; border-bottom: 1px solid #f0f0f0;'>{hours} час(ов)</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0;'><strong>Статус:</strong></td>
                                <td style='padding: 8px 0; color: #28a745; font-weight: bold;'>Подтверждено</td>
                            </tr>
                        </table>
                    </div>
                    
                    <div style='background: #fff3cd; padding: 20px; border-radius: 8px; border-left: 4px solid #ffc107; margin: 20px 0;'>
                        <p style='margin: 0; color: #856404;'><strong>Важная информация:</strong></p>
                        <ul style='margin: 10px 0 0 20px; color: #856404;'>
                            <li>Пожалуйста, придите за 5-10 минут до начала времени брони</li>
                            <li>Вы получите напоминание по email за 30 минут до начала</li>
                            <li>При себе необходимо иметь документ удостоверяющий личность</li>
                        </ul>
                    </div>
                    
                    <div style='text-align: center; margin-top: 30px; padding: 20px; border-top: 1px solid #e0e0e0;'>
                        <p style='color: #666; margin: 0;'>С уважением,</p>
                        <p style='color: #667eea; margin: 5px 0; font-weight: bold;'>Команда CYBERZONE</p>
                        <p style='color: #999; margin: 10px 0 0 0; font-size: 12px;'>Это автоматическое уведомление, пожалуйста не отвечайте на это письмо</p>
                    </div>
                </body>
                </html>";

            await emailService.SendEmailAsync(user.Email, "Подтверждение брони - CYBERZONE", emailMessage);
            });
        }

        var intervalLabel = $"{startLocal:dd.MM.yyyy HH:mm} - {endLocal:HH:mm}";

        return Json(new
        {
            success = true,
            message = "Booking created successfully.",
            redirectUrl = Url.Action("Index", "Bookings"),
            intervalLabel
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetOccupiedSlots(int pcId, string date)
    {
        if (!DateOnly.TryParse(date, out var selectedDate))
        {
            return BadRequest(new { success = false, message = "Invalid date." });
        }

        var dayStartLocal = selectedDate.ToDateTime(TimeOnly.MinValue);
        var dayEndLocal = dayStartLocal.AddDays(1);
        var dayStartUtc = DateTime.SpecifyKind(dayStartLocal, DateTimeKind.Local).ToUniversalTime();
        var dayEndUtc = DateTime.SpecifyKind(dayEndLocal, DateTimeKind.Local).ToUniversalTime();

        var bookings = await dbContext.Bookings
            .Where(b => b.ComputerId == pcId &&
                        (b.Status == BookingStatus.Confirmed.ToString() || b.Status == BookingStatus.Active.ToString()) &&
                        b.StartTimeUtc < dayEndUtc &&
                        b.EndTimeUtc > dayStartUtc)
            .Select(b => new { b.StartTimeUtc, b.EndTimeUtc })
            .ToListAsync();

        var occupiedHours = new HashSet<int>();
        foreach (var booking in bookings)
        {
            var localStart = booking.StartTimeUtc.ToLocalTime() < dayStartLocal
                ? dayStartLocal
                : booking.StartTimeUtc.ToLocalTime();
            var localEnd = booking.EndTimeUtc.ToLocalTime() > dayEndLocal
                ? dayEndLocal
                : booking.EndTimeUtc.ToLocalTime();
            var cursor = localStart;
            while (cursor < localEnd)
            {
                occupiedHours.Add(cursor.Hour);
                cursor = cursor.AddHours(1);
            }
        }

        return Json(new { success = true, occupiedHours = occupiedHours.OrderBy(h => h).ToArray() });
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await GenerateUpcomingNotificationsAsync(userId);

        var bookings = await dbContext.Bookings
            .Include(b => b.Computer)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.StartTimeUtc)
            .ToListAsync();

        return View(bookings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var booking = await dbContext.Bookings
            .Include(b => b.Computer)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (booking is null)
        {
            return NotFound();
        }

        var startLocal = booking.StartTimeUtc.ToLocalTime();
        if (booking.Status != BookingStatus.Confirmed.ToString() || startLocal <= DateTime.Now)
        {
            return BadRequest();
        }

        var nowUtc = DateTime.UtcNow;
        var wasActiveNow = booking.StartTimeUtc <= nowUtc && booking.EndTimeUtc > nowUtc;
        var computerId = booking.ComputerId;

        booking.Status = BookingStatus.Cancelled.ToString();
        await dbContext.SaveChangesAsync();

        if (wasActiveNow)
        {
            var hasAnotherActiveBooking = await dbContext.Bookings.AnyAsync(b =>
                b.ComputerId == computerId &&
                (b.Status == BookingStatus.Confirmed.ToString() || b.Status == BookingStatus.Active.ToString()) &&
                b.StartTimeUtc <= nowUtc &&
                b.EndTimeUtc > nowUtc);

            if (!hasAnotherActiveBooking)
            {
                var computer = await dbContext.Computers.FirstOrDefaultAsync(c => c.Id == computerId);
                if (computer is not null)
                {
                    computer.IsAvailable = true;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var computer = await dbContext.Computers.FirstOrDefaultAsync(c => c.Id == id);
        if (computer is null)
        {
            return NotFound();
        }

        return View(computer);
    }

    private async Task GenerateUpcomingNotificationsAsync(string userId)
    {
        var nowUtc = DateTime.UtcNow;
        var upcomingBookings = await dbContext.Bookings
            .Include(b => b.Computer)
            .Where(b =>
                b.UserId == userId &&
                (b.Status == BookingStatus.Confirmed.ToString() || b.Status == BookingStatus.Active.ToString()) &&
                b.StartTimeUtc > nowUtc &&
                b.StartTimeUtc <= nowUtc.AddMinutes(30) &&
                !b.IsNotificationSent) // Only process bookings that haven't had notifications sent
            .ToListAsync();

        var user = await userManager.FindByIdAsync(userId);
        
        foreach (var booking in upcomingBookings)
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
            if (user?.Email != null)
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
                
                // Mark notification as sent to prevent duplicates
                booking.IsNotificationSent = true;
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task EnsureComputersSeededAsync()
    {
        if (await dbContext.Computers.AnyAsync())
        {
            return;
        }

        dbContext.Computers.AddRange(
        [
            new Computer { Id = 1, Name = "Standard PC #01", ZoneCategory = "Standard", PricePerHour = 5m, IsAvailable = true, Cpu = "Intel i5-14600KF", Gpu = "RTX 4070", Ram = "32GB DDR5" },
            new Computer { Id = 2, Name = "Standard PC #02", ZoneCategory = "Standard", PricePerHour = 5m, IsAvailable = false, Cpu = "Ryzen 7 7700X", Gpu = "RTX 4070", Ram = "32GB DDR5" },
            new Computer { Id = 3, Name = "VIP Room #01", ZoneCategory = "VIP", PricePerHour = 15m, IsAvailable = true, Cpu = "Intel i9-14900K", Gpu = "RTX 4090", Ram = "64GB DDR5" },
            new Computer { Id = 4, Name = "VIP Room #02", ZoneCategory = "VIP", PricePerHour = 15m, IsAvailable = false, Cpu = "Ryzen 9 7950X3D", Gpu = "RTX 4090", Ram = "64GB DDR5" },
            new Computer { Id = 5, Name = "Console Bay #01", ZoneCategory = "Console", PricePerHour = 8m, IsAvailable = true, Cpu = "Custom AMD", Gpu = "RDNA 2", Ram = "16GB GDDR6" },
            new Computer { Id = 6, Name = "Console Bay #02", ZoneCategory = "Console", PricePerHour = 8m, IsAvailable = false, Cpu = "Custom AMD", Gpu = "RDNA 2", Ram = "16GB GDDR6" }
        ]);

        await dbContext.SaveChangesAsync();
    }
}
