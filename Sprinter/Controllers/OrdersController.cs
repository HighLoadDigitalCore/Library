using System;
using System.Collections.Generic;
using System.Data.Linq.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class OrdersController : Controller
    {
        DB db = new DB();
        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Index(string query, int? page)
        {
            var orders = db.Users.AsQueryable();

            if (!query.IsNullOrEmpty())
            {
                query = "%{0}%".FormatWith(query);
                orders =
                    orders.Where(
                        x =>
                        SqlMethods.Like(x.UserName.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.MembershipData.Email.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.UserProfile.Name.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.UserProfile.Patrinomic.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.UserProfile.Surname.ToLower(), query.ToLower()));
            }

            if (((page ?? 0 + 1) * 50) > orders.Count())
                page = 0;

            return
                View(new PagedData<Order>(orders.SelectMany(x => x.Orders).OrderByDescending(x => x.CreateDate),
                                          page ?? 0, 50, "Master",
                                          new RouteValueDictionary(
                                              new {query = (query ?? "").Replace("%", ""), page = page,})));
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Index( string query, int? page, FormCollection collection)
        {
            return RedirectToAction("Index", new {query = query, page = page});
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Delete(int? order, string query, int? page)
        {
            var u = db.Orders.FirstOrDefault(x => x.ID == order);
            if (u == null) return RedirectToAction("Index", new { query = query, page = page});
            return View(u);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Delete(int? order, string query, int? page, FormCollection collection)
        {
            var u = db.Orders.FirstOrDefault(x => x.ID == order);
            if (u == null) return RedirectToAction("Index", new { query = query, page = page});
            db.Orders.DeleteOnSubmit(u);
            db.SubmitChanges();
            return RedirectToAction("Index", new { query = query, page = page});
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Edit(int? order, string query, int? page)
        {
            var u = db.Orders.FirstOrDefault(x => x.ID == order);
            if (u == null) return RedirectToAction("Index", new { query = query, page = page });

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
            message += "<h3>Информация о покупателе:</h3><br>";
            message += ShopCart.TranslateToHtml(u.UserData, "UserData");
            var orgData = u.OrderDetail.OrgData;
            if (!string.IsNullOrEmpty(orgData))
            {
                message += "<h3>Информация о юридическом лице:</h3><br>";
                message += ShopCart.TranslateToHtml(orgData, "OrgData");
            }
            if (u.OrderDetail.OrderDeliveryRegion != null)
            {
                message +=
                    "<h3>Информация о доставке:</h3><br><b>{0}, {1}, стоимость - {2} руб.</b><br>".FormatWith(
                        u.OrderDetail.OrderDeliveryRegion.OrderDeliveryProvider.Name,
                        u.OrderDetail.OrderDeliveryRegion.Name,  u.OrderDetail.DeliveryCost.ForDisplaing());
            }
            message += ShopCart.TranslateToHtml(u.OrderDetail.Address??"", "Address");
            ViewBag.Data = message;
            return View();
        }

    }
}
