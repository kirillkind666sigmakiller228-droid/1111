using System.Diagnostics;
using _1111.Data;
using _1111.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace _1111.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _dbContext;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            await EnsureComputersSeededAsync();
            var nowUtc = DateTime.UtcNow;
            var occupiedCategories = await _dbContext.Bookings
                .Include(b => b.Computer)
                .Where(b => b.Status == "Active" && b.StartTimeUtc <= nowUtc && b.EndTimeUtc > nowUtc && b.Computer != null)
                .Select(b => b.Computer!.ZoneCategory)
                .Distinct()
                .ToListAsync();
            var occupiedLookup = occupiedCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var model = new GamingLandingViewModel
            {
                Zones =
                [
                    new GamingZone
                    {
                        ZoneCategory = "Standard",
                        Name = "Standard PC Zone",
                        Description = "High-performance gaming PCs with RGB lighting.",
                        Hardware = "RTX 4070 + 32GB RAM + 240Hz",
                        PricePerHour = 5m,
                        ImageUrl = "/img/GamingStation.png",
                        IsOccupiedNow = occupiedLookup.Contains("Standard"),
                        Computers =
                        [
                            new ComputerSpec { Name = "PC-01", Cpu = "Intel i5-14600KF", Gpu = "RTX 4070", Ram = "32GB DDR5" },
                            new ComputerSpec { Name = "PC-02", Cpu = "Ryzen 7 7700X", Gpu = "RTX 4070", Ram = "32GB DDR5" }
                        ]
                    },
                    new GamingZone
                    {
                        ZoneCategory = "VIP",
                        Name = "VIP Room",
                        Description = "Premium private gaming room with luxury setup.",
                        Hardware = "Private + RTX 4090 + 165Hz",
                        PricePerHour = 15m,
                        ImageUrl = "/img/VIProoms.png",
                        IsOccupiedNow = occupiedLookup.Contains("VIP"),
                        Computers =
                        [
                            new ComputerSpec { Name = "VIP-01", Cpu = "Intel i9-14900K", Gpu = "RTX 4090", Ram = "64GB DDR5" },
                            new ComputerSpec { Name = "VIP-02", Cpu = "Ryzen 9 7950X3D", Gpu = "RTX 4090", Ram = "64GB DDR5" }
                        ]
                    },
                    new GamingZone
                    {
                        ZoneCategory = "Console",
                        Name = "Console Zone",
                        Description = "Latest consoles with comfortable gaming chairs.",
                        Hardware = "PS5 + Xbox Series X + Switch",
                        PricePerHour = 8m,
                        ImageUrl = "/img/ConsoleZone.png",
                        IsOccupiedNow = occupiedLookup.Contains("Console"),
                        Computers =
                        [
                            new ComputerSpec { Name = "Console Bay 1", Cpu = "Custom AMD", Gpu = "RDNA 2", Ram = "16GB GDDR6" },
                            new ComputerSpec { Name = "Console Bay 2", Cpu = "Custom AMD", Gpu = "RDNA 2", Ram = "16GB GDDR6" }
                        ]
                    }
                ]
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task EnsureComputersSeededAsync()
        {
            if (await _dbContext.Computers.AnyAsync())
            {
                return;
            }

            _dbContext.Computers.AddRange(
            [
                new Computer { Id = 1, Name = "Standard PC #01", ZoneCategory = "Standard", PricePerHour = 5m, IsAvailable = true, Cpu = "Intel i5-14600KF", Gpu = "RTX 4070", Ram = "32GB DDR5" },
                new Computer { Id = 2, Name = "Standard PC #02", ZoneCategory = "Standard", PricePerHour = 5m, IsAvailable = true, Cpu = "Ryzen 7 7700X", Gpu = "RTX 4070", Ram = "32GB DDR5" },
                new Computer { Id = 3, Name = "VIP Room #01", ZoneCategory = "VIP", PricePerHour = 15m, IsAvailable = true, Cpu = "Intel i9-14900K", Gpu = "RTX 4090", Ram = "64GB DDR5" },
                new Computer { Id = 4, Name = "VIP Room #02", ZoneCategory = "VIP", PricePerHour = 15m, IsAvailable = true, Cpu = "Ryzen 9 7950X3D", Gpu = "RTX 4090", Ram = "64GB DDR5" },
                new Computer { Id = 5, Name = "Console Bay #01", ZoneCategory = "Console", PricePerHour = 8m, IsAvailable = true, Cpu = "Custom AMD", Gpu = "RDNA 2", Ram = "16GB GDDR6" },
                new Computer { Id = 6, Name = "Console Bay #02", ZoneCategory = "Console", PricePerHour = 8m, IsAvailable = true, Cpu = "Custom AMD", Gpu = "RDNA 2", Ram = "16GB GDDR6" }
            ]);

            await _dbContext.SaveChangesAsync();
        }
    }
}
