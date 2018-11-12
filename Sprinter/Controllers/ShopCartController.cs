using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Sprinter.Extensions.Helpers;
using Sprinter.Extensions.PaymentServices;
using Sprinter.Models;
using Sprinter.Extensions;
namespace Sprinter.Controllers
{
    public class ShopCartController : Controller
    {
        public PartialViewResult Index(int? step)
        {
            if (step.HasValue)
                ViewData.Add("Step", step.Value);
            return PartialView();
        }

        public PartialViewResult CartBlock()
        {
            ShopCart cart = ShopCart.InitCart();
            return PartialView(cart);
        }

        [HttpPost]
        public PartialViewResult toCart(int id, int? count, bool spec = false)
        {
            var cart = ShopCart.InitCart();
            cart.AddItem(id, count ?? 1, spec);
            return PartialView("CartBlock", ShopCart.InitCart());
        }

        [HttpPost]
        public ContentResult toCartImmediate(int id)
        {
            DB db = new DB();
            var cart = ShopCart.InitCart();
            var forDelay = cart.ActiveBooks.Where(x => x.BookSaleCatalog.ID != id);
            foreach (var dbItem in forDelay.Select(item => db.ShopCartItems.First(x => x.ID == item.ID)))
            {
                dbItem.IsDelayed = true;
            }
            var buyBooks = cart.ShopCartItems.Where(x => x.BookSaleCatalog.ID == id);
            foreach (var dbItem in buyBooks.Select(item => db.ShopCartItems.First(x => x.ID == item.ID)))
            {
                dbItem.Count = 1;
                dbItem.IsDelayed = false;
            }
            db.SubmitChanges();
            cart.Reset();
            if (cart.ActiveBooks.All(x => x.BookSaleCatalog.ID != id))
            {
                cart.AddItem(id, 1);
            }
            return new ContentResult() { Content = string.Format("/order?step={0}&fastorder=true", cart.MaxAvailableStep) };

        }

        public ActionResult BookList(int? type)
        {
            return PartialView();
        }

        public ActionResult Step0()
        {
            return PartialView(ShopCart.InitCart());
        }

        public ActionResult Step1()
        {
            CheckAndRedirect();
            return PartialView(ShopCart.InitCart());
        }

        public ActionResult Step2()
        {
            CheckAndRedirect();
            DB db = new DB();
            var list = db.OrderDeliveryGroups.Where(x => x.OrderDeliveryProviders.Any()).OrderBy(x => x.OrderNum);
            return PartialView(list);
        }


        public ActionResult Step3()
        {
            CheckAndRedirect();
            var cart = ShopCart.InitCart();
            cart.InitFieldsIfEmpty();
            return PartialView(cart);

        }


        public ActionResult Step4()
        {
            CheckAndRedirect();
            return PartialView(ShopCart.InitCart());
        }

        [AllowAnonymous]
        public ActionResult Step5()
        {
            //Logger.WriteToLog("Заходим в шаг 5!!!");
            //Logger.WriteToLog(Request.RawUrl);
            var cart = ShopCart.InitCart();
            Order order = null;
            //если редирект на нотификацию от какой-то платежки
            var payment = PaymentSystems.FirstOrDefault(paymentSystem => paymentSystem.IsMyNotification);
            if (payment != null)
            {
                //Logger.WriteToLog("Нашли оповещателя!!!");
                order = payment.NotificatedOrder;
                if (order != null)
                {
                    payment.Init(order);
                    //Logger.WriteToLog(Enum.GetName(typeof(Mode), payment.CurrentMode));
                    ViewBag.PaymentMessage = payment.AutoExecuteOnRequest();
                }
                if (payment.IsCompleted)
                {
                    if (cart != null)
                        cart.ClearActive();
                    Session["LastOrder"] = null;
                }
                return PartialView();
            }

            //если мы перешли со страницы оформления через браузёр
            if (CheckAndRedirect())
            {
                cart.SaveFieldsInProfile(true);
                IPaymentService ps = null;
                if (cart.SelectedPayment.ID > 0)
                {
                    ps = PaymentSystems.FirstOrDefault(x => x.Name == cart.SelectedPayment.Code);
                    if (ps == null || ps.CurrentMode == Mode.Default)
                    {
                        if (Session["LastOrder"] == null)
                        {
                            order = cart.CreateOrder();

                            Session["LastOrder"] = order;
                        }
                        else
                        {
                            order = (Order)Session["LastOrder"];
                        }
                    }
                    if (ps == null)
                        ViewBag.PaymentMessage = "Payment system is not implemented. Yet.";
                    else
                    {
                        ps.Init(order);
                        ViewBag.PaymentMessage = ps.AutoExecuteOnRequest();
                    }
                }
                if (ps == null || ps.CurrentMode == Mode.Default) //Типо только перешли и не было редиректов
                {

                    var replacements = new List<MailReplacement>
                        {
                            new MailReplacement("{BOOKSLIST}", cart.HTMLForLetterBookList),
                            new MailReplacement("{USERDATA}", cart.HTMLForUserData),
                            new MailReplacement("{DELIVERY}", cart.HtmlForDelivery)
                        };

                    string error = MailingList.Get("OrderNotificationAdmin").WithReplacements(replacements).Send();
                    if (error.IsFilled())
                    {
                        ModelState.AddModelError("", error);
                    }
                    else
                    {
                        MailingList.Get("OrderNotificationClient")
                                   .To(cart.UserMail)
                                   .WithReplacements(replacements)
                                   .Send();
                    }

                }
                if (ps == null || ps.CurrentMode == Mode.Fail)
                {
                    //Session["LastOrder"] = null;
                }
                if (ps == null || /*ps.CurrentMode == Mode.Success || */ps.IsCompleted)
                {
                    cart.ClearActive();
                    Session["LastOrder"] = null;
                }


            }
            return PartialView();
        }


