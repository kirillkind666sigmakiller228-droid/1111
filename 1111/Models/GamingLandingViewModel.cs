namespace _1111.Models;

public class GamingLandingViewModel
{
    public List<GamingZone> Zones { get; set; } = [];
}

public class GamingZone
{
    public string ZoneCategory { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Hardware { get; set; } = string.Empty;
    public decimal PricePerHour { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsOccupiedNow { get; set; }
    public List<ComputerSpec> Computers { get; set; } = [];
}

public class ComputerSpec
{
    public string Name { get; set; } = string.Empty;
    public string Cpu { get; set; } = string.Empty;
    public string Gpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
}
