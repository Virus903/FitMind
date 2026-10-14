using System.ComponentModel.DataAnnotations;

namespace FitMind.ViewModels;

public class RegisterVm
{
    [Required(ErrorMessage = "Введите имя.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Введите email."), EmailAddress(ErrorMessage = "Некорректный email.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Введите пароль."), MinLength(8, ErrorMessage = "Минимум 8 символов."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Повторите пароль."), Compare(nameof(Password), ErrorMessage = "Пароли не совпадают."), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public class LoginVm
{
    [Required(ErrorMessage = "Введите email."), EmailAddress(ErrorMessage = "Некорректный email.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Введите пароль."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}

public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Введите email."), EmailAddress(ErrorMessage = "Некорректный email.")] public string Email { get; set; } = "";
}

public class ResetPasswordVm
{
    [Required(ErrorMessage = "В ссылке отсутствует email."), EmailAddress(ErrorMessage = "Некорректный email в ссылке.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Ссылка восстановления неполная или повреждена.")] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Введите новый пароль."), MinLength(8, ErrorMessage = "Минимум 8 символов."), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required(ErrorMessage = "Повторите пароль."), Compare(nameof(Password), ErrorMessage = "Пароли не совпадают."), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}
