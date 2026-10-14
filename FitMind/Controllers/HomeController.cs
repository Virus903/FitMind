using Microsoft.AspNetCore.Mvc;

namespace FitMind.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    // Старые маршруты предыдущей версии перенаправлены на новый 3-шаговый онбординг.
    public IActionResult Onboarding() => RedirectToAction("Setup", "Profile");
    public IActionResult SelectGoal() => RedirectToAction("Goal", "Profile");

    [HttpPost]
    public IActionResult SelectGoal(int goalId) => RedirectToAction("Goal", "Profile");

    public IActionResult Error() => View();
}
