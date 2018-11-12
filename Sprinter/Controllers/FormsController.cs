using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;
using Sprinter.Extensions;
namespace Sprinter.Controllers
{
    public class FormsController : Controller
    {
        private CommonPageInfo info = AccessHelper.CurrentPageInfo;

        public PartialViewResult FeedBackPopup()
        {
            return PartialView();
        }

        [HttpGet]
        public PartialViewResult BackCall()
        {
            return PartialView(new BackCallForm());
        }

        [HttpGet]
        public PartialViewResult FeedBack()
        {
            return PartialView(new FeedBackForm());
        }

        [HttpGet]
        public PartialViewResult RestorePass()
        {
            return PartialView(new RestorePassForm());
        }

        [HttpGet]
        public PartialViewResult Auth()
        {
            return PartialView(new AuthForm());
        }

        [HttpGet]
        public PartialViewResult Register()
        {
            return PartialView(new AuthForm());
        }

        [HttpGet]
        public PartialViewResult Comment()
        {
            return PartialView(new CommentForm { TargetBook = AccessHelper.CurrentPageInfo.CurrentBook.BookDescriptionCatalog });
        }

        [HttpPost]
        public PartialViewResult Comment(CommentForm form)
        {
            form.TargetBook = AccessHelper.CurrentPageInfo.CurrentBook.BookDescriptionCatalog;
            string sError = "";
            if (form.Name.IsNullOrEmpty() && !HttpContext.User.Identity.IsAuthenticated)
                sError += "Необходимо указать имя<br/>";
            if (form.Mail.IsFilled() && !form.Mail.IsMailAdress())
                sError += "Необходимо указать корректный Email<br/>";
            if (form.Comment.IsNullOrEmpty())
                sError += "Необходимо заполнить поле для отзыва<br/>";

            if (sError.IsFilled())
            {
                ModelState.AddModelError("", sError);
                return PartialView(form);
            }
            var authUser = Membership.GetUser();
            if (!HttpContext.User.Identity.IsAuthenticated)
            {
                authUser = null;
                var searched = Membership.GetUserNameByEmail(form.Mail);
                if (searched.IsFilled())
                    authUser = Membership.GetUser(searched);
            }
            var comment = new BookComment
                {
                    Approved = false,
                    DescriptionID = form.TargetBook.ID,
                    Date = DateTime.Now,
                    Comment = form.Comment,
                    UserID =
                        authUser != null
                            ? (Guid?)authUser.ProviderUserKey
                            : null,
                    UserName =
                        HttpContext.User.Identity.IsAuthenticated ? authUser.UserEntity().Profile.FullName : form.Name,
                    UserMail = HttpContext.User.Identity.IsAuthenticated ? authUser.Email : form.Mail

                };
            var db = new DB();
            db.BookComments.InsertOnSubmit(comment);
            db.SubmitChanges();

            form.IsSent = true;
            form.ResultMessage = "Спасибо!<br>Ваш отзыв успешно сохранен.<br>Он будет опубликован после проверки модератором.";

            return PartialView(form);

        }

        [HttpPost]
        public ActionResult Register(AuthForm form)
        {
            if (!ModelState.IsValid || !form.Email.IsMailAdress())
            {
                ModelState.AddModelError("", "Необходимо указать корректный Email.");
                return PartialView(form);
            }
            var exist = ShopCart.FindAllUsersByEmail(form.Email).Count > 0;
            if (exist)
            {
                ModelState.AddModelError("", "Пользователь с такие Email уже зарегистрирован в системе.");
                return PartialView(form);

            }
            string lr =
                MailingList.Get("RegisterLetter")
                           .To(form.Email)
                           .WithReplacement(new MailReplacement("{PASSWORD}", form.Password))
                           .Send();
            if (!lr.IsNullOrEmpty())
            {
                ModelState.AddModelError("", lr);
                return PartialView(form);
            }

            var msg = ShopCart.RegisterUser(form.Email, form.Password);
            if (msg.IsFilled())
            {
                ModelState.AddModelError("", msg);
                return PartialView(form);
            }
            string url = string.Format("{0}?{1}", info.CurrentPage.FullUrl, Request.QueryString);
            if (url.EndsWith("?"))
                url = url.Substring(0, url.Length - 1);

            form.IsSent = true;
            form.ResultMessage = url;
            return PartialView(form);

        }

