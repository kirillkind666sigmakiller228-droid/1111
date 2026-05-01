using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace _1111.Controllers;

[Authorize]
public class BookingsController : Controller
{
    [HttpGet]
    public IActionResult Reserve()
    {
        return View();
    }
}
