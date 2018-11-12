using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sprinter.Models
{
    public class RegisterModel
    {
        [Display(Name = "Подтвердите пароль"), DataType(DataType.Password), Compare("Password", ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; }

        [DataType(DataType.EmailAddress), Required, Display(Name = "Email")]
        public string Email { get; set; }

        [Required, Display(Name = "Пароль"), DataType(DataType.Password), StringLength(100, ErrorMessage = "{0} должен содержать минимум {2} символов.", MinimumLength = 6)]
        public string Password { get; set; }

        [Display(Name = "Логин"), Required]
        public string UserName { get; set; }
    }
    public class LogOnModel
    {
        [Display(Name = "Введите символы, указанные на картинке"), Required(ErrorMessage = "*")]
        public string CaptchaCode { get; set; }

        [Required(ErrorMessage = "Необходимо заполнить поле \"{0}\""), DataType(DataType.Password), Display(Name = "Пароль")]
        public string Password { get; set; }

        [Display(Name = "Запомнить меня?")]
        public bool RememberMe { get; set; }

        [Required(ErrorMessage = "Необходимо заполнить поле \"{0}\""), Display(Name = "Логин")]
        public string UserName { get; set; }
    }

    public class ChangePasswordModel
    {
        [Display(Name = "Подтвердите пароль"), Compare("NewPassword", ErrorMessage = "Пароли не совпадают."), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Новый пароль"), DataType(DataType.Password), StringLength(100, ErrorMessage = "{0} должен содержать минимум {2} символов.", MinimumLength = 6), Required]
        public string NewPassword { get; set; }

        [DataType(DataType.Password), Required, Display(Name = "Текущий пароль")]
        public string OldPassword { get; set; }
    }
}