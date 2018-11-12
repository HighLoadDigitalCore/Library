using Sprinter.Extensions.Helpers;
using BotDetect.Web.UI.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using System.Web;
using Sprinter.Models;
using Sprinter.Extensions;

namespace Smoking.Controllers
{

    [Authorize]
    public class AccountController : Controller
    {
        public ActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ChangePassword(ChangePasswordModel model)
        {
            if (ModelState.IsValid)
            {
                bool changePasswordSucceeded;
                try
                {

                    changePasswordSucceeded = Membership.GetUser(User.Identity.Name).ChangePassword(model.OldPassword, model.NewPassword);
                }
                catch (Exception)
                {
                    changePasswordSucceeded = false;
                }
                if (changePasswordSucceeded)
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }
                ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
            }
            return View(model);
        }

        public ActionResult ChangePasswordSuccess()
        {
            return View();
        }

        private ActionResult ContextDependentView()
        {
            string actionName = ControllerContext.RouteData.GetRequiredString("action");
            if (Request.QueryString["content"] != null)
            {
                ViewBag.FormAction = "Json" + actionName;
                return PartialView();
            }
            ViewBag.FormAction = actionName;
            return View();
        }

        private static string ErrorCodeToString(MembershipCreateStatus createStatus)
        {
            switch (createStatus)
            {
                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidPassword:
                    return "Пароль некорректный. Укажите другой пароль.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidEmail:
                    return "E-mail указан некорректно. Проверьте e-mail адрес и попробуйте снова.";

                case MembershipCreateStatus.DuplicateUserName:
                    return "Пользователь с таким именем уже существует. Укажите другое имя пользователя.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "Пользователь с таким E-mail уже существует. Укажите другой E-mail.";

                case MembershipCreateStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
            return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
        }

        private IEnumerable<string> GetErrorsFromModelState()
        {
            return (IEnumerable<string>)(from x in ModelState select from error in x.Value.Errors select error.ErrorMessage);
        }

        [AllowAnonymous, HttpPost]
        public JsonResult JsonLogOn(LogOnModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (Membership.ValidateUser(model.UserName, model.Password) || FormsAuthentication.Authenticate(model.UserName, model.Password))
                {
                    FormsAuthentication.SetAuthCookie(model.UserName, model.RememberMe);
                    return Json(new { success = true, redirect = returnUrl });
                }
                ModelState.AddModelError("", "Имя пользователя или пароль указаны неверно.");
            }
            return Json(new { errors = GetErrorsFromModelState() });
        }

        [AllowAnonymous, HttpPost]
        public ActionResult JsonRegister(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                MembershipCreateStatus createStatus;
                Membership.CreateUser(model.UserName, model.Password, model.Email, null, null, true, null, out createStatus);
                if (createStatus == MembershipCreateStatus.Success)
                {

                    FormsAuthentication.SetAuthCookie(model.UserName, false);
                    return Json(new { success = true });
                }
                ModelState.AddModelError("", ErrorCodeToString(createStatus));
            }
            return Json(new { errors = GetErrorsFromModelState() });
        }

        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            return Redirect("/Master");
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult LogOn()
        {
            if (HttpContext.User.Identity.IsAuthenticated && AccessHelper.IsMaster)
            {
                if (!Request["ReturnURL"].IsNullOrEmpty())
                {
                    return new RedirectResult(Request["ReturnURL"]);
                }
                return RedirectToAction("Index", AccessHelper.getStartUserController(HttpContext.User.Identity.Name));
            }
            return ContextDependentView();
        }

        [CaptchaValidation("CaptchaCode", "RegistrationCaptcha", "Вы должны указать символы, изображенные на картинке."), AllowAnonymous, HttpPost]
        public ActionResult LogOn(LogOnModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelErrorCollection captchaErr = ModelState["CaptchaCode"].Errors;
                if (captchaErr.Any())
                {
                    captchaErr.Clear();
                    captchaErr.Add("Вы должны указать символы, изображенные на картинке.");
                }
                return ContextDependentView();
            }
            if (Membership.ValidateUser(model.UserName, model.Password))
            {
                FormsAuthentication.SetAuthCookie(model.UserName, model.RememberMe);
                if (!Request["ReturnURL"].IsNullOrEmpty())
                {
                    return new RedirectResult(Request["ReturnURL"]);
                }
                return RedirectToAction("Index", AccessHelper.getStartUserController(model.UserName));
            }
            return ContextDependentView();
        }

        [AllowAnonymous]
        public ActionResult Register()
        {
            return ContextDependentView();
        }

        [AllowAnonymous, HttpPost]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                MembershipCreateStatus createStatus;
                Membership.CreateUser(model.UserName, model.Password, model.Email, null, null, true, null, out createStatus);
                if (createStatus == MembershipCreateStatus.Success)
                {

                    FormsAuthentication.SetAuthCookie(model.UserName, false);
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError("", ErrorCodeToString(createStatus));
            }
            return View(model);
        }
    }
}

