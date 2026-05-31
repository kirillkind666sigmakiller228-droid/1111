using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using _1111.Data;
using _1111.Models;
using _1111.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace _1111.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var todayLocal = DateOnly.FromDateTime(DateTime.Now);
        var dayStartLocal = todayLocal.ToDateTime(TimeOnly.MinValue);
        var dayEndLocal = dayStartLocal.AddDays(1);
        var dayStartUtc = DateTime.SpecifyKind(dayStartLocal, DateTimeKind.Local).ToUniversalTime();
        var dayEndUtc = DateTime.SpecifyKind(dayEndLocal, DateTimeKind.Local).ToUniversalTime();
        var model = new AdminDashboardViewModel
        {
            TotalComputers = await dbContext.Computers.CountAsync(),
            ActiveBookingsToday = await dbContext.Bookings.CountAsync(b =>
                b.Status == BookingStatus.Active.ToString() &&
                b.StartTimeUtc < dayEndUtc &&
                b.EndTimeUtc > dayStartUtc),
            TotalBookings = await dbContext.Bookings.CountAsync(),
            RegisteredUsers = await dbContext.Users.CountAsync()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Computers()
    {
        var computers = await dbContext.Computers
            .OrderBy(c => c.Id)
            .ToListAsync();
        return View(computers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateComputer(Computer computer)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Computers));
        }

        dbContext.Computers.Add(computer);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Computers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComputer(Computer computer)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Computers));
        }

        var existingComputer = await dbContext.Computers.FirstOrDefaultAsync(c => c.Id == computer.Id);
        if (existingComputer is null)
        {
            return NotFound();
        }

        existingComputer.Name = computer.Name;
        existingComputer.ZoneCategory = computer.ZoneCategory;
        existingComputer.PricePerHour = computer.PricePerHour;
        existingComputer.IsAvailable = computer.IsAvailable;
        existingComputer.Cpu = computer.Cpu;
        existingComputer.Gpu = computer.Gpu;
        existingComputer.Ram = computer.Ram;

        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Computers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComputer(int id)
    {
        var computer = await dbContext.Computers.FirstOrDefaultAsync(c => c.Id == id);
        if (computer is null)
        {
            return NotFound();
        }

        dbContext.Computers.Remove(computer);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Computers));
    }

    [HttpGet]
    public async Task<IActionResult> Bookings()
    {
        var bookings = await dbContext.Bookings
            .Include(b => b.Computer)
            .Include(b => b.User)
            .OrderByDescending(b => b.StartTimeUtc)
            .ToListAsync();

        return View(bookings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmArrival(int id)
    {
        var booking = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        if (booking.Status != BookingStatus.Cancelled.ToString() && booking.Status != BookingStatus.Finished.ToString())
        {
            booking.Status = BookingStatus.Active.ToString();
            await dbContext.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoShowCancel(int id)
    {
        var booking = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        var nowUtc = DateTime.UtcNow;
        var wasActiveNow = booking.Status == BookingStatus.Active.ToString() && booking.StartTimeUtc <= nowUtc && booking.EndTimeUtc > nowUtc;
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

        return RedirectToAction(nameof(Bookings));
    }
}
