using System.Net;
using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using FitMind.Models.Identity;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    IEmailSender emailSender,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Register(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = SafeReturnUrl(returnUrl);
        await LoadExternalProvidersAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterVm model, string? returnUrl = null)
    {
        returnUrl = SafeReturnUrl(returnUrl);
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            await LoadExternalProvidersAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = false
        };

        var createResult = await users.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            await LoadExternalProvidersAsync();
            return View(model);
        }

        var roleResult = await users.AddToRoleAsync(user, "Пользователь");
        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            var cleanupResult = await users.DeleteAsync(user);
            if (!cleanupResult.Succeeded)
                throw new InvalidOperationException("Не удалось отменить частично созданную учётную запись.");
            await LoadExternalProvidersAsync();
            return View(model);
        }

        db.UserProfiles.Add(CreateProfile(user.Id, model.Name));
        await db.SaveChangesAsync();

        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Setup", "Profile");
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = SafeReturnUrl(returnUrl);
        await LoadExternalProvidersAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginVm model, string? returnUrl = null)
    {
        returnUrl = SafeReturnUrl(returnUrl);
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            await LoadExternalProvidersAsync();
            return View(model);
        }

        var result = await signIn.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.IsLockedOut
                ? "Аккаунт временно заблокирован из-за нескольких неудачных попыток входа."
                : "Неверный email или пароль.");
            await LoadExternalProvidersAsync();
            return View(model);
        }

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user == null)
        {
            await signIn.SignOutAsync();
            ModelState.AddModelError("", "Не удалось загрузить аккаунт.");
            await LoadExternalProvidersAsync();
            return View(model);
        }

        return await RedirectAfterLoginAsync(user, returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
    {
        returnUrl = SafeReturnUrl(returnUrl);
        var schemes = await signIn.GetExternalAuthenticationSchemesAsync();
        if (!schemes.Any(x => string.Equals(x.Name, provider, StringComparison.Ordinal)))
        {
            TempData["Error"] = "Вход через Google пока не настроен. Добавьте ClientId и ClientSecret в пользовательские секреты приложения.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var callbackUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl });
        var properties = signIn.ConfigureExternalAuthenticationProperties(provider, callbackUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl = SafeReturnUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            logger.LogWarning("External login did not complete.");
            TempData["Error"] = "Внешний сервис не завершил вход. Попробуйте ещё раз.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var info = await signIn.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["Error"] = "Не удалось проверить данные внешнего входа.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var externalResult = await signIn.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);
        if (externalResult.Succeeded)
        {
            var linkedUser = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            return linkedUser == null
                ? RedirectToAction(nameof(Login))
                : await RedirectAfterLoginAsync(linkedUser, returnUrl);
        }

        if (externalResult.IsLockedOut)
        {
            TempData["Error"] = "Аккаунт временно заблокирован.";
            return RedirectToAction(nameof(Login));
        }

        if (externalResult.RequiresTwoFactor || externalResult.IsNotAllowed)
        {
            TempData["Error"] = "Для этого аккаунта требуется вход с дополнительной проверкой.";
            return RedirectToAction(nameof(Login));
        }

        if (info.LoginProvider != "Google" ||
            !bool.TryParse(info.Principal.FindFirstValue("google:email_verified"), out var emailVerified) || !emailVerified)
        {
            TempData["Error"] = "Google не подтвердил адрес почты. Используйте вход по email и паролю.";
            return RedirectToAction(nameof(Login));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Google не передал email, необходимый для создания аккаунта.";
            return RedirectToAction(nameof(Login));
        }

        var user = await users.FindByEmailAsync(email);
        if (user != null)
        {
            if (!await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user) ||
                await users.GetTwoFactorEnabledAsync(user))
            {
                TempData["Error"] = "Вход в этот аккаунт ограничен. Используйте вход по email и паролю.";
                return RedirectToAction(nameof(Login));
            }
            var linkResult = await users.AddLoginAsync(user, info);
            if (!linkResult.Succeeded)
            {
                TempData["Error"] = "Не удалось привязать Google к существующему аккаунту.";
                return RedirectToAction(nameof(Login));
            }
        }
        else
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var createResult = await users.CreateAsync(user);
            var loginResult = createResult.Succeeded
                ? await users.AddLoginAsync(user, info)
                : IdentityResult.Failed();
            var roleResult = loginResult.Succeeded
                ? await users.AddToRoleAsync(user, "Пользователь")
                : IdentityResult.Failed();

            if (!createResult.Succeeded || !loginResult.Succeeded || !roleResult.Succeeded)
            {
                logger.LogWarning("Unable to create external login account for provider {Provider}.", info.LoginProvider);
                if (createResult.Succeeded)
                {
                    var cleanupResult = await users.DeleteAsync(user);
                    if (!cleanupResult.Succeeded)
                        throw new InvalidOperationException("Не удалось отменить частично созданную внешнюю учётную запись.");
                }
                TempData["Error"] = "Не удалось создать аккаунт через Google.";
                return RedirectToAction(nameof(Login));
            }

            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
            db.UserProfiles.Add(CreateProfile(user.Id, displayName));
            await db.SaveChangesAsync();
        }

        await signIn.SignInAsync(user, isPersistent: false, info.LoginProvider);
        return await RedirectAfterLoginAsync(user, returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user?.Email is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { email = user.Email, token },
                Request.Scheme);

            if (!string.IsNullOrWhiteSpace(callbackUrl))
            {
                var safeUrl = WebUtility.HtmlEncode(callbackUrl);
                await emailSender.SendAsync(
                    user.Email,
                    "Восстановление доступа к FitMind",
                    $"<p>Для смены пароля перейдите по ссылке:</p><p><a href=\"{safeUrl}\">Сбросить пароль</a></p><p>Если вы не запрашивали сброс, проигнорируйте письмо.</p>",
                    HttpContext.RequestAborted);
            }
        }

        ViewBag.Message = "Если аккаунт существует и почтовая служба доступна, инструкция отправлена на указанный email.";
        ModelState.Clear();
        return View(new ForgotPasswordVm());
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Ссылка восстановления неполная или повреждена.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordVm { Email = email, Token = token });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user == null)
        {
            TempData["Ok"] = "Если ссылка была действительной, пароль изменён.";
            return RedirectToAction(nameof(Login));
        }

        var result = await users.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        await users.UpdateSecurityStampAsync(user);
        TempData["Ok"] = "Пароль изменён. Теперь можно войти.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    private async Task<IActionResult> RedirectAfterLoginAsync(ApplicationUser user, string? returnUrl)
    {
        if (await users.IsInRoleAsync(user, "Администратор"))
            return LocalRedirectOrDefault(returnUrl);

        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
        if (profile == null || !profile.OnboardingCompleted)
            return RedirectToOnboarding(profile);

        return LocalRedirectOrDefault(returnUrl);
    }

    private IActionResult LocalRedirectOrDefault(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Schedule");

    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    private IActionResult RedirectToOnboarding(UserProfile? profile)
    {
        var step = profile?.OnboardingStep ?? 1;
        return step switch
        {
            2 => RedirectToAction("Goal", "Profile"),
            3 => RedirectToAction("TrainingPreferences", "Profile"),
            _ => RedirectToAction("Setup", "Profile")
        };
    }

    private async Task LoadExternalProvidersAsync() =>
        ViewBag.ExternalProviders = (await signIn.GetExternalAuthenticationSchemesAsync()).ToArray();

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
    }

    private static UserProfile CreateProfile(string userId, string name) => new()
    {
        UserId = userId,
        Name = name.Trim(),
        CreatedAt = DateTime.UtcNow,
        Gender = "Не указан",
        OnboardingStep = 1,
        OnboardingCompleted = false
    };
}