        [HttpPost]
        public ActionResult Auth(AuthForm form)
        {
            if (!ModelState.IsValid || !form.Email.IsMailAdress())
            {
                ModelState.AddModelError("", "Необходимо указать корректный Email.");
                return PartialView(form);
            }
            var exist = ShopCart.FindAllUsersByEmail(form.Email).Count > 0;
            if (!exist)
            {
                ModelState.AddModelError("", "Указан неверный пароль или Email.");
                return PartialView(form);

            }
            else
            {
                var msg = ShopCart.AuthorizeUser(form.Email, form.Password);
                if (msg.IsFilled())
                {
                    ModelState.AddModelError("", msg);
                    return PartialView(form);
                }
            }
            string url = form.RedirectPage == "cabinet"
                             ? string.Format("/{0}", CMSPage.FullPageTable.First(x => x.Type == 5).FullUrl)
                             : string.Format("{0}?{1}", info.CurrentPage.FullUrl, Request.QueryString);
            if (url.EndsWith("?"))
                url = url.Substring(0, url.Length - 1);
            form.IsSent = true;
            form.ResultMessage = url;
            return PartialView(form);

        }

        [HttpPost]
        public PartialViewResult RestorePass(RestorePassForm form)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Необходимо указать Email.");
                return PartialView(form);
            }
            var user = Membership.FindUsersByEmail(form.Email);
            if (user.Count == 0)
            {
                ModelState.AddModelError("", "Пользователь с таким Email не найден в базе данных.");
                return PartialView(form);

            }

            string result =
                MailingList.Get("RestorePassLetter")
                           .To(form.Email)
                           .WithReplacement(new MailReplacement("{PASSWORD}",
                                                                user.Cast<MembershipUser>().First().GetPassword()))
                           .Send();

            if (result.IsNullOrEmpty())
            {
                form.IsSent = true;
                form.ResultMessage =
                    "Ваш пароль успешно отправлен на указанный почтовый ящик.";
            }
            else
            {
                ModelState.AddModelError("", result);
            }
            return PartialView(form);
        }


        [HttpPost]
        [AllowAnonymous]
        public PartialViewResult BackCall(BackCallForm form)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Необходимо заполнить все поля формы.");
                return PartialView(form);
            }

            var result = MailingList.Get("BackCallLetter")
                          .WithReplacement(new MailReplacement("{FORMCONTENT}", form.LetterBody))
                          .Send();
            if (result.IsNullOrEmpty())
            {
                form.IsSent = true;
                form.ResultMessage =
                    "Ваша заявка на обратный звонок успешно принята. В течение нескольких минут наш оператор свяжется с вами.";
            }
            else
            {
                ModelState.AddModelError("", result);
            }
            return PartialView(form);
        }

        [HttpPost]
        [AllowAnonymous]
        public PartialViewResult FeedBack(FeedBackForm form)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Необходимо заполнить все поля формы.");
                return PartialView(form);
            }

            //FeedBackLetter

            string result = MailingList.Get("FeedBackLetter")
                                       .WithReplacement(new MailReplacement("{FORMCONTENT}", form.LetterBody))
                                       .Send();
            if (result.IsNullOrEmpty())
            {
                form.IsSent = true;
                form.ResultMessage =
                    "Ваша сообщение успешно отправлено. В ближайшее время наш менеджер ответит вам.";
            }
            else
            {
                ModelState.AddModelError("", result);
            }
            return PartialView(form);
        }

    }
}
