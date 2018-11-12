using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;
using Sprinter.Models.ViewModels;

namespace Sprinter.Controllers
{
    public class ExportController : Controller
    {

        private DB db = new DB();

        public FileContentResult OrderPayment(int orderID)
        {
            var order = db.Orders.FirstOrDefault(x => x.ID == orderID);
            if (order == null)
            {
                Response.StatusCode = 404;
                return null;
            }
            var content = order.CreatePdfDoc(null);
            var file = new FileContentResult(content, MIMETypeWrapper.GetMIME("pdf"));
            HttpContext.Response.AddHeader("Content-Disposition",
                                           "attachment;filename*=UTF-8''{0}".FormatWith(
                                               HttpUtility.UrlPathEncode(string.Format("Квитанция на оплату заказа №{0}.pdf", order.ID.ToString("d10")))));

            HttpContext.Response.AddHeader("Content-Length", file.FileContents.Length.ToString());
            return file;

        }

        public ContentResult Orders(DateTime? from_date, int? from_id)
        {
            /* Dictionary<string, int> Payments = new Dictionary<string, int>();
             Payments.Add("", 0);
             Payments.Add("Наличными", 1);
             Payments.Add("Оплата через Банк", 2);
             Payments.Add("Наложенным платежом", 3);
             Payments.Add("Оплата банковской картой", 4);
             Payments.Add("Оплата по безналичному расчету", 5);
             Payments.Add("WebMoney", 6);
             Payments.Add("Оплата через сервис Robokassa", 7);
             Payments.Add("Yandex-деньги", 8);*/

            var payments = db.OrderPaymentProviders.Select(x => new { Key = x.SprinterUID, Value = x.Name }).ToList();

            Dictionary<string, int> Deliveries = new Dictionary<string, int>();
            Deliveries.Add("", 0);
            Deliveries.Add("Самовывоз", 1);
            Deliveries.Add("Курьером", 2);
            Deliveries.Add("Почтой России", 3);
            Deliveries.Add("Почтой России в дальнее зарубежье", 4);
            Deliveries.Add("Пони-Экспресс", 5);

            if (!from_date.HasValue && !from_id.HasValue) from_date = DateTime.Now.Date.AddDays(-1);
            const int maxDayLimit = -90;
            IQueryable<Order> records = null;
            if (from_date.HasValue && from_date < DateTime.Now.AddDays(maxDayLimit).Date) from_date = DateTime.Now.AddDays(maxDayLimit).Date;
            if (from_date.HasValue)
                records =
                    db.Orders.Where(x => x.CreateDate > from_date || x.OrderComments.Any(z => z.Date > from_date))
                      .OrderByDescending(x => x.CreateDate);
            if (from_id.HasValue)
                records = db.Orders.Where(x => x.ID > from_id).OrderByDescending(x => x.CreateDate);


            var list = records.ToList();
            if (!list.Any()) list = new List<Order>();

            XDocument doc = new XDocument();
            var orders = new XElement("Orders");
            doc.Add(orders);

            foreach (Order order in list)
            {
                var xo = new XElement("Order");
                xo.Add(new XAttribute("id", order.ID));
                xo.Add(new XAttribute("date", order.CreateDate.ToString("yyyy-MM-ddTHH:mm:ss")));
                orders.Add(xo);

                var books = new XElement("Books");
                xo.Add(books);
                books.Add(order.OrderedBooks.Where(x => x.Partner != null).Select(x => new XElement("Book", new[]
                        {
                            new XElement("Supplier", x.Partner == null ? -1 : x.Partner.ID),
                            new XElement("SupplierName", x.Partner == null ? "" : x.Partner.Name),
                            new XElement("SaledPrice", x.SalePrice),
                            new XElement("Amount", x.Amount),
                            new XElement("BookId", x.BookDescriptionCatalog.SprinterCode),
                            new XElement("BookSupplierId",
                                         x.BookDescriptionCatalog.BookSaleCatalogs.First(z => z.PartnerID == x.PartnerID)
                                             .PartnerUID)

                        })
                              ));
                xo.Add(XDocument.Parse(order.UserData).Root);
                var orgData = order.OrderDetail.OrgData;
                if (orgData.IsFilled())
                    xo.Add(XDocument.Parse(orgData).Root);


                var adress = order.OrderDetail.Address;
                xo.Add(new XElement("Delivery", Deliveries.First(x => x.Value == order.OrderDetail.DeliveryType).Key));
                xo.Add(new XElement("DeliveryCost", order.OrderDetail.DeliveryCost));
                if (order.OrderDetail.OrderDeliveryRegion != null)
                    xo.Add(new XElement("DeliveryRegion", order.OrderDetail.OrderDeliveryRegion.Name));
                xo.Add(new XElement("PaymentType", order.OrderDetail.PaymentType));
                if (payments.Any(x => x.Key == order.OrderDetail.PaymentType))
                {
                    xo.Add(new XElement("Payment", payments.First(x => x.Key == order.OrderDetail.PaymentType).Value));
                }
                var statusNode = new XElement("OrderStatus", order.OrderStatus.Status);
                statusNode.Add(new XAttribute("name", order.OrderStatus.EngName));
                xo.Add(statusNode);

                var notifyNode = new XElement("NotifyMail", order.User.Profile.NotifyMail ?? order.User.MembershipData.Email);
                notifyNode.Add(new XAttribute("enabled", order.User.Profile.NeedMailNotify ?? false));
                xo.Add(notifyNode);

                var notifySmsNode = new XElement("NotifyPhone", order.User.Profile.NotifyPhone ?? order.User.Profile.MobilePhone);
                notifySmsNode.Add(new XAttribute("enabled", order.User.Profile.NeedPhoneNotify ?? false));
                xo.Add(notifySmsNode);

                if (order.OrderComments.Any())
                {
                    var comments = new XElement("Comments",
                                                order.OrderComments.ToList()
                                                     .Select(
                                                         x =>
                                                             {
                                                                 var comment = new XElement("Comment", x.Comment);
                                                                 comment.Add(new XAttribute("author",
                                                                                            x.Author.IsNullOrEmpty()
                                                                                                ? x.Order.User
                                                                                                   .UserProfile
                                                                                                   .FullName
                                                                                                : x.Author),
                                                                             new XAttribute("date",
                                                                                            x.Date.ToString(
                                                                                                "yyyy-MM-dd HH:mm:ss")),
                                                                             new XAttribute("id", x.ID.ToString()));
                                                                 return comment;
                                                             }
                                                    ));
                    xo.Add(comments);
                }


                if (adress.IsFilled())
                    xo.Add(XDocument.Parse(adress).Root);
            }
            ContentResult content = new ContentResult();
            content.Content = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n";
            content.Content += doc.ToString();
            content.ContentType = "text/xml";
            return content;
        }

