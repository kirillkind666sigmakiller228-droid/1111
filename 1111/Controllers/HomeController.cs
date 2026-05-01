using System.Diagnostics;
using _1111.Models;
using Microsoft.AspNetCore.Mvc;

namespace _1111.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var model = new GamingLandingViewModel
            {
                Zones =
                [
                    new GamingZone
                    {
                        Name = "Standard PC Zone",
                        Description = "High-performance gaming PCs with RGB lighting.",
                        Hardware = "RTX 4070 + 32GB RAM + 240Hz",
                        PricePerHour = 5m,
                        ImageUrl = "/img/standard.jpg",
                        Computers =
                        [
                            new ComputerSpec { Name = "PC-01", Cpu = "Intel i5-14600KF", Gpu = "RTX 4070", Ram = "32GB DDR5" },
                            new ComputerSpec { Name = "PC-02", Cpu = "Ryzen 7 7700X", Gpu = "RTX 4070", Ram = "32GB DDR5" }
                        ]
                    },
                    new GamingZone
                    {
                        Name = "VIP Room",
                        Description = "Premium private gaming room with luxury setup.",
                        Hardware = "Private + RTX 4090 + 165Hz",
                        PricePerHour = 15m,
                        ImageUrl = "/img/vip.jpg",
                        Computers =
                        [
                            new ComputerSpec { Name = "VIP-01", Cpu = "Intel i9-14900K", Gpu = "RTX 4090", Ram = "64GB DDR5" },
                            new ComputerSpec { Name = "VIP-02", Cpu = "Ryzen 9 7950X3D", Gpu = "RTX 4090", Ram = "64GB DDR5" }
                        ]
                    },
                    new GamingZone
                    {
                        Name = "Console Zone",
                        Description = "Latest consoles with comfortable gaming chairs.",
                        Hardware = "PS5 + Xbox Series X + Switch",
                        PricePerHour = 8m,
                        ImageUrl = "/img/console.jpg",
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
    }
}
