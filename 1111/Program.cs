using _1111.Data;
using _1111.Models;
using _1111.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});

// Register EmailService
builder.Services.AddScoped<IEmailService, EmailService>();

// Register Background Service
builder.Services.AddHostedService<NotificationBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Computers (
            Id INTEGER NOT NULL CONSTRAINT PK_Computers PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            ZoneCategory TEXT NOT NULL,
            PricePerHour TEXT NOT NULL,
            IsAvailable INTEGER NOT NULL,
            Cpu TEXT NOT NULL,
            Gpu TEXT NOT NULL,
            Ram TEXT NOT NULL
        );
        """);
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Bookings (
            Id INTEGER NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY AUTOINCREMENT,
            ComputerId INTEGER NOT NULL,
            UserId TEXT NOT NULL,
            StartTimeUtc TEXT NOT NULL,
            EndTimeUtc TEXT NOT NULL,
            Hours INTEGER NOT NULL,
            Status TEXT NOT NULL,
            CONSTRAINT FK_Bookings_Computers_ComputerId FOREIGN KEY (ComputerId) REFERENCES Computers (Id) ON DELETE CASCADE
        );
        """);
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Notifications (
            Id INTEGER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY AUTOINCREMENT,
            UserId TEXT NOT NULL,
            Message TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            IsRead INTEGER NOT NULL
        );
        """);

    try
    {
        dbContext.Database.ExecuteSqlRaw("""
            ALTER TABLE Bookings ADD COLUMN EndTimeUtc TEXT NOT NULL DEFAULT '0001-01-01T00:00:00';
            """);
    }
    catch (SqliteException)
    {
        // Column already exists for databases created with newer schema.
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("""
            ALTER TABLE Bookings ADD COLUMN IsNotificationSent INTEGER NOT NULL DEFAULT 0;
            """);
    }
    catch (SqliteException)
    {
        // Column already exists for databases created with newer schema.
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ??????? ??? ? Program.cs ????? app.Run();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    // ??????? ???? Admin
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    // ????????? ???? ????
    var user = await userManager.FindByEmailAsync("kirillkind666@gmail.com");
    if (user != null)
    {
        await userManager.AddToRoleAsync(user, "Admin");
    }
}


app.Run();
