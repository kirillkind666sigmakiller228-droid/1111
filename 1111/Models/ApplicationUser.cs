using Microsoft.AspNetCore.Identity;

namespace _1111.Models;

public class ApplicationUser : IdentityUser
{
    public decimal Balance { get; set; }
}
