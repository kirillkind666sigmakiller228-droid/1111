using _1111.Models;
using _1111.Services;
using _1111.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace _1111.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailService emailService) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await userManager.FindByEmailAsync(model.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Name,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Please enter your email address.");
            return View();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            TempData["SuccessMessage"] = "If an account with that email exists, a password reset link has been sent.";
            return View();
        }

        // Generate password reset token
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        
        Console.WriteLine("GENERATED TOKEN: " + token);
        Console.WriteLine("TOKEN LENGTH: " + token?.Length);
        
        // Encode token to handle special characters
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token ?? string.Empty));
        Console.WriteLine("ENCODED TOKEN: " + encodedToken);
        
        // Create reset link with encoded token
        var resetLink = Url.Action(
            "ResetPassword", 
            "Account", 
            new { email = email, token = encodedToken }, 
            protocol: Request.Scheme);
            
        Console.WriteLine("Reset Link: " + resetLink);
        Console.WriteLine("Request Scheme: " + Request.Scheme);
        Console.WriteLine("Request Host: " + Request.Host);
        Console.WriteLine("Full URL Base: " + Request.Scheme + "://" + Request.Host);

        // Send email with reset link
        var emailMessage = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Сброс пароля - CYBERZONE</title>
            </head>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px; text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: white; margin: 0; font-size: 28px;'>CYBERZONE</h1>
                    <p style='color: #e0e0e0; margin: 5px 0 0 0; font-size: 16px;'>Gaming Club</p>
                </div>
                
                <div style='background: #f8f9fa; padding: 25px; border-radius: 8px; border-left: 4px solid #667eea; margin-bottom: 20px;'>
                    <h2 style='color: #333; margin-top: 0;'>Сброс пароля</h2>
                    <p style='margin: 15px 0;'>Здравствуйте, {user.UserName}!</p>
                    <p style='margin: 15px 0;'>Мы получили запрос на сброс пароля для вашей учетной записи в CYBERZONE.</p>
                    <p style='margin: 15px 0;'>Для сброса пароля перейдите по ссылке ниже:</p>
                </div>
                
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{resetLink}' style='background: linear-gradient(90deg, #3B82F6, #8B5CF6); color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>Сбросить пароль</a>
                </div>
                
                <div style='background: #fff3cd; padding: 20px; border-radius: 8px; border-left: 4px solid #ffc107; margin: 20px 0;'>
                    <p style='margin: 0; color: #856404;'><strong>Важная информация:</strong></p>
                    <ul style='margin: 10px 0 0 20px; color: #856404;'>
                        <li>Эта ссылка действительна в течение 24 часов</li>
                        <li>Если вы не запрашивали сброс пароля, проигнорируйте это письмо</li>
                        <li>Никогда не передавайте эту ссылку другим лицам</li>
                    </ul>
                </div>
                
                <div style='text-align: center; margin-top: 30px; padding: 20px; border-top: 1px solid #e0e0e0;'>
                    <p style='color: #666; margin: 0;'>С уважением,</p>
                    <p style='color: #667eea; margin: 5px 0; font-weight: bold;'>Команда CYBERZONE</p>
                    <p style='color: #999; margin: 10px 0 0 0; font-size: 12px;'>Это автоматическое уведомление, пожалуйста не отвечайте на это письмо</p>
                </div>
            </body>
            </html>";

        // Send email asynchronously
        _ = Task.Run(async () =>
        {
            await emailService.SendEmailAsync(email, "CyberZone Password Reset", emailMessage);
        });

        TempData["SuccessMessage"] = "If an account with that email exists, a password reset link has been sent.";
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            ViewBag.ErrorMessage = "Invalid password reset link.";
            return View();
        }

        Console.WriteLine("RESET PASSWORD - Received token: " + token);
        
        // Decode the token
        try
        {
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            Console.WriteLine("RESET PASSWORD - Decoded token: " + decodedToken);
            
            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = decodedToken
            };

            return View(model);
        }
        catch (Exception ex)
        {
            Console.WriteLine("RESET PASSWORD - Token decode error: " + ex.Message);
            ViewBag.ErrorMessage = "Invalid password reset link.";
            return View();
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            ViewBag.ErrorMessage = "Invalid password reset attempt.";
            return View();
        }

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        ViewBag.SuccessMessage = "Пароль успешно изменен. Теперь вы можете войти.";
        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return View(user);
    }
}