        private List<IPaymentService> _paymentSystems;
        protected List<IPaymentService> PaymentSystems
        {
            get
            {
                return _paymentSystems ?? (_paymentSystems = new List<IPaymentService>
                    {
                        new RobokassaService(),
                        new CashService(),
                        new YandexService(),
                        new OnPostService(),
                        new BankService()
                    });
            }
        }

        protected bool CheckAndRedirect()
        {
            var cart = ShopCart.InitCart();
            if (!cart.ActiveBooks.Any())
            {
                Response.Redirect("/order");
                return false;
            }
            if (OrderSteps.CurrentStep > 1 && !HttpContext.User.Identity.IsAuthenticated)
            {
                //сначала пытаемся авторизоваться по сохраненным данным
                var name = cart.GetField<string>("UserMail");
                if (!name.IsNullOrEmpty())
                {
                    var cr = check(name, cart.GetField<string>("UserPass"), cart.GetField<int>("AuthType"));
                    if (!cr.Content.IsNullOrEmpty())
                    {
                        Response.Redirect("/order?step=1");
                        return false;
                    }
                }
                else
                {
                    Response.Redirect("/order?step=0");
                    return false;
                }
            }
            return true;
        }

        [HttpPost]
        public ContentResult check(string name, string pass, int type)
        {
            var result = new ContentResult();
            if (name.IsNullOrEmpty() || pass.IsNullOrEmpty())
                result.Content = "Необходимо указать данные для авторизации.";
            {
                var exist = ShopCart.FindAllUsersByEmail(name).Count > 0;
                if (type == 1)
                {
                    if (exist)
                    {
                        result.Content = "Пользователь с таким Email уже зарегистрирован. Используйте другой Email.";
                    }
                    else
                    {
                        if (!name.IsMailAdress())
                        {
                            result.Content = "Необходимо указать корректный E-mail адрес";

                        }
                        else
                        {
                            pass = new Random(DateTime.Now.Millisecond).GeneratePassword();
                            string lr = MailingList.Get("RegisterLetter")
                                       .To(name)
                                       .WithReplacement(new MailReplacement("{PASSWORD}", pass))
                                       .Send();
                            result.Content = !lr.IsNullOrEmpty() ? lr : ShopCart.RegisterUser(name, pass);
                        }
                    }
                }
                else if (type == 2)
                {
                    result.Content = !exist ? "Указан неверный пароль или Email." : ShopCart.AuthorizeUser(name, pass);
                }
                else result.Content = "Выберите тип авторизации";
            }
            return result;
        }

        [HttpPost]
        public ContentResult autoSave(IEnumerable<JFieldEntry> list)
        {
            var cart = ShopCart.InitCart();
            foreach (var entry in list)
            {
                cart.SetField(entry.name, entry.value);
            }
            cart.Reset();
            return new ContentResult();
        }


        [HttpPost]
        public PartialViewResult editItem(int id, string act, int count, bool? isCart)
        {
            DB db = new DB();
            if (isCart.HasValue)
            {
                ViewBag.IsCart = isCart.Value;
            }
            if (count == -1)
            {
                return PartialView("Index");
            }
            var item = db.ShopCartItems.FirstOrDefault(x => x.ID == id);
            if (item != null)
            {

                if (act == "delete")
                {
                    db.ShopCartItems.DeleteOnSubmit(item);
                }
                if (act == "delay")
                {
                    item.IsDelayed = !item.IsDelayed;
                }
                if (act == "change")
                {
                    if (count > 0)
                        item.Count = count;
                }
                if (!item.IsSpec)
                {
                    var inactiveSpecs =
                        item.ShopCart.ActiveBooks.Where(
                            x =>
                            x.IsSpec && x.BookSaleCatalog.BookSpecOffers.Any() &&
                            x.BookSaleCatalog.BookSpecOffers.First().MinPrice > item.ShopCart.TotalSumWithoutSpecs);
                    foreach (ShopCartItem inactiveSpec in inactiveSpecs)
                    {
                        db.ShopCartItems.DeleteOnSubmit(db.ShopCartItems.First(x => x.ID == inactiveSpec.ID));
                    }
                }

                db.SubmitChanges();
            }
            return PartialView("Index");
        }

    }
}
