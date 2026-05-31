namespace _1111.Models;

public class Booking
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public int Hours { get; set; }
    public string Status { get; set; } = BookingStatus.Confirmed.ToString();
    public bool IsNotificationSent { get; set; } = false;

    public Computer? Computer { get; set; }
    public ApplicationUser? User { get; set; }
}
