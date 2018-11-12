using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Xml.Linq;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class CabinetController : Controller
    {
        private DB db = new DB();

        [AuthorizeClient]
        public PartialViewResult Index()
        {
            return PartialView();
        }

        [HttpGet]
        [AuthorizeClient]
        public PartialViewResult Common()
        {
            var order =
                db.Orders.Where(x => x.UserID == (Guid)Membership.GetUser().ProviderUserKey)
                  .OrderByDescending(x => x.CreateDate);
            var last = order.FirstOrDefault();
            if (last != null)
            {
                ViewBag.OrderNum = last.ID.ToString();
                ViewBag.Header = "Ваш последний заказ №S" + last.ID.ToString("d9");
            }
            return PartialView();
        }

        [HttpGet]
        [AuthorizeClient]
        public PartialViewResult Settings()
        {
            var p = Membership.GetUser().UserEntity().Profile;
            if (p.NotifyMail.IsNullOrEmpty())
                p.NotifyMail = p.MembershipUser.Email;
            if (p.NotifyPhone.IsNullOrEmpty())
                p.NotifyPhone = p.MobilePhone;

            return PartialView(p);
        }

        [HttpPost]
        [AuthorizeClient]
        public PartialViewResult Settings(FormCollection collection)
        {
            var db = new DB();
            var p = db.UserProfiles.FirstOrDefault(x => x.UserID == (Guid)Membership.GetUser().ProviderUserKey);
            if (p == null)
            {
                if (HttpContext.User.Identity.IsAuthenticated)
                {
                    p = new UserProfile() { UserID = (Guid)Membership.GetUser().ProviderUserKey, Name = "" };
                    db.UserProfiles.InsertOnSubmit(p);
                }
                else
                {
                    ModelState.AddModelError("", "Профиль пользователя не найден");
                    return PartialView(p);
                }
            }
            UpdateModel(p);
            db.SubmitChanges();
            ModelState.AddModelError("", "Данные успешно сохранены.");
            return PartialView(p);
        }

        [HttpPost]
        [AuthorizeClient]
        public PartialViewResult Details(int? id, string header, string Message, FormCollection collection)
        {

            if (header.IsFilled())
            {
                ViewBag.Header = header;
            }
            var u = db.Orders.FirstOrDefault(x => x.ID == id);
            if (u == null)
            {
                ViewBag.Message = "Заказ не найден.";
                return Details(id, header);
            }
            if (Message.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо заполнить поле для сообщения.");
                return Details(id, header);
            }
            var now = DateTime.Now;
            now = now.Subtract(new TimeSpan(0, 0, 0, 0, now.Millisecond));
            var comment = new OrderComment() { Author = null, Comment = Message, Date = now, OrderID = u.ID };
            db.OrderComments.InsertOnSubmit(comment);
            db.SubmitChanges();

            return Details(id, header);
        }


        [HttpGet]
        [AuthorizeClient]
        public PartialViewResult Details(int? id, string header)
        {
            var u = db.Orders.FirstOrDefault(x => x.ID == id);
            if (header.IsFilled())
            {
                ViewBag.Header = header;
            }
            else if (u != null)
            {
                ViewBag.Header = "Информация о заказе №S" + u.ID.ToString("d9");
            }

            if (u == null)
            {
                ViewBag.Message = "Заказ не найден.";
                return PartialView(u);
            }
            if (Request.HttpMethod == "GET")
            {
                try
                {
                    XDocument data = XDocument.Load("http://www.sprinter.ru/obmen/?order_id=" + id);
                    //XDocument data = XDocument.Load("http://www.sprinter.ru/obmen/?order_id=1347276765");
                    var status = data.Descendants("status").ToList();
                    if (status.Any())
                    {
                        u.StatusID = OrderStatus.GetStatusIDByRUS(status.First().Value);
                    }
                    var comments = data.Descendants("comment").ToList();
                    if (comments.Any())
                    {
                        foreach (XElement comment in comments)
                        {
                            DateTime date;
                            if (DateTime.TryParseExact(comment.Attribute("date").Value, "yyyy-MM-dd HH:mm:ss",
                                                       CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                            {
                                var dbc = db.OrderComments.FirstOrDefault(x => x.OrderID == u.ID && x.Date == date);
                                if (dbc == null)
                                {
                                    dbc = new OrderComment()
                                        {
                                            Author = comment.Attribute("name").Value,
                                            Comment = comment.Value,
                                            OrderID = u.ID,
                                            Date = date
                                        };
                                    db.OrderComments.InsertOnSubmit(dbc);
                                }
                            }
                        }
                    }
                    db.SubmitChanges();

                }
                catch
                {

                }
            }
            string message = "";
            message = "<h3>Статус заказа:</h3>" + u.OrderStatus.Status + "<br><br>";

            message += "<h3>Заказанные товары:</h3><br><table>";
            message += "<tr><td><b>Название</b></td><td><b>Количество</b></td><td><b>Цена</b></td></tr>";
            message += string.Join("",
                                   u.OrderedBooks.Select(
                                       x =>
                                       "<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>".FormatWith(
                                           x.BookDescriptionCatalog.Header, x.Amount.ToString(),
                                           x.Sum.ForDisplaing())));
            message += "<tr><td colspan=\"3\"><b>Итого к оплате &nbsp;&mdash;&nbsp;" +
                        u.TotalSum + " руб.</b></td></tr> </table><br><br>";
            /*
                        message += "<h3>Информация о покупателе:</h3><br>";
                        message += ShopCart.TranslateToHtml(u.UserData, "UserData");
                        var orgData = u.OrderDetail.OrgData;
                        if (!string.IsNullOrEmpty(orgData))
                        {
                            message += "<h3>Информация о юридическом лице:</h3><br>";
                            message += ShopCart.TranslateToHtml(orgData, "OrgData");
                        }
            */
            if (u.OrderDetail.OrderDeliveryRegion != null)
            {
                message +=
                    "<h3>Информация о доставке:</h3><br><b>{0}, {1}, стоимость - {2} руб.</b><br>".FormatWith(
                        u.OrderDetail.OrderDeliveryRegion.OrderDeliveryProvider.Name,
                        u.OrderDetail.OrderDeliveryRegion.Name, u.OrderDetail.DeliveryCost.ForDisplaing());
            }
            message += ShopCart.TranslateToHtml(u.OrderDetail.Address ?? "", "Address");
            ViewBag.Data = message;
            return PartialView(u);
        }

        [HttpGet]
        [AuthorizeClient]
        public PartialViewResult Orders()
        {
            return PartialView(Membership.GetUser().UserEntity().Orders.OrderByDescending(x => x.CreateDate));
        }

        [HttpGet]
        [AuthorizeClient]
        public PartialViewResult Shopcart()
        {
            return PartialView();
        }

        [HttpGet]
        [AuthorizeClient]
        public PartialViewResult Personal()
        {
            return PartialView(Membership.GetUser().UserEntity().Profile);
        }

        [HttpPost]
        [AuthorizeClient]
        public PartialViewResult Personal(FormCollection collection)
        {
            var profile = db.UserProfiles.FirstOrDefault(x => x.UserID == (Guid)Membership.GetUser().ProviderUserKey);
            if (profile == null)
            {
                profile = new UserProfile() { UserID = (Guid)Membership.GetUser().ProviderUserKey };
                db.UserProfiles.InsertOnSubmit(profile);
            }
            UpdateModel(profile);
            ShopCart.InitCart().InitFieldsIfEmpty();
            db.SubmitChanges();
            ModelState.AddModelError("", "Данные успешно сохранены");
            return PartialView(profile);
        }

    }
}