        [HttpGet]
        public ActionResult Yandex()
        {
            return View(new CatalogExportFilterModel());
        }

        [HttpPost]
        public ActionResult Yandex(CatalogExportFilterModel model)
        {
            ParseringInfo.Reset("YandexExport");
            var workingThread = new Thread(ThreadFuncExportCatalogToYandex);
            workingThread.Start(new ThreadExportInfo() { Context = System.Web.HttpContext.Current, ExporterName = "YandexExport", ExportFilter = model, ProgressFunc = OnOrderFormedYandex });

            return View(model);
        }


        private void ThreadFuncExportCatalogToYandex(object context)
        {
            DB dbx = new DB();
            var thi = (ThreadExportInfo)context;
            System.Web.HttpContext.Current = thi.Context;
            ParseringInfo info = ParseringInfo.Create(thi.ExporterName);
            info.Created = info.Updated = info.Deleted = info.Dirs = info.Prepared = 0;
            info.EndDate = null;
            info.StartDate = DateTime.Now;
            info.AddMessage(DateTime.Now.ToString("dd.MM.yyyy HH:mm") + " - Начало формирования файла.");
            var db = new DB();
            var pageIDs = thi.ExportFilter.PageListPlain.Split<int>();
            var pagesList = CMSPage.FullPageTable.Where(x => pageIDs.Contains(x.ID)).ToList();

            //Ищем все подразделы
            if (pagesList.Any())
            {
                int minLevel = pagesList.Min(x => x.TreeLevel);
                int maxLevel = CMSPage.FullPageTable.Max(x => x.TreeLevel);
                for (int i = minLevel; i < maxLevel; i++)
                {
                    var thisLevel = pagesList.Where(x => x.TreeLevel == i).Select(x => x.ID).ToList();
                    var nextLevel = CMSPage.FullPageTable.Where(x => thisLevel.Contains(x.ParentID ?? 0)).ToList();
                    pagesList.AddRange(nextLevel);
                }
            }
            var pagesListIdsPlain = string.Join(";", pagesList.Select(x => x.ID));

            var pages = db.getIntListByJoinedString(pagesListIdsPlain, ";").Join(db.CMSPages.AsQueryable(), x => x.ID,
                                                                                 y => y.ID, (x, y) => y);
            var partnersListIds = thi.ExportFilter.PartnerListPlain.Split<int>();

            
            var filtered =
                db.BookSaleCatalogs.Where(
                    x =>
                    (!thi.ExportFilter.AvailableOnly || x.IsAvailable) &&
                    (!thi.ExportFilter.MinPrice.HasValue ||
                     (x.Partner == null
                          ? 0
                          : ((x.PriceOverride.HasValue && x.PriceOverride > 0)
                                 ? x.PriceOverride.Value
                                 : x.PartnerPrice * (100 +
                                                   (x.Margin > 0
                                                        ? x.Margin
                                                        : (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                                           x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.
                                                               Any(
                                                                   z => z.PartnerID == x.PartnerID) &&
                                                           x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.
                                                               First(
                                                                   z => z.PartnerID == x.PartnerID).Margin.HasValue
                                                               ? (x.BookDescriptionCatalog.BookPublisher.
                                                                     BookPublisherMargins
                                                                     .First(
                                                                         z => z.PartnerID == x.PartnerID).Margin.Value)
                                                               : (x.Partner.Margin)))

                                                   - (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                                      x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                                          z => z.PartnerID == x.PartnerID) &&
                                                      x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                                          z => z.PartnerID == x.PartnerID).Discount.HasValue
                                                          ? x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.
                                                                First
                                                                (
                                                                    z => z.PartnerID == x.PartnerID).Discount.Value
                                                          : x.Partner.Discount)) / 100)) >= thi.ExportFilter.MinPrice) &&
                    (!thi.ExportFilter.MaxPrice.HasValue || (x.Partner == null
                                                                 ? 0
                                                                 : ((x.PriceOverride.HasValue && x.PriceOverride > 0)
                                                                        ? x.PriceOverride.Value
                                                                        : x.PartnerPrice * (100 +
                                                                                          (x.Margin > 0
                                                                                               ? x.Margin
                                                                                               : (x.
                                                                                                      BookDescriptionCatalog
                                                                                                      .PublisherID.
                                                                                                      HasValue &&
                                                                                                  x.
                                                                                                      BookDescriptionCatalog
                                                                                                      .BookPublisher.
                                                                                                      BookPublisherMargins
                                                                                                      .Any(
                                                                                                          z =>
                                                                                                          z.PartnerID ==
                                                                                                          x.PartnerID) &&
                                                                                                  x.
                                                                                                      BookDescriptionCatalog
                                                                                                      .BookPublisher.
                                                                                                      BookPublisherMargins
                                                                                                      .First(
                                                                                                          z =>
                                                                                                          z.PartnerID ==
                                                                                                          x.PartnerID).
                                                                                                      Margin.HasValue
                                                                                                      ? (x.
                                                                                                            BookDescriptionCatalog
                                                                                                            .
                                                                                                            BookPublisher
                                                                                                            .
                                                                                                            BookPublisherMargins
                                                                                                            .First(
                                                                                                                z =>
                                                                                                                z.
                                                                                                                    PartnerID ==
                                                                                                                x.
                                                                                                                    PartnerID)
                                                                                                            .Margin.
                                                                                                            Value)
                                                                                                      : (x.Partner.
                                                                                                            Margin)))

                                                                                          -
                                                                                          (x.BookDescriptionCatalog.
                                                                                               PublisherID.HasValue &&
                                                                                           x.BookDescriptionCatalog.
                                                                                               BookPublisher.
                                                                                               BookPublisherMargins.Any(
                                                                                                   z =>
                                                                                                   z.PartnerID ==
                                                                                                   x.PartnerID) &&
                                                                                           x.BookDescriptionCatalog.
                                                                                               BookPublisher.
                                                                                               BookPublisherMargins.
                                                                                               First(
                                                                                                   z =>
                                                                                                   z.PartnerID ==
                                                                                                   x.PartnerID).Discount
                                                                                               .HasValue
                                                                                               ? x.
                                                                                                     BookDescriptionCatalog
                                                                                                     .BookPublisher.
                                                                                                     BookPublisherMargins
                                                                                                     .First
                                                                                                     (
                                                                                                         z =>
                                                                                                         z.PartnerID ==
                                                                                                         x.PartnerID).
                                                                                                     Discount.Value
                                                                                               : x.Partner.Discount)) /
                                                                          100)) <= thi.ExportFilter.MaxPrice) &&
                    x.PartnerPrice > 0 &&
                    partnersListIds.Contains(x.PartnerID) && x.BookPageRels.Any(z => pages.Any(c=> c.ID == z.PageID))).
                    GroupBy(x => x.DescriptionID).
                    Select(x => new
                        {
                            Priority =
                                    x.Min(
                                        z =>
                                        z.BookPageRels.First().CMSPage.PartnerPriorities.Any(
                                            v => v.PartnerID == z.PartnerID)
                                            ? z.BookPageRels.First().CMSPage.PartnerPriorities.First(
                                                v => v.PartnerID == z.PartnerID).Priority
                                            : z.Partner.SalePriority),
                            Item = x
                        })
                    .Select(x => x.Item.Where(z=> z.PartnerPrice>0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()).FirstOrDefault(z => z.Partner.SalePriority == x.Priority ));

            var exporter = new YmlExporter(filtered);

            if (thi.ProgressFunc != null)
                exporter.OnOrderFormed = thi.ProgressFunc;

            var catalog = exporter.CreateYmlCatalog();
            info.AddMessage("Запущено формирование YML структуры.");
            var result = exporter.ExportToFile(catalog, thi.ExportFilter.UseZip);
            info.AddMessage("Файл сформирован.");
            info.AddMessage(DateTime.Now.ToString("dd.MM.yyyy HH:mm") + " - Обработка завершена.");
            info.AddMessage(
                "<b style='font-size:14px'>Файл доступен по этой ссылке - <a target='_blank' href='{0}'>{0}</a></b>"
                    .FormatWith(
                        result));
            info.EndDate = DateTime.Now;
        }

        protected void OnOrderFormedYandex(int current, int total)
        {
            ParseringInfo info = ParseringInfo.Create("YandexExport");
            info.Created = current;
            info.Updated = total;
        }
    }
}
