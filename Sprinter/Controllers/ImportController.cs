using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq;
using System.Data.OleDb;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Security;
using System.Xml;
using System.Xml.Linq;
using NExcel;
using NExcel.Read.Biff;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class ImportController : Controller
    {
        //  private DB db = new DB();

        protected const int ImportParallelismDegree = 10;


        #region Импорт со старого сайта Спринтера
        #region Заказа со старого сайта Спринтера

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Orders()
        {
            return View(new DownloadInfo() { URL = "http://www.sprinter.ru/orders.xml.gz" });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Orders(DownloadInfo model)
        {
            ParseringInfo info = ParseringInfo.Create("SprinterOrders");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("SprinterOrders");

            info.ParseURL = model.URL;

            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            var workingThread = new Thread(ThreadFuncSprinterOrders);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(model);
        }

        protected static void ThreadFuncSprinterOrders(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("SprinterOrders");
            var path = HttpContext.Current.Server.CreateDir("/Temp/Sprinter/");
            WebUnzipper unzipper = new WebUnzipper(path, "SprinterOrders", info.ParseURL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                return;
            }

            string corrected_cat = unzipper.ResultFileName.Replace(".", "_correct.");


            StreamReader sr = new StreamReader(unzipper.ResultFileName, Encoding.GetEncoding(1251));
            StreamWriter sw = new StreamWriter(corrected_cat, false, Encoding.GetEncoding(1251));
            while (sr.Peek() >= 0)
            {
                //ебаный пиздец
                string line =
                    sr.ReadLine().Replace("\x13", "");

                sw.WriteLine(line);
            }
            sr.Close();
            sw.Close();

            Deliveries.Add("", 0);
            Deliveries.Add("Самовывоз", 1);
            Deliveries.Add("Курьером", 2);
            Deliveries.Add("Почтой России", 3);
            Deliveries.Add("Почтой России в дальнее зарубежье", 4);
            Deliveries.Add("Пони-Экспресс", 5);

            Payments.Add("", 0);
            Payments.Add("Наличными", 1);
            Payments.Add("Оплата через Банк", 2);
            Payments.Add("Наложенным платежом", 3);
            Payments.Add("Оплата банковской картой", 4);
            Payments.Add("Оплата по безналичному расчету", 5);
            Payments.Add("WebMoney / Yandex-деньги", 6);
            Payments.Add("Оплата через сервис Robokassa", 7);


            using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                       new XmlReaderSettings
                                                           {
                                                               DtdProcessing = DtdProcessing.Ignore,
                                                               IgnoreWhitespace = true,
                                                               IgnoreComments = true
                                                           }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("orders");
                reader.ReadToDescendant("item");

                reader.ReadElements("item").AsParallel().WithDegreeOfParallelism(10).Select(
                    x => ProcessOrdersRecord(x, HttpContext)).Count();

            }
            info.EndDate = DateTime.Now;
            info.AddMessage("Обработка завершена.");
        }

        private static Dictionary<string, int> Deliveries = new Dictionary<string, int>();
        private static Dictionary<string, int> Payments = new Dictionary<string, int>();

        public static bool ProcessOrdersRecord(XElement order, HttpContext context)
        {


            System.Web.HttpContext.Current = context;

            ParseringInfo info = ParseringInfo.Create("SprinterOrders");
            if (info.IsItemProcessed(order.Attribute("id").Value, true))
            {
                info.Errors++;
                return true;
            }

            info.AddProcessedItem(order.Attribute("id").Value, true);

            DB db = new DB();
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
            }

            var importID = order.Attribute("id").Value;

            var exist = db.Orders.FirstOrDefault(x => x.ImportID == importID);
            if (exist == null)
            {
                DateTime date = DateTime.Now;
                DateTime.TryParseExact(order.Attribute("date").Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture,
                                       DateTimeStyles.None, out date);
                var user =
                    db.UserProfiles.FirstOrDefault(
                        x =>
                        x.ImportUID == order.Attribute("user_id").Value ||
                        x.SecImportUID == order.Attribute("user_id").Value);
                if (user == null)
                {
                    info.AddMessage("Пользователь " + order.Attribute("user_id").Value + " не найден в БД.");
                    info.Errors++;
                    return true;

                }

                string sStatus = order.Element("status").Value.Trim().ToNiceForm();
                var dbs = db.OrderStatus.FirstOrDefault(x => x.Status == sStatus);
                if (dbs == null)
                {
                    dbs = new OrderStatus() { EngName = "", Status = sStatus };
                    db.OrderStatus.InsertOnSubmit(dbs);
                }
                var region = db.OrderDeliveryRegions.FirstOrDefault(x => x.Name == order.Element("region").Value);
                exist = new Order()
                    {
                        CreateDate = date,
                        ImportID = importID,
                        Number = order.Element("num").Value.ToInt(),
                        User = user.User,
                        OrderStatus = dbs
                    };

                var orderDetails = new OrderDetail()
                    {
                        DeliveryCost = order.Element("dostavka_price").Value.ToDecimal(),
                        DeliveryType = Deliveries[order.Element("dostavka").Value],
                        PaymentType = Payments[order.Element("payment").Value],
                        OrderDeliveryRegion = region,
                        Order = exist

                    };
                db.Orders.InsertOnSubmit(exist);
                db.OrderDetails.InsertOnSubmit(orderDetails);

                var goods = order.Element("goods").Elements("good");
                int skipped = 0;
                foreach (var good in goods)
                {
                    var dsc = db.BookDescriptionCatalogs.FirstOrDefault(x => x.ProviderUID == good.Attribute("id").Value);
                    if (dsc != null)
                    {
                        var dbg = new OrderedBook()
                            {
                                Amount = good.Attribute("kolvo").Value.ToInt(),
                                ImportUID = good.Attribute("id").Value,
                                Order = exist,
                                BookDescriptionCatalog = dsc,
                                SalePrice = good.Attribute("price").Value.ToDecimal()
                            };
                        db.OrderedBooks.InsertOnSubmit(dbg);
                    }
                    else
                    {
                        skipped++;
                        info.AddMessage("Товар " + good.Attribute("id").Value + " не найден в БД");
                    }
                }

                if (skipped == goods.Count())
                {
                    info.Errors++;
                    info.AddMessage("Заказ пропущен - не найдена ни одна книга. / UID = " + order.Attribute("id").Value);
                    return true;
                }



                db.SubmitChanges();
                info.Created++;
            }

            return true;

        }

        #endregion
        #region Пользователи со старого сайта Спринтера

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Users()
        {
            return View(new DownloadInfo() { URL = "http://www.sprinter.ru/customers.xml.gz" });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Users(DownloadInfo model)
        {
            ParseringInfo info = ParseringInfo.Create("SprinterUsers");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("SprinterUsers");

            info.ParseURL = model.URL;

            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            var workingThread = new Thread(ThreadFuncSprinterUsers);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(model);
        }

        protected static void ThreadFuncSprinterUsers(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("SprinterUsers");
            var path = HttpContext.Current.Server.CreateDir("/Temp/Sprinter/");
            WebUnzipper unzipper = new WebUnzipper(path, "SprinterUsers", info.ParseURL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                return;
            }

            string corrected_cat = unzipper.ResultFileName.Replace(".", "_correct.");


            StreamReader sr = new StreamReader(unzipper.ResultFileName, Encoding.GetEncoding(1251));
            StreamWriter sw = new StreamWriter(corrected_cat, false, Encoding.GetEncoding(1251));
            while (sr.Peek() >= 0)
            {
                //ебаный пиздец
                string line =
                    sr.ReadLine().Replace("\x13", "");

                sw.WriteLine(line);
            }
            sr.Close();
            sw.Close();



            using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                       new XmlReaderSettings
                                                           {
                                                               DtdProcessing = DtdProcessing.Ignore,
                                                               IgnoreWhitespace = true,
                                                               IgnoreComments = true
                                                           }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("customers");
                reader.ReadToDescendant("item");

                reader.ReadElements("item").AsParallel().WithDegreeOfParallelism(10).Select(
                    x => ProcessUserRecord(x, HttpContext)).Count();

            }
            info.EndDate = DateTime.Now;
            info.AddMessage("Обработка завершена.");
        }


        public static bool ProcessUserRecord(XElement user, HttpContext context)
        {


            System.Web.HttpContext.Current = context;

            ParseringInfo info = ParseringInfo.Create("SprinterUsers");
            if (info.IsItemProcessed(user.Attribute("id").Value, true))
            {
                info.Errors++;
                return true;
            }

            info.AddProcessedItem(user.Attribute("id").Value, true);

            DB db = new DB();
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
            }

            var importID = user.Attribute("id").Value;

            var exist = db.UserProfiles.FirstOrDefault(x => x.ImportUID == importID || x.SecImportUID == importID);
            if (exist == null)
            {

                string ipAddr = user.Attribute("user_ip").Value;
                DateTime regDate = DateTime.MinValue;
                DateTime.TryParseExact(user.Attribute("date").Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture,
                                       DateTimeStyles.None, out regDate);

                var mail = user.Element("email").Value.Replace(",", ".");
                if (mail.IsNullOrEmpty())
                    mail = user.Element("phone").Value.ReduceSpaces();
                if (mail.IsNullOrEmpty())
                    mail = user.Element("mobile").Value.ReduceSpaces();
                MembershipUser us = null;
                try
                {
                    var ex = Membership.GetUser(mail);
                    if (ex != null)
                    {
                        var profileEx = db.UserProfiles.FirstOrDefault(x => x.UserID == (Guid)ex.ProviderUserKey);
                        if (profileEx != null && profileEx.SecImportUID.IsNullOrEmpty())
                        {
                            profileEx.SecImportUID = user.Attribute("id").Value;
                            db.SubmitChanges();
                            info.Created++;
                            return true;
                        }
                    }
                    if (ex == null)

                        us = Membership.CreateUser(mail,
                                                   user.Element("password").Value.IsNullOrEmpty() ||
                                                   user.Element("password").Value.Contains("http")
                                                       ? new Random(DateTime.Now.Millisecond).GeneratePassword(6)
                                                       : user.Element("password").Value,
                                                   mail.IsMailAdress() ? mail : AccessHelper.NoMail);
                    else us = ex;
                }
                catch (MembershipCreateUserException ce)
                {
                    if (ce.StatusCode == MembershipCreateStatus.InvalidPassword)
                    {
                        us = Membership.CreateUser(mail,
                                                   new Random(DateTime.Now.Millisecond).GeneratePassword(6),
                                                   mail.IsMailAdress() ? mail : AccessHelper.NoMail);
                    }
                    else if (ce.StatusCode == MembershipCreateStatus.InvalidUserName)
                    {
                        us = Membership.CreateUser(new Random(DateTime.Now.Millisecond).GeneratePassword(6),
                                                   user.Element("password").Value.IsNullOrEmpty() ||
                                                   user.Element("password").Value.Contains("http")
                                                       ? new Random(DateTime.Now.Millisecond).GeneratePassword(6)
                                                       : user.Element("password").Value,
                                                   mail.IsMailAdress() ? mail : AccessHelper.NoMail);
                    }


                }
                catch (Exception e)
                {
                    info.AddMessage(e.Message);
                    info.AddMessage((string)user);
                    info.Errors++;
                    return true;
                }
                if (!Roles.IsUserInRole(us.UserName, "Client"))
                    Roles.AddUserToRole(us.UserName, "Client");
                Membership.UpdateUser(us);

                var profile = db.UserProfiles.FirstOrDefault(x => x.UserID == (Guid)us.ProviderUserKey);
                if (profile == null)
                {
                    profile = new UserProfile() { UserID = (Guid)us.ProviderUserKey };
                    db.UserProfiles.InsertOnSubmit(profile);
                    profile.ImportUID = importID;

                    profile.FromIP = ipAddr.ToIPInt();
                    if (regDate != DateTime.MinValue)
                        profile.RegDate = regDate;
                    profile.Name = user.Element("name").Value.ReduceSpaces().ToNiceForm();
                    profile.Surname = user.Element("second_name").Value.ReduceSpaces().ToNiceForm();
                    profile.Patrinomic = user.Element("middle_name").Value.ReduceSpaces().ToNiceForm();
                    profile.HomePhone = user.Element("phone").Value.ReduceSpaces();
                    profile.MobilePhone = user.Element("mobile").Value.ReduceSpaces();
                    if (profile.MobilePhone.Length > 150) profile.MobilePhone = "";
                    profile.Region = user.Element("region").Value;
                    profile.Address = user.Element("address").Value;
                    profile.Floor = user.Element("floor").Value;
                    profile.Metro = user.Element("metro").Value;
                }
                else if (profile.ImportUID != importID && profile.SecImportUID == null)
                {
                    profile.SecImportUID = importID;
                }
                try
                {
                    db.SubmitChanges();
                }
                catch (Exception ex)
                {
                    info.Errors++;
                    info.AddMessage(ex.Message);
                    info.AddMessage((string)user);
                }
                info.Created++;
            }
            return true;

        }

        #endregion
        #region Каталог со старого сайта Спринтера


        #region Первая версия импорта каталога
        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Sprinter()
        {
            return View(new DownloadInfo() { URL = "http://www.sprinter.ru/catalog_all.xml.gz" });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Sprinter(DownloadInfo model)
        {
            ParseringInfo info = ParseringInfo.Create("Sprinter");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("Sprinter");

            info.ParseURL = model.URL;
            DB db = new DB();
            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            info.Provider = db.BookDescriptionProviders.FirstOrDefault(x => x.ProviderName == "sprinter.ru");
            var workingThread = new Thread(ThreadFuncSprinter);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(model);


        }

        protected static void ThreadFuncSprinter(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("Sprinter");
            var path = HttpContext.Current.Server.CreateDir("/Temp/Sprinter/");


            WebUnzipper unzipper = new WebUnzipper(path, "Sprinter", info.ParseURL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                return;
            }

            string xmlPath = unzipper.ResultFileName;
            //string xmlPath = "D:\\Sites\\Sprinter\\Sprinter\\Sprinter\\Temp\\Sprinter\\catalog.yml";
            string corrected_cat = xmlPath.Replace(".", "_correct.");


            StreamReader sr = new StreamReader(xmlPath, Encoding.GetEncoding(1251));
            StreamWriter sw = new StreamWriter(corrected_cat, false, Encoding.GetEncoding(1251));
            long counter = 0;
            while (sr.Peek() >= 0)
            {
                //ебаный пиздец
                string line =
                    sr.ReadLine().Replace("&amp;quot;", "&quot;").Replace("<Объект не", "").Replace("<>", "").Replace(
                        "<неуказано>", "").Replace("t&t", "t&amp;t").Replace("<Сервис>", "&quot;Сервис&quot;").Replace(
                            "<Туризм>", "&quot;Туризм&quot;").Replace("s&m", "s&amp;m").Replace("<У Никитских ворот>",
                                                                                                "&quot;У Никитских ворот&quot;")
                        .Replace("&nbsp;", " ").Replace("<Юридическая книга>", "&quot;Юридическая книга&quot;").Replace(
                            "\x0b", ".").Replace("A&i", "A&amp;i")
                        .Replace("V&a", "V&amp;a").Replace("</B>", "").Replace("</FONT>", "").Replace("Book&breakfast",
                                                                                                      "Book&amp;breakfast")
                        .Replace("<br>", ", ").Replace("\x07", "").Replace("<Спутник>", "&quot;Спутник&quot;").
                        Replace("&anton", "&amp;anton").Replace("\x14", "").Replace("<Негоциант>",
                                                                                    "&quot;Негоциант&quot;").Replace(
                                                                                        "B&b", "B&amp;b")
                        .Replace("&ot", "&amp;ot").Replace("T&p", "T&amp;p").Replace("<В>", "").Replace("<FONT>", "").
                        Replace("<не указано>", "").Replace("e&i", "e&amp;i").Replace("<Редакция Ежедневной Газеты>",
                                                                                      "&quot;Редакция Ежедневной Газеты&quot;")
                        .Replace("e&l", "e&amp;l").Replace(" m&a ", " m&amp;a ").Replace("Work&o", "Work&amp;o").Replace
                        (" s&p ", " s&amp;p ").Replace("</</", "</").Replace("\x1F", " ").Replace("<не У., ", "").
                        Replace(", <не У.", "").Replace("<Не указано>", "");

                if (counter == 4404045)
                    line = "<page_extent>608</page_extent><categoryId>375</categoryId><description></description>";
                if (counter == 4781531)
                    line = "<page_extent>402</page_extent><categoryId>865</categoryId><description></description>";

                int index = line.IndexOf("&");
                while (index > 0)
                {
                    if ( /*line[index - 1].ToString().ToUpper() == line[index - 1].ToString()
                                                ||*/ line[index + 1].ToString().ToUpper() ==
                                                                                                                                                    line
                                                                                                                                                        [
                                                                                                                                                            index +
                                                                                                                                                            1
                                                                                                                                                        ]
                                                                                                                                                        .
                                                                                                                                                        ToString
                                                                                                                                                        ()
                                                                                                                                                    ||
                                                                                                                                                    (line
                                                                                                                                                         [
                                                                                                                                                             index -
                                                                                                                                                             1
                                                                                                                                                         ]
                                                                                                                                                         .
                                                                                                                                                         ToString
                                                                                                                                                         () ==
                                                                                                                                                     " " &&
                                                                                                                                                     line
                                                                                                                                                         [
                                                                                                                                                             index +
                                                                                                                                                             1
                                                                                                                                                         ]
                                                                                                                                                         .
                                                                                                                                                         ToString
                                                                                                                                                         () ==
                                                                                                                                                     " ")
                        )
                    {
                        line = line.Substring(0, index) + "&amp;" + line.Substring(index + 1);
                    }
                    index = line.IndexOf("&", index + 1);
                }
                sw.WriteLine(line);
                counter++;
            }
            sw.WriteLine("</catalog>");
            sr.Close();
            sw.Close();


            info.Dirs = 0;
            using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                       new XmlReaderSettings
                                                           {
                                                               DtdProcessing = DtdProcessing.Ignore,
                                                               IgnoreWhitespace = true,
                                                               IgnoreComments = true
                                                           }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("catalog");

                reader.ReadToDescendant("category");

                var allList = new List<CatalogStruct>();
                CatalogStruct root = null;
                var cats = reader.ReadElements("category");
                foreach (var category in cats)
                {
                    info.Dirs++;
                    string parent = "0";
                    var parentAttr = category.Attribute("parentId");
                    if (parentAttr != null)
                    {
                        parent = parentAttr.Value;
                    }
                    string name = category.Value;
                    string url = name.Translit().ToLower();
                    if (parent == "0")
                    {
                        root = new CatalogStruct()
                            {
                                Name = name,
                                Parent = null,
                                ProviderID = category.Attribute("id").Value.ToInt(),
                                Children = new List<CatalogStruct>(),
                                Url = url,
                                ProviderParentID = 0,

                            };
                        allList.Add(root);

                    }
                    else
                    {
                        var parentNode = allList.FirstOrDefault(x => x.ProviderID == parent.ToInt());

                        if (parentNode == null)
                        {
                            parentNode = root;

                        }
                        var child = new CatalogStruct()
                            {
                                Name = name,
                                Parent = parentNode,
                                ProviderID = category.Attribute("id").Value.ToInt(),
                                Children = new List<CatalogStruct>(),
                                Url = url,
                                ProviderParentID = parent.ToInt(),

                            };
                        parentNode.Children.Add(child);
                        allList.Add(child);

                    }
                }

                root.SaveTree();
                info.AddMessage("Обработано " + info.Dirs + " страниц каталога");
                //reader.ReadToFollowing("good");
                //<author>Лотман Л. М., Браже Т. Г., Скатов Н. Н.&nbsp; все, Ионин Г.Н.&nbsp; скрыть</author>
            }

            using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                       new XmlReaderSettings
                                                           {
                                                               DtdProcessing = DtdProcessing.Ignore,
                                                               IgnoreWhitespace = true,
                                                               IgnoreComments = true
                                                           }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("catalog");
                reader.ReadToDescendant("good");

                reader.ReadElements("good").AsParallel().WithDegreeOfParallelism(10).Select(
                    x => ProcessCatalogRecord(x, HttpContext)).Count();

            }

            info.EndDate = DateTime.Now;
            info.AddMessage("Обработка завершена.");
        }


        protected static Dictionary<int, int> _sprinterSuppliers;

        protected static Dictionary<int, int> SprinterSuppliers
        {
            get
            {
                if (_sprinterSuppliers == null)
                {
                    _sprinterSuppliers = new Dictionary<int, int>();
                    _sprinterSuppliers.Add(1, 2);
                    _sprinterSuppliers.Add(4, 3);
                    _sprinterSuppliers.Add(8, 4);
                    _sprinterSuppliers.Add(9, 5);
                    _sprinterSuppliers.Add(25, 6);
                    _sprinterSuppliers.Add(29, 7);
                    _sprinterSuppliers.Add(31, 8);
                    _sprinterSuppliers.Add(38, 9);
                    _sprinterSuppliers.Add(64, 10);
                    _sprinterSuppliers.Add(70, 11);
                    _sprinterSuppliers.Add(72, 12);
                    _sprinterSuppliers.Add(73, 13);
                    _sprinterSuppliers.Add(74, 14);
                }
                return _sprinterSuppliers;
            }
        }

        public static bool ProcessCatalogRecord(XElement offer, HttpContext context)
        {


            System.Web.HttpContext.Current = context;

            ParseringInfo info = ParseringInfo.Create("Sprinter");
            if (info.IsItemProcessed(offer.Attribute("id").Value, true))
            {
                info.Errors++;
                return true;
            }

            info.AddProcessedItem(offer.Attribute("id").Value, true);

            DB db = new DB();
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
                var p = db.BookDescriptionProviders.FirstOrDefault(x => x.ID == info.Provider.ID);
                p.LastUpdateDate = DateTime.Now;
                db.SubmitChanges();
            }
            string ISBN = "";
            var isbnNode = offer.Element("ISBN");

            if (isbnNode != null)
                ISBN = isbnNode.Value.Trim();


            string sYear = "";

            var yearNode = offer.Element("year");
            if (yearNode != null) sYear = yearNode.Value.Trim();

            var isbnList = ISBN.Split(new string[] { ";", ",", " " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!isbnList.Any())
                isbnList.Add("");

            foreach (var isbn in isbnList)
            {
                try
                {

                    var normalIsbn = EAN13.NormalizeIsbn(isbn);
                    if (normalIsbn.Replace("-", "").Length < 7)
                    {
                        normalIsbn = "";
                    }

                    //if (normalIsbn.IsNullOrEmpty()) continue;
                    if (normalIsbn.Length > 13)
                        normalIsbn = normalIsbn.Substring(0, 13);

                    long ean = 0;
                    if (normalIsbn.IsNullOrEmpty())
                        ean = 0;
                    else ean = long.Parse(EAN13.IsbnToEan13(normalIsbn));
                    BookDescriptionCatalog book =
                        db.BookDescriptionCatalogs.FirstOrDefault(
                        /*x => (x.EAN == ean) && x.Header.ToLower().Trim() == offer.Element("name").Value.Trim()*/
                            x => x.DataSourceID == 10 && x.ProviderUID == (string)offer.Attribute("id")
                            );
                    bool created = false;
                    if (book == null)
                    {
                        created = true;
                        book = new BookDescriptionCatalog()
                            {

                                DataSourceID = info.Provider.ID,
                                ProviderUID = (string)offer.Attribute("id"),

                                EAN = ean,
                                PublishYear = (sYear.IsNullOrEmpty()
                                                   ? (int?)null
                                                   : (int.Parse(sYear) > 0
                                                          ? int.Parse(sYear)
                                                          : (int?)null))
                            };
                        db.BookDescriptionCatalogs.InsertOnSubmit(book);
                        book.Header = offer.Element("name").Value.Trim();
                        info.Created++;

                    }
                    else
                    {
                        info.Updated++;
                        return true;
                    }

                    if (created)
                    {
                        book.ISBN = isbn.Replace("nothing", "0").ClearBorders();

                        string sPageCount = offer.Element("page_extent").Value;
                        if (!sPageCount.IsNullOrEmpty() && sPageCount.ToInt() > 0)
                            book.PageCount = sPageCount.ToInt();

                        if (offer.Element("binding").Value.Trim().Length < 100)
                            book.BookType = offer.Element("binding").Value.Trim();
                        else book.BookType = "";
                        var puplisherNode = offer.Element("publisher");
                        if (puplisherNode != null && book.BookPublisher == null && !puplisherNode.Value.IsNullOrEmpty())
                        {
                            string publisher = puplisherNode.Value.Trim();
                            var dbPub = db.BookPublishers.FirstOrDefault(x => x.Name == publisher);
                            if (dbPub == null)
                            {
                                dbPub = new BookPublisher() { Name = publisher };
                                db.BookPublishers.InsertOnSubmit(dbPub);
                            }
                            book.BookPublisher = dbPub;
                        }

                        var descrNode = offer.Element("description");
                        if (descrNode != null &&
                            (book.Annotation.IsNullOrEmpty() || book.Annotation.Length < descrNode.Value.Trim().Length))
                            book.Annotation = descrNode.Value.Trim();

                        var authNode = offer.Element("author");
                        if (authNode != null && !book.BookAuthorsRels.Any())
                        {

                            var authors =
                                authNode.Value.Replace("скрыть", "").Replace("все", "").Replace("  ", " ").Replace(
                                    "  ", " ")
                                    .Split(new string[] { ",", ";" },
                                           StringSplitOptions.
                                               RemoveEmptyEntries);

                            foreach (var author in authors)
                            {
                                if (!author.IsNullOrEmpty() && author.Length >= 2 &&
                                    author[0].ToString().ToUpper() == author[0].ToString())
                                {
                                    var dbAuthor = db.Authors.FirstOrDefault(x => x.FIO == author);
                                    if (dbAuthor == null)
                                    {
                                        dbAuthor = new Author { FIO = author };
                                        db.Authors.InsertOnSubmit(dbAuthor);

                                    }
                                    db.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel
                                        {
                                            Author = dbAuthor,
                                            BookDescriptionCatalog = book
                                        });
                                }
                            }
                        }


                        //теперь добавляем в товары

                    }
                    else
                    {
                        book.ProviderUID = (string)offer.Attribute("id");
                        book.DataSourceID = info.Provider.ID;
                    }


                    if (info.Provider.LoadCoversOnDemand &&
                        !(book.CoverID.HasValue && book.DataSourceID == info.Provider.ID))
                    {
                        try
                        {
                            var coverNode = offer.Element("picture");
                            if (coverNode != null)
                            {
                                string coverURL = coverNode.Value.Trim();
                                if (coverURL != "http://www.sprinter.ru/")
                                {
                                    WebClient client = new WebClient();
                                    byte[] imgData = client.DownloadData(coverURL);
                                    MemoryStream ms = new MemoryStream(imgData);
                                    Image bitmap = Image.FromStream(ms);

                                    if (!book.CoverID.HasValue)
                                    {
                                        BookCover cover = new BookCover
                                        {
                                            Data = imgData,
                                            Name = coverURL,
                                            Height = bitmap.Height,
                                            Width = bitmap.Width
                                        };
                                        db.BookCovers.InsertOnSubmit(cover);
                                        book.BookCover = cover;

                                    }
                                    else if (book.BookCover.Width < bitmap.Width)
                                    {
                                        book.BookCover.Data = imgData;
                                        book.BookCover.Name = coverURL;
                                        book.BookCover.Width = bitmap.Width;
                                        book.BookCover.Height = bitmap.Height;
                                    }
                                    bitmap.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            //info.AddMessage("Невозможно занрузить картинку URL = " + offer.Element("picture"));
                        }
                    }


                    var suppliers = offer.Elements("supplier");
                    var existSuppliers = new List<int>();
                    foreach (var supplier in suppliers)
                    {
                        if (SprinterSuppliers.ContainsKey(supplier.Attribute("id").Value.ToInt()))
                        {
                            existSuppliers.Add(SprinterSuppliers[supplier.Attribute("id").Value.ToInt()]);
                            var sale =
                                db.BookSaleCatalogs.FirstOrDefault(
                                    x =>
                                    x.PartnerID == SprinterSuppliers[supplier.Attribute("id").Value.ToInt()] &&
                                    x.PartnerUID == supplier.Attribute("code").Value);

                            var page =
                                db.CMSPages.FirstOrDefault(x => x.ImportID == offer.Element("categoryId").Value.ToInt());
                            DateTime dt;
                            if (!DateTime.TryParseExact(supplier.Attribute("date").Value, "yyyy-MM-dd",
                                                        CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                                dt = DateTime.Now;

                            if (sale == null)
                            {
                                sale = new BookSaleCatalog()
                                    {
                                        PartnerID = SprinterSuppliers[supplier.Attribute("id").Value.ToInt()],
                                        PartnerUID = supplier.Attribute("code").Value,
                                    };

                                db.BookSaleCatalogs.InsertOnSubmit(sale);

                                if (page != null)
                                {
                                    db.BookPageRels.InsertOnSubmit(new BookPageRel() { BookSaleCatalog = sale, CMSPage = page });
                                }

                                sale.PartnerPrice = supplier.Attribute("price").Value.ToDecimal();
                                sale.IsAvailable = offer.Attribute("available").Value.ToBool();
                                sale.LastUpdate = dt;

                            }
                            else
                            {
                                if (page != null)
                                {
                                    var rel =
                                        db.BookPageRels.FirstOrDefault(
                                            x => x.PageID == page.ID && x.SaleCatalogID == sale.ID);
                                    if (rel == null)
                                    {
                                        db.BookPageRels.InsertOnSubmit(new BookPageRel() { BookSaleCatalog = sale, CMSPage = page });
                                    }
                                }
                                if (sale.LastUpdate < dt)
                                {
                                    sale.PartnerPrice = supplier.Attribute("price").Value.ToDecimal();
                                    sale.IsAvailable = offer.Attribute("available").Value.ToBool();
                                }

                            }
                            sale.BookDescriptionCatalog = book;

                        }

                    }


                    db.SubmitChanges();


                    db.BookSaleCatalogs.DeleteAllOnSubmit(
                        db.BookSaleCatalogs.Where(
                            x => x.BookDescriptionCatalog == book && !existSuppliers.Contains(x.PartnerID)));

                    db.SubmitChanges();
                }
                catch (Exception e)
                {
                    info.AddMessage(e.Message);
                    info.AddMessage(offer.ToString());
                    info.AddMessage(e.StackTrace);
                    info.Errors++;
                }


            }
            return true;

        }
        #endregion
        #region Описания

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult SprinterReviews()
        {
            return View(new DownloadInfo() { URL = "http://www.sprinter.ru/1/reviews.xml.gz" });
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult SprinterReviews(DownloadInfo model)
        {
            ParseringInfo info = ParseringInfo.Create("SprinterReviews");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("SprinterReviews");

            info.ParseURL = model.URL;
            DB db = new DB();
            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            info.Provider = db.BookDescriptionProviders.FirstOrDefault(x => x.ProviderName == "sprinter.ru");
            var workingThread = new Thread(ThreadFuncSprinterReviews);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(model);
        }

        protected static void ThreadFuncSprinterReviews(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("SprinterReviews");
            var path = HttpContext.Current.Server.CreateDir("/Temp/SprinterReviews/");


            WebUnzipper unzipper = new WebUnzipper(path, "SprinterReviews", info.ParseURL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                return;
            }

            string xmlPath = unzipper.ResultFileName;
            //string xmlPath = "D:\\Sites\\Sprinter\\Sprinter\\Sprinter\\Temp\\Sprinter\\catalog.yml";
            string corrected_cat = xmlPath.Replace(".", "_correct.");


            StreamReader sr = new StreamReader(xmlPath, Encoding.GetEncoding(1251));
            StreamWriter sw = new StreamWriter(corrected_cat, false, Encoding.GetEncoding(1251));
            long counter = 0;
            while (sr.Peek() >= 0)
            {
                string line = sr.ReadLine();
                sw.WriteLine(line);
                counter++;
            }
            sr.Close();
            sw.Close();




            using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                       new XmlReaderSettings
                                                           {
                                                               DtdProcessing = DtdProcessing.Ignore,
                                                               IgnoreWhitespace = true,
                                                               IgnoreComments = true
                                                           }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("reviews");
                reader.ReadToDescendant("item");

                reader.ReadElements("item").AsParallel().WithDegreeOfParallelism(10).Select(
                    x => ProcessSprinterReviewsRecord(x, HttpContext)).Count();

            }

            info.EndDate = DateTime.Now;
            info.AddMessage("Обработка завершена.");
        }


        public static bool ProcessSprinterReviewsRecord(XElement offer, HttpContext context)
        {


            System.Web.HttpContext.Current = context;

            ParseringInfo info = ParseringInfo.Create("SprinterReviews");
            if (info.IsItemProcessed(offer.Attribute("id").Value, true))
            {
                info.Errors++;
                return true;
            }

            info.AddProcessedItem(offer.Attribute("id").Value, true);

            DB db = new DB();
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
                var p = db.BookDescriptionProviders.FirstOrDefault(x => x.ID == info.Provider.ID);
                p.LastUpdateDate = DateTime.Now;
                db.SubmitChanges();
            }

            DateTime commentDate;
            if (
                !DateTime.TryParseExact(offer.Attribute("date").Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture,
                                        DateTimeStyles.None, out commentDate)) return false;

            string importID = offer.Attribute("book_id").Value;

            string userName = offer.Element("user_name").Value;
            string userMail = offer.Element("e_mail").Value;
            string comment = offer.Element("text").Value;

            var book = db.BookDescriptionCatalogs.FirstOrDefault(x => x.ProviderUID == importID && x.DataSourceID == 10);
            if (book == null)
            {
                info.AddMessage("Книга с ИД = " + importID + " не найдена. Комментарий не добавлен.");
                info.Errors++;
                return false;
            }
            else
            {
                info.Updated++;
                var exist =
                    db.BookComments.FirstOrDefault(
                        x => x.DescriptionID == book.ID && x.ImportID == offer.Attribute("id").Value.ToInt());
                if (exist == null)
                {
                    exist = new BookComment()
                        {
                            Approved = true,
                            BookDescriptionCatalog = book,
                            Comment = comment,
                            UserMail = userMail,
                            ImportID = offer.Attribute("id").Value.ToInt(),
                            Date = commentDate,
                            UserName = userName
                        };
                    var usr = db.Users.FirstOrDefault(x => x.MembershipData.Email == userMail);
                    if (usr != null)
                        exist.User = usr;

                    db.BookComments.InsertOnSubmit(exist);
                    db.SubmitChanges();
                }
            }

            return true;

        }
        #endregion



        #region Импорт с описаниями

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult SprinterDescription(DownloadInfo model)
        {
            ParseringInfo info = ParseringInfo.Create("SprinterDescription");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("SprinterDescription");

            info.ParseURL = model.URL;
            DB db = new DB();
            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            info.Provider = db.BookDescriptionProviders.FirstOrDefault(x => x.ProviderName == "sprinter.ru");
            var workingThread = new Thread(ThreadFuncSprinterDescription);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(model);


        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult SprinterDescription()
        {
            return View(new DownloadInfo() { URL = "http://www.sprinter.ru/1/catalog.xml.gz" });
        }



        protected static void ThreadFuncSprinterDescription(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("SprinterDescription");
            var path = HttpContext.Current.Server.CreateDir("/Temp/SprinterDescription/");


                        WebUnzipper unzipper = new WebUnzipper(path, "SprinterDescription", info.ParseURL);
                        bool success = unzipper.GetFile();
                        if (!success)
                        {
                            return;
                        }


                        string xmlPath = unzipper.ResultFileName;
            //string xmlPath = "D:\\Sites\\Sprinter\\Sprinter\\Sprinter\\Temp\\SprinterDescription\\catalog.xml";
            //string xmlPath = "C:\\SPRINTER\\SITE\\Sprinter\\Temp\\SprinterDescription\\catalog.yml";
            string corrected_cat = xmlPath.Replace(".", "_correct.");


                  StreamReader sr = new StreamReader(xmlPath, Encoding.GetEncoding(1251));
                  StreamWriter sw = new StreamWriter(corrected_cat, false, Encoding.GetEncoding(1251));
                  long counter = 0;
                  Regex rxGoodPrice = new Regex(@"price=""[\d\.]{0,}""");
                  Regex rxGoodCode1 = new Regex(@"code=""[\d]{4,}?""");
                  Regex rxGoodCode2 = new Regex(@"code=""[A-Za-z]{1,4}[\d]{1,}?""");
                  //Regex rxGoodCode2= new Regex(@"code=""[A-Za-z\d]{4,}?""");
                  Regex rxPriceReplace = new Regex(@"price="".+?""");
                  Regex rxCodeReplace = new Regex(@"code="".+?""");
                  while (sr.Peek() >= 0)
                  {
                      //ебаный пиздец
                      string line =
                          sr.ReadLine()
                            .Replace("\x00", "")
                            .Replace("\x01", "")
                            .Replace("\x02", "")
                            .Replace("\x03", "")
                            .Replace("\x04", "")
                            .Replace("\x05", "")
                            .Replace("\x06", "")
                            .Replace("\x07", "")
                            .Replace("\x08", "")
                            .Replace("\x09", "")
                            .Replace("\x0B", "")
                            .Replace("\x0C", "")
                            .Replace("\x0E", "")
                            .Replace("\x0F", "")
                            .Replace("\x10", "")
                            .Replace("\x11", "")
                            .Replace("\x12", "")
                            .Replace("\x13", "")
                            .Replace("\x14", "")
                            .Replace("\x15", "")
                            .Replace("\x16", "")
                            .Replace("\x17", "")
                            .Replace("\x18", "")
                            .Replace("\x19", "")
                            .Replace("\x1A", "")
                            .Replace("\x1B", "")
                            .Replace("\x1C", "")
                            .Replace("\x1D", "")
                            .Replace("\x1E", "")
                            .Replace("\x1F", "").Replace("Q&A", "Q&amp;A");
                      if (rxGoodPrice.Matches(line).Count == 0)
                      {
                          line = rxPriceReplace.Replace(line, "price=\"\"");
                      }
                      if (rxGoodCode1.Matches(line).Count == 0 && rxGoodCode2.Matches(line).Count == 0)
                      {
                          line = rxCodeReplace.Replace(line, "code=\"\"");
                      }
                      sw.WriteLine(line);
                      counter++;
                  }
                  sw.WriteLine("</catalog>");
                  sr.Close();
                  sw.Close();

            /*            info.Dirs = 0;

                        using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                                   new XmlReaderSettings
                                                                       {
                                                                           DtdProcessing = DtdProcessing.Ignore,
                                                                           IgnoreWhitespace = true,
                                                                           IgnoreComments = true
                                                                       }))
                        {
                            reader.MoveToContent();
                            reader.ReadStartElement("catalog");

                            reader.ReadToDescendant("category");

                            var allList = new List<CatalogStruct>();
                            CatalogStruct root = null;
                            var cats = reader.ReadElements("category");
                            var forSkip = new List<string>() { "/catalog/35/", "/catalog/36/", "/catalog/37/", "/catalog/20/" };
                            foreach (var category in cats)
                            {
                                info.Dirs++;
                                string parent = "0";
                                var parentAttr = category.Attribute("parentId");
                                if (parentAttr != null)
                                {
                                    parent = parentAttr.Value;
                                }

                                var linkAttr = category.Attribute("link");
                                string link = linkAttr == null ? "" : linkAttr.Value;
                                if (forSkip.Any(link.Contains)) continue;

                                string name = category.Value;
                                string url = name.Translit().ToLower();
                                if (parent == "0")
                                {
                                    root = new CatalogStruct()
                                        {
                                            Name = name,
                                            Parent = null,
                                            ProviderID = category.Attribute("id").Value.ToInt(),
                                            Children = new List<CatalogStruct>(),
                                            Url = url,
                                            ProviderParentID = 0,

                                        };
                                    allList.Add(root);

                                }
                                else
                                {
                                    var parentNode = allList.FirstOrDefault(x => x.ProviderID == parent.ToInt());

                                    if (parentNode == null)
                                    {
                                        parentNode = root;

                                    }
                                    var child = new CatalogStruct()
                                        {
                                            Name = name,
                                            Parent = parentNode,
                                            ProviderID = category.Attribute("id").Value.ToInt(),
                                            Children = new List<CatalogStruct>(),
                                            Url = url,
                                            ProviderParentID = parent.ToInt(),
                                            RedirectUrl = link

                                        };
                                    parentNode.Children.Add(child);
                                    allList.Add(child);

                                }
                            }

                            root.SaveTree();
                            info.AddMessage("Обработано " + info.Dirs + " страниц каталога");
                        }*/




            /*            var db = new DB();
                        using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                                   new XmlReaderSettings
                                                                       {
                                                                           DtdProcessing = DtdProcessing.Ignore,
                                                                           IgnoreWhitespace = true,
                                                                           IgnoreComments = true
                                                                       }))
                        {
                            reader.MoveToContent();
                            reader.ReadStartElement("catalog");

                            reader.ReadToDescendant("category");

                            var cats = reader.ReadElements("category");
                            foreach (var category in cats)
                            {
                                info.Dirs++;
                                var id = category.Attribute("id");
                                var link = category.Attribute("link");
                                if (link != null && id != null)
                                {
                                    var page = db.CMSPages.FirstOrDefault(x => x.ImportID == id.Value.ToInt());
                                    if (page != null)
                                    {
                                        page.OriginalURL = link.Value;
                                    }

                                }
                            }
                            info.AddMessage("Обработано " + info.Dirs + " страниц каталога");
                        }
                        db.SubmitChanges();*/
            using (XmlReader reader = XmlReader.Create(new StreamReader(corrected_cat, Encoding.GetEncoding(1251)),
                                                       new XmlReaderSettings
                                                           {
                                                               DtdProcessing = DtdProcessing.Ignore,
                                                               IgnoreWhitespace = true,
                                                               IgnoreComments = true
                                                           }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("catalog");
                reader.ReadToDescendant("good");

                reader.ReadElements("good").AsParallel().WithDegreeOfParallelism(10).Select(
                    x => ProcessSprinterDescriptionRecord(x, HttpContext)).Count();

            }
            info.EndDate = DateTime.Now;
            info.AddMessage("Обработка завершена.");

        }

        protected static Dictionary<int, int> _suppliersRels;
        protected static Dictionary<int, int> SuppliersRels
        {
            get
            {
                if (_suppliersRels == null)
                {
                    _suppliersRels = new Dictionary<int, int>();
                    _suppliersRels.Add(1, 2);//УЧКНИГА
                    _suppliersRels.Add(2, 1);//ЮРАЙТ
                    _suppliersRels.Add(4, 3);//Абрис
                    _suppliersRels.Add(5, 19);//УМНИК
                    _suppliersRels.Add(6, 20);//ADMOS
                    _suppliersRels.Add(7, 21);//ТД Школьник
                    _suppliersRels.Add(8, 4);//Британия
                    _suppliersRels.Add(9, 5);//Логосфера
                    _suppliersRels.Add(10, 22);//Медицина
                    _suppliersRels.Add(19, 23);//Инфра-М
                    _suppliersRels.Add(25, 6);//Релод
                    _suppliersRels.Add(28, 24);//МИА
                    _suppliersRels.Add(29, 7);//Эксмо
                    _suppliersRels.Add(31, 8);//Лабиринт
                    _suppliersRels.Add(38, 9);//Кнорус
                    _suppliersRels.Add(40, 25);//Медпрактика
                    _suppliersRels.Add(41, 26);//Ювента
                    _suppliersRels.Add(43, 27);//Фирма Диля
                    _suppliersRels.Add(47, 15);//Глобус
                    _suppliersRels.Add(57, 28);//Книголюб
                    _suppliersRels.Add(58, 29);//АСТ
                    _suppliersRels.Add(64, 10);//36,6
                    _suppliersRels.Add(69, 30);//Спринтер-Опт
                    _suppliersRels.Add(70, 11);//Премьер-игрушка
                    _suppliersRels.Add(72, 12);//ИП Яковлев
                    _suppliersRels.Add(73, 13);//Юпитер
                    _suppliersRels.Add(74, 14);//Белый город
                    _suppliersRels.Add(75, 0);//Тен видео
                    _suppliersRels.Add(76, 16);//Книга по Требованию
                    _suppliersRels.Add(77, 18);//СервисТорг
                }
                return _suppliersRels;
            }
        }

        public static bool ProcessSprinterDescriptionRecord(XElement offer, HttpContext context)
        {


            System.Web.HttpContext.Current = context;

            ParseringInfo info = ParseringInfo.Create("SprinterDescription");
            if (info.IsItemProcessed(offer.Attribute("id").Value, true))
            {
                info.Errors++;
                return true;
            }

            info.AddProcessedItem(offer.Attribute("id").Value, true);

            var db = new DB();
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
                var p = db.BookDescriptionProviders.FirstOrDefault(x => x.ID == info.Provider.ID);
                p.LastUpdateDate = DateTime.Now;
                db.SubmitChanges();
            }
            string ISBN = "";
            var isbnNode = offer.Element("ISBN");

            if (isbnNode != null)
                ISBN = isbnNode.Value.Trim();


            string sYear = "";

            var yearNode = offer.Element("year");
            if (yearNode != null) sYear = yearNode.Value.Trim();

            try
            {


                var normalIsbn = EAN13.NormalizeIsbn(ISBN);
                if (normalIsbn.Replace("-", "").Length < 7)
                {
                    normalIsbn = "";
                }

                //if (normalIsbn.IsNullOrEmpty()) continue;
                if (normalIsbn.Length > 13)
                    normalIsbn = normalIsbn.Substring(0, 13);

                long ean = normalIsbn.IsNullOrEmpty() ? 0 : long.Parse(EAN13.IsbnToEan13(normalIsbn));

                /*     var paramList = new BookAdditionalParams();
                     if (sYear.IsFilled())
                         paramList.Year = ImportData.ParseYear(sYear);

                     var pageExtent = offer.Element("page_extent");
                     if (pageExtent != null && pageExtent.Value.IsFilled())
                         paramList.Pages = int.Parse(pageExtent.Value);

                     var binding = offer.Element("binding");
                     if (binding != null && binding.Value.IsFilled())
                         paramList.Format = binding.Value;

                     var series = offer.Element("series");
                     if (series != null && series.Value.IsFilled())
                         paramList.Series = series.Value;

                     var sved = offer.Element("sved");
                     if (sved != null && sved.Value.IsFilled())
                         paramList.Details = sved.Value;

                     var dopsved = offer.Element("dopsved");
                     if (dopsved != null && dopsved.Value.IsFilled())
                         paramList.AdditionalDetails = dopsved.Value;

                     var grif = offer.Element("grif");
                     if (grif != null && grif.Value.IsFilled())
                         paramList.Stamp = grif.Value;

                     var vidizd = offer.Element("vidizd");
                     if (vidizd != null && vidizd.Value.IsFilled())
                         paramList.PublishingType = vidizd.Value;

                     var dluchzav = offer.Element("dluchzav");
                     if (dluchzav != null && dluchzav.Value.IsFilled())
                         paramList.ForLearning = dluchzav.Value;

                     var ves = offer.Element("ves");
                     if (ves != null && ves.Value.IsFilled() && ves.Value.Trim() != "0")
                         paramList.Weight = ImportData.ParsePrice(ves.Value);
                     else paramList.Weight = null;

                     var prilozh = offer.Element("prilozh");
                     if (prilozh != null && prilozh.Value.IsFilled())
                         paramList.Apps = prilozh.Value;

                     var opisan = offer.Element("opisan");
                     if (opisan != null && opisan.Value.IsFilled())
                         paramList.ShortDescription = opisan.Value;

                     var jazikorig = offer.Element("jazikorig");
                     if (jazikorig != null && jazikorig.Value.IsFilled())
                         paramList.Language = jazikorig.Value;

                     var illustr = offer.Element("illustr");
                     if (illustr != null && illustr.Value.IsFilled())
                         paramList.Illustrations = illustr.Value;
     */
                var dids = new List<int>();
                var suppliers = offer.Elements("supplier");
                foreach (var supplier in suppliers)
                {

                    int sid = supplier.Attribute("id").Value.ToInt();
                    if (!SuppliersRels.ContainsKey(sid)) continue;
                    var partnerID = SuppliersRels[sid];
                    if (partnerID == 0) continue;
                    if (partnerID == 16) continue;
                    if (supplier.Attribute("code") == null) continue;
                    string code = supplier.Attribute("code").Value;
                    if (code.IsNullOrEmpty()) continue;

                    if (supplier.Attribute("price") == null) continue;
                    decimal price = ImportData.ParsePrice(supplier.Attribute("price").Value);
                    //if (price == 0) continue;

                    /*
                                        DateTime updateDate;
                                        if (!DateTime.TryParseExact(supplier.Attribute("date").Value, "yyyy-MM-dd",
                                                                    CultureInfo.CurrentCulture,
                                                                    DateTimeStyles.None, out updateDate))
                                            updateDate = DateTime.Now;

                    */

                    var sale =
                        db.BookSaleCatalogs.FirstOrDefault(x => x.PartnerUID == code && x.PartnerID == partnerID);

                    var partner = db.Partners.First(x => x.ID == partnerID);
                    var possibleDataSource =
                        db.BookDescriptionProviders.FirstOrDefault(x => x.ProviderName == partner.Name);

                    BookDescriptionCatalog description = null;
                    if (sale == null)
                    {

                        description = db.BookDescriptionCatalogs.FirstOrDefault(
                            x =>
                            (x.DataSourceID == 10 && x.ProviderUID == (string)offer.Attribute("id")) ||
                            (ean > 0 && x.EAN == ean) || x.ISBN == ISBN ||
                            (possibleDataSource != null &&
                             (x.DataSourceID == possibleDataSource.ID && x.ProviderUID == code))
                            );
                        if (description == null)
                        {
                            if (code.IsNullOrEmpty()) continue;

                            BookPublisher publisher = null;
                            if (offer.Element("publisher") != null)
                            {
                                publisher =
                                    db.BookPublishers.FirstOrDefault(
                                        x => x.Name == offer.Element("publisher").Value.Trim());
                                if (publisher == null)
                                {
                                    publisher = new BookPublisher() { Name = offer.Element("publisher").Value.Trim() };
                                    db.BookPublishers.InsertOnSubmit(publisher);
                                }
                            }
                            description = new BookDescriptionCatalog()
                                {
                                    Header = offer.Element("name").Value,
                                    EAN = ean > 0 ? ean : (long?)null,
                                    ISBN = ISBN,
                                    DataSourceID = 10,
                                    ProviderUID = offer.Attribute("id").Value,
                                    Average = 0,
                                    TotalCount = 0,
                                    TotalSum = 0,
                                    BookPublisher = publisher,
                                };

                            if (offer.Element("author") != null)
                            {
                                var authors = ImportData.CreateAuthorsList(offer.Element("author").Value);
                                foreach (string author in authors)
                                {
                                    var dba = db.Authors.FirstOrDefault(x => x.FIO == author);
                                    if (dba == null)
                                    {
                                        dba = new Author() { FIO = author };
                                        db.Authors.InsertOnSubmit(dba);
                                    }
                                    db.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel()
                                        {
                                            Author = dba,
                                            BookDescriptionCatalog = description
                                        });
                                }
                            }
                            if (partnerID != 16)
                            {
                                db.BookDescriptionCatalogs.InsertOnSubmit(description);
                            }

                        }

                        else
                        {
                            if (code.IsNullOrEmpty())
                                code = description.ProviderUID;
                            description.ProviderUID = offer.Attribute("id").Value;
                            description.DataSourceID = 10;
                        }
                        if (partnerID != 16 && code.IsFilled())
                        {
                            info.Created++;

                            sale = new BookSaleCatalog()
                                {
                                    BookDescriptionCatalog = description,
                                    LastUpdate = DateTime.Now,
                                    IsAvailable = supplier.Attribute("available").Value == "true",
                                    //IsAvailable = offer.Attribute("available").Value == "true",
                                    PartnerPrice = price,
                                    PartnerID = partnerID,
                                    PartnerUID = code
                                };
                            db.BookSaleCatalogs.InsertOnSubmit(sale);

                            var existSale =
                                description.BookSaleCatalogs.SelectMany(x => x.BookPageRels)
                                           .FirstOrDefault(x => x.BookSaleCatalog.PartnerID != 16);
                            if (existSale != null)
                            {
                                db.BookPageRels.InsertOnSubmit(new BookPageRel()
                                    {
                                        BookSaleCatalog = sale,
                                        PageID = existSale.PageID
                                    });
                            }
                            else
                            {
                                var targetPage =
                                    db.CMSPages.FirstOrDefault(
                                        x => x.ImportID == offer.Element("categoryId").Value.ToInt());
                                if (targetPage != null)
                                {
                                    db.BookPageRels.InsertOnSubmit(new BookPageRel()
                                        {
                                            BookSaleCatalog = sale,
                                            PageID = targetPage.ID
                                        });
                                }
                            }
                        }
                    }
                    else
                    {
                        info.Updated++;
                        description = sale.BookDescriptionCatalog;
                        description.ProviderUID = offer.Attribute("id").Value;
                        description.DataSourceID = 10;

                        if (partnerID != 16)
                        {
                            sale.PartnerPrice = price;
                            //sale.LastUpdate = updateDate;
                            sale.IsAvailable = supplier.Attribute("available").Value == "true";
                            //sale.IsAvailable = offer.Attribute("available").Value == "true";

                            if (!sale.BookPageRels.Any())
                            {
                                var targetPage =
                                    db.CMSPages.FirstOrDefault(
                                        x => x.ImportID == offer.Element("categoryId").Value.ToInt());
                                if (targetPage != null)
                                {
                                    db.BookPageRels.InsertOnSubmit(new BookPageRel()
                                    {
                                        BookSaleCatalog = sale,
                                        PageID = targetPage.ID
                                    });
                                }
                            }

                        }
                    }






                    /* if (description == null) continue;      
                      if (partnerID != 16)
                           {
                               if (description.Annotation.IsNullOrEmpty() ||
                                   description.Annotation.Length < offer.Element("description").Value.Length)
                                   description.Annotation = offer.Element("description").Value;
                           }

                           if (!description.CoverID.HasValue)
                           {
                               try
                               {
                                   var coverNode = offer.Element("picture");
                                   if (coverNode != null)
                                   {
                                       string coverURL = coverNode.Value.Trim();
                                       if (coverURL != "http://www.sprinter.ru/")
                                       {
                                           var client = new WebClient();
                                           byte[] imgData = client.DownloadData(coverURL);
                                           var ms = new MemoryStream(imgData);
                                           var bitmap = Image.FromStream(ms);

                                           if (!description.CoverID.HasValue)
                                           {
                                               var cover = new BookCover
                                                   {
                                                       Data = imgData,
                                                       Name = coverURL,
                                                       Height = bitmap.Height,
                                                       Width = bitmap.Width
                                                   };
                                               db.BookCovers.InsertOnSubmit(cover);
                                               description.BookCover = cover;

                                           }
                                           else if (description.BookCover.Width < bitmap.Width)
                                           {
                                               description.BookCover.Data = imgData;
                                               description.BookCover.Name = coverURL;
                                               description.BookCover.Width = bitmap.Width;
                                               description.BookCover.Height = bitmap.Height;
                                           }
                                           bitmap.Dispose();
                                       }
                                   }


                               }
                               catch (Exception e)
                               {
                                   //info.AddMessage("Невозможно загрузить картинку URL = " + offer.Element("picture"));
                               }
                           }*/
                    var rnd = new Random(DateTime.Now.Millisecond);
                    try
                    {
                        db.SubmitChanges();
                    }
                    catch
                    {

                        Thread.Sleep(rnd.Next(1000, 5000));
                        try
                        {
                            db.SubmitChanges();
                        }
                        catch
                        {
                            Thread.Sleep(rnd.Next(1000, 5000));
                            try
                            {
                                db.SubmitChanges();
                            }
                            catch
                            {
                                Thread.Sleep(rnd.Next(1000, 5000));
                                try
                                {
                                    db.SubmitChanges();
                                }
                                catch
                                {
                                    Thread.Sleep(rnd.Next(1000, 5000));
                                    db.SubmitChanges();
                                }
                            }
                        }
                    }
                    /*  if (!dids.Contains(description.ID))
                      {
                          if (description.ID > 0)
                              description.SaveParams(paramList);
                          dids.Add(description.ID);
                      }*/
                }

            }
            catch (Exception e)
            {
                info.AddMessage(e.Message);
                info.AddMessage(offer.ToString());
                info.AddMessage(e.StackTrace);
                info.Errors++;
            }


            return true;

        }


        #endregion



        #endregion
        #region Новинки со старого спринтера
        [HttpGet]
        [AuthorizeMaster]
        public ActionResult NewsList()
        {
            return View(new DownloadInfo() { URL = "http://sprinter.ru/yml/new.xml.gz" });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult NewsList(DownloadInfo model)
        {
            ParseringInfo info = ParseringInfo.Create("NewsList");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("NewsList");

            info.ParseURL = model.URL;
            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            var workingThread = new Thread(ThreadFuncNewsList);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(model);


        }


        protected static void ThreadFuncNewsList(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("NewsList");
            var path = HttpContext.Current.Server.CreateDir("/Temp/NewsList/");

            WebUnzipper unzipper = new WebUnzipper(path, "NewsList", info.ParseURL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                info.AddMessage("Ошибка при распаковке архива.", true);
                return;
            }

            try
            {

                var db = new DB();
                db.CommandTimeout = 3600;
                db.ExecuteCommand("update BookSaleCatalog set IsNew = 0");

                string xmlPath = unzipper.ResultFileName;
                info.Dirs = 0;
                using (XmlReader reader = XmlReader.Create(new StreamReader(xmlPath, Encoding.GetEncoding(1251)),
                                                           new XmlReaderSettings
                                                               {
                                                                   DtdProcessing = DtdProcessing.Ignore,
                                                                   IgnoreWhitespace = true,
                                                                   IgnoreComments = true
                                                               }))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("yml_catalog");
                    reader.ReadToDescendant("offer");

                    var count = reader.ReadElements("offer").AsParallel().WithDegreeOfParallelism(5).Select(
                        x => ProcessNewsListRecord(x, HttpContext)).Count();

                }
            }
            catch (Exception e)
            {
                info.AddMessage(e.Message);
                info.AddMessage(e.StackTrace);
            }
            info.EndDate = DateTime.Now;
            info.AddMessage("Обработка завершена.");
        }

        public static bool ProcessNewsListRecord(XElement offer, HttpContext context)
        {


            System.Web.HttpContext.Current = context;

            ParseringInfo info = ParseringInfo.Create("NewsList");
            if (info.IsItemProcessed(offer.Attribute("id").Value, true))
            {
                info.Errors++;
                return true;
            }

            info.AddProcessedItem(offer.Attribute("id").Value, true);

            DB db = new DB();
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
                var p = db.BookDescriptionProviders.FirstOrDefault(x => x.ID == info.Provider.ID);
                p.LastUpdateDate = DateTime.Now;
                db.SubmitChanges();
            }
            info.Created++;
            string ISBN = "";
            var isbnNode = offer.Element("ISBN");

            if (isbnNode != null)
                ISBN = isbnNode.Value.Trim();


            string sYear = "";

            var yearNode = offer.Element("year");
            if (yearNode != null) sYear = yearNode.Value.Trim();

            var isbnList = ISBN.Split(new string[] { ";", ",", " " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!isbnList.Any())
                isbnList.Add("");

            foreach (var isbn in isbnList)
            {
                try
                {
                    var normalIsbn = EAN13.NormalizeIsbn(isbn);
                    if (normalIsbn.Replace("-", "").Length < 7)
                    {
                        normalIsbn = "";
                    }

                    //if (normalIsbn.IsNullOrEmpty()) continue;
                    if (normalIsbn.Length > 13)
                        normalIsbn = normalIsbn.Substring(0, 13);

                    long ean = 0;
                    if (normalIsbn.IsNullOrEmpty())
                        ean = 0;
                    else ean = long.Parse(EAN13.IsbnToEan13(normalIsbn));
                    if (ean == 0) return false;
                    var books = db.BookDescriptionCatalogs.Where(x => x.EAN == ean /*&& x.Header.ToLower() == offer.Element("name").Value.ToLower()*/);
                    foreach (var book in books)
                    {
                        foreach (var sale in book.BookSaleCatalogs)
                        {
                            sale.IsNew = true;
                        }
                        info.Updated++;

                        if (!book.CoverID.HasValue)
                        {
                            try
                            {
                                var coverNode = offer.Element("picture");
                                if (coverNode != null)
                                {
                                    string coverURL = coverNode.Value.Trim();
                                    if (coverURL != "http://www.sprinter.ru/")
                                    {
                                        WebClient client = new WebClient();
                                        byte[] imgData = client.DownloadData(coverURL);
                                        MemoryStream ms = new MemoryStream(imgData);
                                        Image bitmap = Image.FromStream(ms);

                                        BookCover cover = new BookCover
                                        {
                                            Data = imgData,
                                            Name = coverURL,
                                            Height = bitmap.Height,
                                            Width = bitmap.Width
                                        };
                                        db.BookCovers.InsertOnSubmit(cover);
                                        book.BookCover = cover;

                                        bitmap.Dispose();
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                //info.AddMessage("Невозможно занрузить картинку URL = " + offer.Element("picture"));
                            }
                        }
                        var descrNode = offer.Element("description");
                        if (descrNode != null &&
                            (book.Annotation.IsNullOrEmpty() || book.Annotation.Length < descrNode.Value.Trim().Length))
                            book.Annotation = descrNode.Value.Trim();

                        var extentNode = offer.Element("page_extent");
                        if (extentNode != null && extentNode.Value.IsFilled() && extentNode.Value.ToInt() > 0)
                            book.PageCount = extentNode.Value.ToInt();

                    }
                    db.SubmitChanges();
                }
                catch (Exception e)
                {
                    info.AddMessage(e.Message);
                    info.AddMessage(offer.ToString());
                    info.AddMessage(e.StackTrace);
                    info.Errors++;
                }


            }
            return true;

        }


        #endregion
        #endregion

        #region Импорт книг по требованию

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult OnDemand()
        {
            string lastDate = "";
            StreamReader sr = new StreamReader(Server.MapPath("/Temp/OnDemand/LastDate.txt"));
            lastDate = sr.ReadLine();
            sr.Close();
            return
                View(new DownloadInfo()
                {
                    URL = lastDate,
                    SectionListDownloadInfo = null
                });
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult OnDemand(DownloadInfo model)
        {
            string targetUrl = SiteSetting.Get<string>("OnDemandService");
            WebRequestWrapper wrapper = new WebRequestWrapper(targetUrl);


            StreamReader sr = new StreamReader(Server.MapPath("/Temp/OnDemand/LastDate.txt"));
            string lastDate = sr.ReadLine();
            sr.Close();
            if (string.IsNullOrEmpty(lastDate))
                lastDate = "0000-00-00 00:00:00";

            XDocument request = new XDocument();
            request.Declaration = new XDeclaration("1.0", "UTF-8", "yes");
            request.Add(new XElement("request", new XElement("operation", "get_incremental_update"),
                                     new XElement("last_update_time", lastDate)));


            string tempFile = "/Temp/OnDemand/answer.gz";
            wrapper.PostAndSaveResponse(request.ToString(), tempFile, true);

            var path = Server.CreateDir("/Temp/OnDemand/");
            WebUnzipper unzipper = new WebUnzipper(path, "", Server.MapPath(tempFile));
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View();
            }

            string xlsPath = unzipper.ResultFileName;

            //string xlsPath = "D:\\Sites\\Sprinter\\Sprinter\\Sprinter\\Temp\\OnDemand\\catalog.yml";
            //string xlsPath = "C:\\SPRINTER\\SITE\\Sprinter\\Temp\\OnDemand\\catalog.yml";

            var importList = ParseOnDemandList(xlsPath);

            //инициализируем перед запуском мультипоточности
            var sectionList = OnDemandSectionList;
            //----------------------------------------------

            UpdateCatalog(importList, "Печать по требованию", false, PrepareOnDemand, PostProcessOnDemand, null, true);
            return View(model);
        }

        private void PostProcessOnDemand(int saleid, ImportData args, DownloadInfo dl)
        {
            var db = new DB();
            var sections = (args.Section).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var section in sections)
            {
                var tree = new List<string>() { section };
                string ns = section;
                while (ns.Length > 1)
                {
                    ns = ns.Substring(0, ns.Length - 1);
                    tree.Add(ns);
                }
                int pid = CreateBranch(tree.OrderBy(x => x.Length).ToList());

                var rel = new BookPageRel() { SaleCatalogID = saleid, PageID = pid };
                db.BookPageRels.InsertOnSubmit(rel);
                db.SubmitChanges();
            }

        }

        private int CreateBranch(List<string> itemList)
        {
            int pid = 0;
            var db = new DB();
            var parent = db.CMSPages.FirstOrDefault(x => x.URL == "ondemand");
            if (parent == null)
            {
                var catpage = db.CMSPages.First(x => x.URL == "catalog");
                parent = new CMSPage()
                {
                    PageName = "Печать по требованию",
                    FullName = "Печать по требованию",
                    URL = "ondemand",
                    ParentID = catpage.ID,
                    Type = 1,
                    ViewMenu = true,
                    Visible = true
                };
                db.CMSPages.InsertOnSubmit(parent);
                db.SubmitChanges();
            }
            pid = parent.ID;
            foreach (string item in itemList)
            {
                var pp = db.CMSPages.FirstOrDefault(x => x.SystemData == item);
                if (pp != null)
                    parent = pp;
                else
                {
                    string name = OnDemandSectionList[item];
                    string url = name.Translit().ToLower();
                    CMSPage page = null;
                    try
                    {
                        url = CreateUrl(url, db);
                        page = new CMSPage()
                        {
                            PageName = name,
                            FullName = name,
                            Title = name,
                            Keywords = name,
                            Description = name,
                            ParentID = parent.ID,
                            URL = url,
                            ViewMenu = true,
                            Visible = true,
                            SystemData = item,
                            Type = 1
                        };
                        db.CMSPages.InsertOnSubmit(page);
                        db.SubmitChanges();
                    }
                    catch
                    {
                        try
                        {
                            url = CreateUrl(url, db);
                            page = new CMSPage()
                            {
                                PageName = name,
                                FullName = name,
                                Title = name,
                                Keywords = name,
                                Description = name,
                                ParentID = parent.ID,
                                URL = url,
                                ViewMenu = true,
                                Visible = true,
                                SystemData = item,
                                Type = 1
                            };
                            db.CMSPages.InsertOnSubmit(page);
                            db.SubmitChanges();
                        }
                        catch
                        {
                            return parent.ID;
                        }
                    }
                    parent = page;

                }
                pid = parent.ID;
            }
            return pid;
        }

        private string CreateUrl(string url, DB db)
        {
            if (db.CMSPages.Any(x => x.URL == url))
            {
                for (int i = 1; i < 100; i++)
                {
                    var nurl = url + "_" + i;
                    if (!db.CMSPages.Any(x => x.URL == nurl))
                        return nurl;
                }
            }
            return url;
        }

        private void PrepareOnDemand(ref ImportData data)
        {
            try
            {
                XDocument doc = XDocument.Load(data.Header);
                data.ISBN = EAN13.ClearIsbn(doc.Descendants("IDValue").First().Value);
                long iEan = 0;
                long.TryParse(EAN13.IsbnToEan13(data.ISBN), out iEan);
                if (iEan > 0)
                    data.EAN = iEan;

                data.Header = doc.Descendants("TitleText").First().Value + " " +
                              doc.Descendants("Subtitle").First().Value;

                data.Authors = new List<string>();

                var authors = doc.Descendants("Contributor");
                foreach (var element in authors)
                {
                    data.Authors.Add(element.Descendants("PersonName").First().Value);
                }

                data.PublisherName = doc.Descendants("PublisherName").First().Value;
                data.Year = ImportData.ParseYear(doc.Descendants("PublicationDate").First().Value);
                data.PageCount = (int?)ImportData.ParseInt(doc.Descendants("NumberOfPages").First().Value);
                data.Type = doc.Descendants("ProductFormDescription").First().Value;
                data.Section = string.Join(";",
                                           doc.Descendants("Subject").Where(
                                               x => x.Element("SubjectSchemeIdentifier").Value == "12").Select(
                                                   x => x.Element("SubjectCode").Value));
                data.Description =
                    doc.Descendants("OtherText").Where(x => x.Element("TextTypeCode").Value == "01").Select(
                        x => x.Element("Text")).First().Value;
                data.CoverURL =
                    doc.Descendants("MediaFile").First(x => x.Element("ImageWidth").Value == "200").Element(
                        "MediaFileLink").Value;

            }
            catch { }


        }

        private List<ImportData> ParseOnDemandList(string xlsPath)
        {
            var importList = new List<ImportData>();
            try
            {
                var db = new DB();
                db.BookSaleCatalogs.DeleteAllOnSubmit(db.BookSaleCatalogs.Where(x => x.PartnerID == 16 && !x.BookPageRels.Any()));
                db.SubmitChanges();

                /*
                                var existList =
                                    db.BookSaleCatalogs.Where(x => x.PartnerID == 16).Select(x => x.PartnerUID).ToDictionary(x => x,
                                                                                                                             y => "");
                */

                int counter = 0;
                var relative = new Uri(xlsPath);
                using (XmlReader reader = XmlReader.Create(relative.ToString(),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("response");

                    reader.ReadToDescendant("update_time");
                    foreach (var ut in reader.ReadElements("update_time"))
                    {
                        StreamWriter sw = new StreamWriter(Server.MapPath("/Temp/OnDemand/LastDate.txt"), false);
                        sw.WriteLine(ut.Value);
                        sw.Close();
                    }
                }

                using (XmlReader reader = XmlReader.Create(relative.ToString(),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("response");

                    reader.ReadToDescendant("product");

                    foreach (var offer in reader.ReadElements("product"))
                    {
                        counter++;

                        /*
                                                if (existList.ContainsKey(offer.Element("id").Value))
                                                    continue;
                        */

                        ImportData data = new ImportData();
                        data.PartnerUID = offer.Element("id").Value;
                        data.IsNew = null;
                        data.IsSpec = null;
                        data.IsTop = null;

                        var priceNode = offer.Element("price");
                        if (priceNode != null)
                            data.PartnerPrice = ImportData.ParsePrice(priceNode.Value.Trim());
                        else data.PartnerPrice = 0;

                        var avail = offer.Element("available").Value.Trim() == "1";
                        if (!avail)
                            data.OutOfPrint = true;
                        var descrNode = offer.Element("onixurl");
                        if (descrNode != null) data.Header = descrNode.Value;
                        importList.Add(data);
                    }
                }
            }
            catch
            {

            }
            finally
            {
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }
            }
            return importList.Where(x => x.PartnerPrice > 0).ToList();
        }

        private static Dictionary<string, string> _onDemandSectionList;
        public static Dictionary<string, string> OnDemandSectionList
        {
            get
            {
                if (_onDemandSectionList == null)
                {
                    _onDemandSectionList = new Dictionary<string, string>();
                    Regex rxid = new Regex(@"\([A-Z0-9]+\)");
                    Workbook workbook = null;
                    try
                    {
                        workbook = Workbook.getWorkbook(HostingEnvironment.MapPath("/Temp/OnDemand/BIC.xls"));
                        Sheet sheet = workbook.Sheets[0];
                        for (int i = 0; i < sheet.Rows; i++)
                        {
                            var cells = sheet.getRow(i);
                            foreach (var cell in cells)
                            {
                                if (cell != null && !cell.Contents.IsNullOrEmpty())
                                {
                                    string key =
                                        rxid.Match(cell.Contents).Captures[0].Value.Replace("(", "").Replace(")", "");
                                    string val = rxid.Replace(cell.Contents, "").Trim();
                                    if (!_onDemandSectionList.ContainsKey(key))
                                        _onDemandSectionList.Add(key, val);
                                }
                            }

                        }
                    }
                    catch
                    {

                    }
                    finally
                    {
                        if (workbook != null)
                            workbook.close();
                    }
                }
                return _onDemandSectionList;
            }
        }

        #endregion

        #region Прайс-лист Абриса

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Abris()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "http://textbook.ru/upload/Price_all.xls",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Абрис")
                    });
        }

        protected List<ImportData> ParseAbrisList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {
                string lastSection = "";
                workbook = Workbook.getWorkbook(xlsPath);
                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 9; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    var uidCell = sheet.getCell(0, irow);
                    if (uidCell != null && uidCell.Contents != null)
                        data.PartnerUID = uidCell.Contents.Trim();
                    data.IsNew = null;
                    data.IsSpec = null;
                    data.Header = sheet.getCell(1, irow).Contents.Trim();
                    data.Authors = new List<string>();// ImportData.CreateAuthorsList(sheet.getCell(7, irow).Contents.Trim());
                    data.PublisherName = (sheet.getCell(2, irow).Contents ?? "").Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(4, irow).Contents);
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(12, irow).Contents.Trim());
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(5, irow).Contents);
                    data.Type = sheet.getCell(6, irow).Contents;
                    if (data.PartnerUID.IsNullOrEmpty())
                        lastSection = data.Header;
                    data.Section = lastSection;// sheet.getCell(14, irow).Contents;
                    var sIsbn = sheet.getCell(3, irow).Contents.Trim();
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                    else
                        data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();
                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Abris(DownloadInfo model)
        {
            var path = Server.CreateDir("/Temp/Abris/");
            WebUnzipper unzipper = new WebUnzipper(path, "", model.URL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(model, "Абрис"));

            }
            string xlsPath = unzipper.ResultFileName;
            var importList = ParseAbrisList(xlsPath);
            UpdateCatalog(importList, "Абрис");
            return View(InitSectionList(model, "Абрис"));
        }

        #endregion

        #region Прайс лист Юрайта

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Urait()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "http://urait.ru/price/Files/Archive/Urait-poln.zip",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Urait")
                    });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Urait(DownloadInfo model)
        {
            var path = Server.CreateDir("/Temp/Urait/");
            WebUnzipper unzipper = new WebUnzipper(path, "", model.URL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(model, "Urait"));
            }
            string xlsPath = unzipper.ResultFileName;
            var importList = ParseUraitList(xlsPath);
            UpdateCatalog(importList, "Urait");
            return View(InitSectionList(model, "Urait"));
        }

        protected List<ImportData> ParseUraitList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {

                workbook = Workbook.getWorkbook(xlsPath);
                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 8; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(0, irow).Contents.Trim();
                    data.IsNew = !sheet.getCell(1, irow).Contents.IsNullOrEmpty();
                    data.IsSpec = !sheet.getCell(2, irow).Contents.IsNullOrEmpty();
                    data.Header = sheet.getCell(6, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(sheet.getCell(7, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(8, irow).Contents.Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(9, irow).Contents);
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(10, irow).Contents.Trim());
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(12, irow).Contents);
                    data.Type = sheet.getCell(13, irow).Contents;
                    data.Section = sheet.getCell(14, irow).Contents;
                    var sIsbn = sheet.getCell(15, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    var ean = ImportData.ParseInt(sheet.getCell(16, irow).Contents);
                    if (data.ISBN.IsNullOrEmpty() && !ean.HasValue) data.EAN = 0;
                    else
                        data.EAN = ean.HasValue ? ean.Value : long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);

                }



            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();
                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Кноруса

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Knorus()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "http://www.knorus.ru/upload/price/prc_all_alf.zip",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Кнорус")
                    });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Knorus(DownloadInfo model)
        {
            var path = Server.CreateDir("/Temp/Knorus/");
            WebUnzipper unzipper = new WebUnzipper(path, "", model.URL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(model, "Кнорус"));
            }
            string xlsPath = unzipper.ResultFileName;
            var importList = ParseKnorusList(xlsPath);
            UpdateCatalog(importList, "Кнорус");
            return View(InitSectionList(model, "Кнорус"));
        }

        private List<ImportData> ParseKnorusList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {
                workbook = Workbook.getWorkbook(xlsPath);
                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 9; irow < sheet.Rows; irow++)
                {
                    counter++;
                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(0, irow).Contents.Trim();
                    var attrData = sheet.getCell(1, irow).Contents;
                    if (attrData == null) attrData = "";
                    data.IsNew = attrData.Contains("N");
                    data.IsSpec = attrData.Contains("$");
                    data.IsTop = null;
                    data.Header = sheet.getCell(2, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList((sheet.getCell(4, irow).Contents ?? "").Trim());
                    data.PublisherName = sheet.getCell(5, irow).Contents.Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(9, irow).Contents);

                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(6, irow).Contents.Trim());
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(8, irow).Contents);
                    //data.Type = sheet.getCell(13, irow).Contents;
                    data.Section = "[Нет указано]";
                    var sIsbn = sheet.getCell(10, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                    else
                        data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();
                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Релода

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Relod()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Релод") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Relod(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Relod/");
            string target = "";
            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Релод"));

            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Релод"));

            }
            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseRelodList(xlsPath);
            UpdateCatalog(importList, "Релод");
            return View(InitSectionList(null, "Релод"));
        }

        private List<ImportData> ParseRelodList(string xlsPath, bool isSpec = false)
        {
            #region Заменяемые слова
            List<string> forReplace = new List<string>()
                {
                    "SB",
                    "PB",
                    "CB",
                    "IF",
                    "LB",
                    "WB",
                    "AB",
                    "PB",
                    "DWK",
                    "PF",
                    "PSB",
                    "HSB",
                    "WB W/K",
                    "WB Wo/K",
                    "TB",
                    "TBk, IM",
                    "TRP",
                    "TRB",
                    "ARP",
                    "TM",
                    "TG",
                    "TN",
                    "TE",
                    "BRP",
                    "HB",
                    "NB",
                    "CL Cass",
                    "cass",
                    "ST Cass",
                    "WB Cass",
                    "CL CD",
                    "ST CD",
                    "1K7",
                    "SS",
                    "AnsB",
                    "VHS PAL",
                    "VG",
                    "TG",
                    "L&S",
                    "Split Ed",
                    "GPB",
                    "R&W",
                    "SPW",
                    "AB",
                    "M KIT",
                    "Wless Ed",
                    "Thumb Ed",
                    "HB",
                    "S",
                    "PB",
                    "Ppb",
                    "FLEXI",
                    "OP!",
                    "ОР!",
                    "*",
                    "#",
                    "AB",
                    "AQA",
                    "BC",
                    "BEC",
                    "BEG",
                    "BULATS",
                    "C.",
                    "CAE",
                    "CAL",
                    "CDR",
                    "CEC",
                    "CER",
                    "CFC",
                    "CHLT",
                    "CL",
                    "CLA",
                    "CLD",
                    "CLTL",
                    "CMD",
                    "CPE",
                    "CPT",
                    "CS",
                    "CT",
                    "CTL",
                    "CTTD",
                    "CYLET",
                    "DALF",
                    "DC",
                    "De",
                    "De L&U",
                    "DEL",
                    "DELF",
                    "DILF",
                    "DK",
                    "DKR",
                    "DLE",
                    "E.",
                    "ED:",
                    "EDT:",
                    "ELR OSA:",
                    "ELR:",
                    "ELT",
                    "Es",
                    "Es LyA",
                    "ESOL",
                    "ET:",
                    "ETR",
                    "EU",
                    "FC",
                    "FCE",
                    "FD",
                    "FFT",
                    "FLD",
                    "Fr",
                    "Fr CT",
                    "Fr FaL",
                    "Fr LeS'E",
                    "Fr LeV",
                    "Fr XX",
                    "FT",
                    "FTV",
                    "GA",
                    "GCSE",
                    "GD",
                    "GL",
                    "HOTPUZZ",
                    "IB",
                    "IBD",
                    "ICT",
                    "ID",
                    "IELTS",
                    "IGCSE",
                    "Int.with Litr",
                    "It",
                    "It Cl",
                    "It IL",
                    "KET",
                    "L",
                    "LA",
                    "Las",
                    "LBB",
                    "LC",
                    "LDOCE",
                    "LF",
                    "LFF",
                    "LHLT",
                    "LN",
                    "LT",
                    "LV",
                    "M",
                    "M Bl",
                    "MCB",
                    "MEL",
                    "MHDW",
                    "NA",
                    "NCSS",
                    "NLFF",
                    "NPS",
                    "OAL",
                    "OBC",
                    "OBF",
                    "OBL",
                    "OBP",
                    "OBS",
                    "OCC",
                    "OGR",
                    "OHFT",
                    "OILS",
                    "OL",
                    "OMP",
                    "OPD",
                    "OPER",
                    "OPR",
                    "ORT",
                    "ORT FF",
                    "ORT PL",
                    "OS",
                    "OSD",
                    "OSR",
                    "OSS",
                    "OSSP",
                    "OST",
                    "OSTL",
                    "OTL",
                    "OWC",
                    "OXED",
                    "P",
                    "PC",
                    "PCP",
                    "PD",
                    "PE",
                    "PeD",
                    "PEG",
                    "PET",
                    "PMC",
                    "PPC",
                    "PR",
                    "PS",
                    "PSN",
                    "QD",
                    "RBFT",
                    "RC",
                    "RD",
                    "RDC",
                    "RG",
                    "RH",
                    "RWL",
                    "SC TEACH ED",
                    "SD",
                    "SEV",
                    "SFC",
                    "SMP",
                    "SWER",
                    "TDC",
                    "TdC",
                    "TKT",
                    "TOEFL",
                    "TOEIC",
                    "TTESL",
                    "VIP",
                    "VSI",
                    "YLET",
                    "CD"



                };
            #endregion
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {
                workbook = Workbook.getWorkbook(xlsPath);
                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 1; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(0, irow).Contents.Trim();
                    data.IsNew = null;
                    data.IsSpec = null;
                    data.IsTop = null;
                    data.Header = sheet.getCell(1, irow).Contents.Trim() + " ";
                    data.OutOfPrint = data.Header.Contains("OP!") || data.Header.Contains("ОР!");
                    data.Header = data.Header.Replace("NA!", "");
                    foreach (var replace in forReplace)
                    {
                        if (replace == "CD" && data.Header.Trim().EndsWith("CD")) continue;
                        if (data.Header.Contains(" " + replace + " "))
                            data.Header = data.Header.Replace(" " + replace + " ", " ");
                    }
                    data.Header =
                        data.Header.Trim().Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").
                            Replace("  ", " ");
                    if (data.Header.EndsWith("*") || data.Header.EndsWith("#") || data.Header.EndsWith("$") || data.Header.EndsWith("+")) data.Header = data.Header.Substring(0, data.Header.Length - 1).Trim();
                    if (data.Header.EndsWith("*") || data.Header.EndsWith("#") || data.Header.EndsWith("$") || data.Header.EndsWith("+")) data.Header = data.Header.Substring(0, data.Header.Length - 1).Trim();
                    if (data.Header.EndsWith("*") || data.Header.EndsWith("#") || data.Header.EndsWith("$") || data.Header.EndsWith("+")) data.Header = data.Header.Substring(0, data.Header.Length - 1).Trim();
                    if (data.Header.EndsWith("*") || data.Header.EndsWith("#") || data.Header.EndsWith("$") || data.Header.EndsWith("+")) data.Header = data.Header.Substring(0, data.Header.Length - 1).Trim();

                    data.Authors = ImportData.CreateAuthorsList("");
                    data.PublisherName = sheet.getCell(3, irow).Contents.Trim();
                    //data.Year = ImportData.ParseYear(sheet.getCell(9, irow).Contents);

                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(2, irow).Contents.Trim());
                    //data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(8, irow).Contents);
                    //data.Type = sheet.getCell(13, irow).Contents;
                    data.Section = sheet.getCell(3, irow).Contents;
                    var sIsbn = sheet.getCell(0, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                    else
                        data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();
                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Логосферы

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Logosfera()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "http://www.logobook.ru/FILES/yandex_list.xml",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Логосфера")
                    });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Logosfera(DownloadInfo model)
        {
            var path = Server.CreateDir("/Temp/Logosfera/");

            WebUnzipper unzipper = new WebUnzipper(path, "", model.URL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(model, "Логосфера"));

            }

            string xlsPath = unzipper.ResultFileName;
            var importList = ParseLogosferaList(xlsPath);
            UpdateCatalog(importList, "Логосфера");
            return View(InitSectionList(model, "Логосфера"));
        }

        private string CreatePath(Dictionary<int, string> catList, Dictionary<int, int> parentList, int catId)
        {
            string path = catList[catId];
            int parent = catId;
            while (parent > 0)
            {
                if (parentList.ContainsKey(parent))
                {
                    parent = parentList[parent];
                    path += " ->> ";
                    path += catList[parent];
                }
                else
                {
                    parent = 0;
                }
            }
            return string.Join(" ->> ",
                               path.Split(new[] { " ->> " }, StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray());
        }

        private List<ImportData> ParseLogosferaList(string xlsPath, bool isSpec = false)
        {
            var importList = new List<ImportData>();
            try
            {
                int counter = 0;
                var relative = new Uri(xlsPath);
                var sectionList = new Dictionary<int, string>();
                var parentList = new Dictionary<int, int>();

                using (XmlReader reader = XmlReader.Create(relative.ToString(),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("yml_catalog");

                    reader.ReadToDescendant("category");
                    foreach (var cat in reader.ReadElements("category"))
                    {
                        if (!sectionList.ContainsKey(cat.Attribute("id").Value.ToInt()))
                            sectionList.Add(cat.Attribute("id").Value.ToInt(), cat.Value);
                        var pid = cat.Attribute("parentId");
                        if (pid != null)
                        {
                            if (!parentList.ContainsKey(cat.Attribute("id").Value.ToInt()))
                                parentList.Add(cat.Attribute("id").Value.ToInt(),
                                               cat.Attribute("parentId").Value.ToInt());
                        }
                    }
                }

                using (XmlReader reader = XmlReader.Create(relative.ToString(),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("yml_catalog");

                    reader.ReadToDescendant("offer");

                    foreach (var offer in reader.ReadElements("offer"))
                    {
                        counter++;

                        ImportData data = new ImportData();
                        data.PartnerUID = offer.Attribute("id").Value;
                        data.IsNew = null;
                        data.IsSpec = null;
                        data.IsTop = null;

                        data.PartnerPrice = ImportData.ParsePrice(offer.Element("price").Value.Trim());

                        var pictElement = offer.Element("picture");
                        if (pictElement != null) data.CoverURL = pictElement.Value;
                        var authorsElement = offer.Element("author");
                        if (authorsElement != null)
                            data.Authors = ImportData.CreateAuthorsList(authorsElement.Value);
                        else data.Authors = new List<string>();

                        data.Header = offer.Element("name").Value;

                        data.PublisherName = offer.Element("publisher").Value;
                        var yearElement = offer.Element("year");
                        if (yearElement != null)
                            data.Year = ImportData.ParseYear(yearElement.Value);
                        data.Section = CreatePath(sectionList, parentList, offer.Element("categoryId").Value.ToInt());
                        //sectionList[offer.Element("categoryId").Value.ToInt()];
                        var sIsbn = offer.Element("ISBN").Value;
                        data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                        if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                        else
                            data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
                        var descrElement = offer.Element("description");
                        if (descrElement != null) data.Description = descrElement.Value;
                        importList.Add(data);

                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Эксмо

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Eksmo()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Эксмо") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Eksmo(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Eksmo/");
            string target = "";

            if (file != null && file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Эксмо"));

            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Эксмо"));

            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseEksmoList(xlsPath);
            UpdateCatalog(importList, "Эксмо");
            return View(InitSectionList(null, "Эксмо"));
        }

        private List<ImportData> ParseEksmoList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {

                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 17; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(21, irow).Contents.Trim();
                    data.IsNew = !sheet.getCell(20, irow).Contents.IsNullOrEmpty();
                    data.IsSpec = null;// !sheet.getCell(2, irow).Contents.IsNullOrEmpty();
                    data.Header = sheet.getCell(3, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(sheet.getCell(4, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(8, irow).Contents.Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(14, irow).Contents);
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(2, irow).Contents.Trim());
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(16, irow).Contents);
                    data.Type = sheet.getCell(18, irow).Contents;
                    data.Section = sheet.getCell(7, irow).Contents;
                    var sIsbn = sheet.getCell(9, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    var ean = ImportData.ParseInt(sheet.getCell(15, irow).Contents);
                    if (data.ISBN.IsNullOrEmpty() && !ean.HasValue) data.EAN = 0;
                    else
                        data.EAN = ean.HasValue ? ean.Value : long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Британии

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Britania()
        {
            return View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Британия") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Britania(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Britania/");
            string target = "";

            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Британия"));

            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Британия"));

            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseBritaniaList(xlsPath);
            UpdateCatalog(importList, "Британия");
            return View(InitSectionList(null, "Британия"));
        }

        private List<ImportData> ParseBritaniaList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {

                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 1; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(1, irow).Contents.Trim();
                    data.IsNew = null;
                    data.IsSpec = null;
                    data.IsTop = null;

                    data.Header = sheet.getCell(4, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(sheet.getCell(12, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(7, irow).Contents.Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(8, irow).Contents);
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(6, irow).Contents.Trim());


                    data.Section = sheet.getCell(13, irow).Contents;
                    var sIsbn = sheet.getCell(2, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                    else
                        data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист ИП Яковлев

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Yakovleff()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "ИП Яковлев") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Yakovleff(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Yakovleff/");
            string target = "";

            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "ИП Яковлев"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "ИП Яковлев"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseYakovleffList(xlsPath);
            UpdateCatalog(importList, "ИП Яковлев");
            return View(InitSectionList(null, "ИП Яковлев"));
        }

        private List<ImportData> ParseYakovleffList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            Regex rxHead = new Regex(@"\(.+\)");
            try
            {

                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 13; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(7, irow).Contents.Trim();
                    data.IsNew = null;
                    data.IsSpec = null;
                    data.IsTop = null;

                    data.Header = sheet.getCell(1, irow).Contents.Trim();
                    data.Header = rxHead.Replace(data.Header, "").Replace("покет", "").Replace("эконом", "").Trim();
                    data.Authors = ImportData.CreateAuthorsList((sheet.getCell(0, irow).Contents ?? "").Trim());
                    data.PublisherName = sheet.getCell(2, irow).Contents.Trim();
                    //data.Year = ImportData.ParseYear(sheet.getCell(8, irow).Contents);
                    data.PartnerPrice = ImportData.ParsePrice((sheet.getCell(4, irow).Contents ?? "").Trim());


                    data.Section = sheet.getCell(6, irow).Contents.Trim();
                    var sIsbn = sheet.getCell(8, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                    else
                        data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
                    importList.Add(data);
                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Белый Город

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult WhiteTown()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Белый город") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult WhiteTown(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/WhiteTown/");
            string target = "";

            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Белый город"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Белый город"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseWhitetownList(xlsPath);
            UpdateCatalog(importList,
                          "Белый город", false, PrepareWhiteTown);

            return View(InitSectionList(null, "Белый город"));
        }

        private List<ImportData> ParseWhitetownList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {

                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 5; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(1, irow).Contents.Trim();
                    data.IsNew = null;
                    data.IsSpec = null;
                    data.IsTop = null;

                    data.Header = sheet.getCell(0, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList((sheet.getCell(3, irow).Contents ?? "").Replace("--", "").Trim());
                    data.PublisherName = sheet.getCell(6, irow).Contents.Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(5, irow).Contents);
                    data.PartnerPrice = ImportData.ParsePrice((sheet.getCell(15, irow).Contents ?? "").Trim());
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(9, irow).Contents);
                    data.Type = sheet.getCell(14, irow).Contents;
                    data.Section = sheet.getCell(7, irow).Contents;
                    var sIsbn = sheet.getCell(12, irow).Contents;
                    data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                    if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                    else
                        data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));



                    importList.Add(data);


                }


            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        private void PrepareWhiteTown(ref ImportData data)
        {
            var db = new DB();
            var partenrUID = data.PartnerUID;
            var bsc =
                db.BookSaleCatalogs.FirstOrDefault(x => x.PartnerUID == partenrUID && x.Partner.Name == "Белый город");
            data.CoverURL = "http://www.belygorod.ru/b/N{0}.jpg".FormatWith(data.PartnerUID);
            if (bsc == null || bsc.BookDescriptionCatalog == null || bsc.BookDescriptionCatalog.Annotation.IsNullOrEmpty())
            {

                Regex descrEx = new Regex(@"<div id=""tab1"" class=""tab_content"">(.+)?</div>(.+)<div id=""tab2"" class=""tab_content"">", RegexOptions.Multiline);
                string URL = "http://www.belygorod.ru/catalog/N{0}/".FormatWith(data.PartnerUID);
                DescriptionParser parser = new DescriptionParser()
                {
                    URL = URL,
                    FieldList =
                        new List<DescriptionParserData>
                                    {
                                        new DescriptionParserData()
                                            {
                                                Expr = descrEx,
                                                CaptureNum = 0,
                                                GroupNum = 1,
                                                FieldName = "Description",
                                            }
                                    }
                };

                parser.TryLoadDescription(ref data);
                if (data.Description.IsFilled() && data.Description.Contains("<script"))
                    data.Description = data.Description.Substring(0, data.Description.IndexOf("<script"));
                data.Description = data.Description.ClearHTML();

            }
        }

        #endregion

        #region Прайс лист 36,6

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Thirty()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "36,6") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Thirty(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/36_6/");
            string target = "";

            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "36,6"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "36,6"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseThirtyList(xlsPath);
            UpdateCatalog(importList, "36,6");
            return View(InitSectionList(null, "36,6"));
        }

        private List<ImportData> ParseThirtyList(string xlsPath, bool isSpec = false)
        {
            var importList = new List<ImportData>();
            try
            {
                string constr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + Path.GetDirectoryName(xlsPath) + ";Extended Properties=DBASE III";
                using (OleDbConnection con = new OleDbConnection(constr))
                {
                    var sql = "select * from " + Path.GetFileName(xlsPath);
                    OleDbCommand cmd = new OleDbCommand(sql, con);
                    con.Open();
                    DataSet ds = new DataSet();

                    OleDbDataAdapter da = new OleDbDataAdapter(cmd);
                    da.Fill(ds);
                    if (ds.Tables.Count > 0)
                    {
                        DataTable source = ds.Tables[0];


                        foreach (DataRow row in source.Rows)
                        {
                            ImportData data = new ImportData();
                            data.PartnerUID = row[0].ToString().Trim();
                            var sIsbn = row[1].ToString();
                            data.ISBN = sIsbn.IsNullOrEmpty() ? "" : EAN13.ClearIsbn(sIsbn);
                            if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
                            else
                                data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));

                            data.Section = row[2].ToString();
                            data.Authors = ImportData.CreateAuthorsList(row[3].ToString().Replace("--", "").Trim());
                            data.Header = row[4].ToString();

                            data.IsNew = null;
                            data.IsSpec = null;
                            data.IsTop = null;



                            data.PublisherName = row[8].ToString().Trim();
                            data.Year = ImportData.ParseYear(row[10].ToString());
                            data.PageCount = (int?)ImportData.ParseInt(row[11].ToString());
                            data.PartnerPrice = ImportData.ParsePrice(row[17].ToString().Trim());

                            data.Type = row[13].ToString().Contains("x") || row[13].ToString().Contains("х")
                                            ? row[13].ToString()
                                            : "";

                            importList.Add(data);

                        }

                    }
                }



            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Лабиринта

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Labirint()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Лабиринт") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Labirint(HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Labirint/");
            string target = "";

            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Лабиринт"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Лабиринт"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseLabirintList(xlsPath);
            UpdateCatalog(importList, "Лабиринт", true, PrepareLabirintCatalog);
            return View(InitSectionList(null, "Лабиринт"));
        }

        private List<ImportData> ParseLabirintList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {


                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;

                for (int irow = 11; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(0, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(sheet.getCell(2, irow).Contents.Trim());

                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(5, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(7, irow).Contents.Trim();
                    data.Year = ImportData.ParseYear(sheet.getCell(9, irow).Contents);
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(10, irow).Contents);
                    data.Type = sheet.getCell(12, irow).Contents;



                    data.Header = sheet.getCell(3, irow).Contents.Trim();

                    var backColor = sheet.getCell(0, irow).CellFormat.BackgroundColour;
                    data.IsNew = backColor != NExcel.Format.Colour.WHITE &&
                                 backColor != NExcel.Format.Colour.UNKNOWN &&
                                 backColor != NExcel.Format.Colour.DEFAULT_BACKGROUND &&
                                 backColor != NExcel.Format.Colour.DEFAULT_BACKGROUND1;
                    data.IsSpec = null;
                    var font = sheet.getCell(0, irow).CellFormat.Font;
                    data.IsTop = font.BoldWeight == 700 && !data.IsNew.Value;
                    data.Section = sheet.getCell(8, irow).Contents.Trim();

                    importList.Add(data);

                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        private void PrepareLabirintCatalog(ref ImportData data)
        {
            Regex isbnEx = new Regex(@"<div class=""isbn smallbr"">ISBN: ([\d-Xx]+)?</div>");
            Regex headEx = new Regex(@"<h1>(.+)?</h1>");
            Regex descrEx = new Regex(@"<noindex><p>(.+)?</p>", RegexOptions.Multiline);
            Regex descrExAlter = new Regex(@"</h2><p>(.+)?</p>", RegexOptions.Multiline);
            Regex descrExAdd = new Regex(@"<p><noindex>(.+)?</noindex>", RegexOptions.Multiline);
            string URL = "http://www.labirint.ru/books/{0}/".FormatWith(data.PartnerUID);
            DescriptionParser parser = new DescriptionParser()
            {
                URL = URL,
                FieldList =
                    new List<DescriptionParserData>
                                    {
                                        new DescriptionParserData
                                            {Expr = headEx, CaptureNum = 0, GroupNum = 1, FieldName = "Header"},
                                        new DescriptionParserData
                                            {Expr = isbnEx, CaptureNum = 0, GroupNum = 1, FieldName = "ISBN"},
                                        new DescriptionParserData()
                                            {
                                                Expr = descrEx,
                                                CaptureNum = 0,
                                                GroupNum = 1,
                                                FieldName = "Description",
                                                AlterExpr = descrExAlter,
                                                AddExpr = descrExAdd
                                            }
                                    }
            };

            parser.TryLoadDescription(ref data);
            if (data.ISBN.IsNullOrEmpty()) data.ISBN = "";
            data.Description = data.Description.ClearHTML();
            if (data.ISBN.IsNullOrEmpty()) data.EAN = 0;
            else
                data.EAN = long.Parse(EAN13.IsbnToEan13(data.ISBN));
        }

        #endregion

        #region Прайс лист Глобуса

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Globus()
        {
            return
                View(new DownloadInfo() { URL = "", SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Глобус") });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Globus(HttpPostedFileBase file, bool isSpec)
        {
            var path = Server.CreateDir("/Temp/Globus/");
            string target = "";
            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Глобус"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Глобус"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseGlobusList(xlsPath, isSpec);
            UpdateCatalog(importList, "Глобус", true);
            return View(InitSectionList(null, "Глобус"));
        }

        private List<ImportData> ParseGlobusList(string xlsPath, bool isSpec)
        {
            Workbook workbook = null;
            Regex rxHead = new Regex(@"\(.+?\)");
            var importList = new List<ImportData>();
            try
            {
                Regex rx = new Regex(@"[A-ZА-Я]{1}[a-zа-я]{1}[\w\d\W]+");
                var lastSection = "";
                var preLastSection = "";
                int preLastSectionIndex = 0;
                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;

                for (int irow = 8; irow < sheet.Rows; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(1, irow).Contents.Trim();
                    var authList = sheet.getCell(2, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(authList);

                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(isSpec ? 11 : 10, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(4, irow).Contents.Trim();
                    var sYear = sheet.getCell(7, irow).Contents;
                    if (sYear.Contains("-")) sYear = sYear.Split(new[] { '-' })[1];
                    data.Year = ImportData.ParseYear(sYear);
                    data.PageCount = null;

                    data.Header = sheet.getCell(3, irow).Contents.Trim();

                    if (data.Header.IsNullOrEmpty())
                    {
                        var section = rx.Replace(sheet.getCell(2, irow).Contents.Trim(), "");
                        if (section.IsNullOrEmpty() || section.EndsWith("\"") || section.EndsWith("(")) section = sheet.getCell(2, irow).Contents.Trim();
                        var nextHead = "";
                        if (sheet.Rows > irow + 1)
                        {
                            nextHead = sheet.getCell(3, irow + 1).Contents.Trim();
                            if (nextHead.IsNullOrEmpty())
                            {
                                preLastSection = section;
                            }
                        }
                        lastSection = section;

                    }
                    if (preLastSection == lastSection)
                        data.Section = lastSection;
                    else
                        data.Section = preLastSection + " ->> " + lastSection;
                    data.Header = rxHead.Replace(data.Header, "").Trim();
                    if (authList.IsFilled())
                        data.Header = data.Header.Replace(authList, "").Trim();

                    if (data.Header.Contains("/"))
                    {
                        data.Header = data.Header.Substring(0, data.Header.IndexOf("/")).Trim();

                    }
                    if (data.Header.Contains("арт"))
                    {
                        data.Header = data.Header.Substring(0, data.Header.IndexOf("арт")).Trim();

                    }


                    data.IsSpec = isSpec ? (bool?)true : null;
                    data.IsTop = null;
                    data.IsNew = null;


                    data.ISBN = EAN13.ClearIsbn(sheet.getCell(5, irow).Contents);
                    data.EAN = data.ISBN.IsNullOrEmpty() ? 0 : long.Parse(EAN13.IsbnToEan13(data.ISBN));

                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Учкниги

        private DownloadInfo InitSectionList(DownloadInfo model, string partnerName)
        {
            if (model == null) model = new DownloadInfo() { URL = "" };
            model.SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, partnerName);
            return model;
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult UchKniga()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "http://www.book-online.ru/zip1/biblprice/bibl_price_csv.zip",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "УЧКНИГА")
                    });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult UchKniga(DownloadInfo model)
        {
            var path = Server.CreateDir("/Temp/UchKniga/");

            WebUnzipper unzipper = new WebUnzipper(path, "", model.URL);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(model, "УЧКНИГА"));
            }
            string xlsPath = unzipper.ResultFileName;
            var importList = ParseUchknigaList(xlsPath);
            UpdateCatalog(importList, "УЧКНИГА");

            return View(InitSectionList(model, "УЧКНИГА"));
        }

        private List<ImportData> ParseUchknigaList(string xlsPath, bool isSpec = false)
        {
            CSVParser sheet = new CSVParser(xlsPath, Encoding.GetEncoding(1251));
            var importList = new List<ImportData>();
            Regex rx = new Regex(@"[\[;\d\]]");
            try
            {
                sheet.ParseDocument(1);

                int counter = 0;
                for (int irow = 0; irow < sheet.Count; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(0, irow).Contents.Trim();
                    data.Header = sheet.getCell(1, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(sheet.getCell(3, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(4, irow).Contents.Trim();
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(5, irow).Contents.Trim());
                    data.Year = ImportData.ParseYear(sheet.getCell(6, irow).Contents);

                    data.ISBN = sheet.getCell(7, irow).Contents;
                    var ean = ImportData.ParseInt(sheet.getCell(10, irow).Contents);
                    if (data.ISBN.IsNullOrEmpty() && !ean.HasValue) data.EAN = 0;
                    else
                        data.EAN = ean.HasValue ? ean.Value : long.Parse(EAN13.IsbnToEan13(data.ISBN));

                    data.Type = sheet.getCell(19, irow).Contents;
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(16, irow).Contents);
                    data.Section = rx.Replace(sheet.getCell(24, irow).Contents, "").Trim();

                    data.IsNew = null; //!sheet.getCell(1, irow).Contents.IsNullOrEmpty();
                    data.IsSpec = null; //!sheet.getCell(2, irow).Contents.IsNullOrEmpty();

                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }

            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Учкниги (Игрушки)


        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Toys()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "http://www.book-online.ru/zip1/biblprice/bibl_price_csv.zip",
                        AdditionalPath = "/Temp/Toys/Images/",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "Игрушки")
                    });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Toys(DownloadInfo model, HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Toys/");
            string target = "";
            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "Игрушки"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "Игрушки"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseToysList(xlsPath);
            UpdateCatalog(importList, "Игрушки", false, null, PostProcessToys, model);

            return View(InitSectionList(model, "Игрушки"));
        }

        private void PostProcessToys(int saleid, ImportData args, DownloadInfo dl)
        {
            var db = new DB();
            var item = db.BookSaleCatalogs.FirstOrDefault(x => x.ID == saleid);
            if (item == null) return;
            if (!item.BookDescriptionCatalog.CoverID.HasValue)
            {
                var file = Path.Combine(HostingEnvironment.MapPath(dl.AdditionalPath) ?? "", item.PartnerUID + ".jpg");

                if (System.IO.File.Exists(file))
                {
                    FileStream fs = new FileStream(file, FileMode.Open);
                    byte[] buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, (int)fs.Length);

                    MemoryStream ms = new MemoryStream(buffer);
                    Image bitmap = Image.FromStream(ms);

                    BookCover cover = new BookCover
                    {
                        Data = buffer,
                        Name = "",
                        Height = bitmap.Height,
                        Width = bitmap.Width,
                    };

                    db.BookCovers.InsertOnSubmit(cover);
                    item.BookDescriptionCatalog.BookCover = cover;
                    db.SubmitChanges();
                }
            }
        }

        private List<ImportData> ParseToysList(string xlsPath, bool isSpec = false)
        {
            CSVParser sheet = new CSVParser(xlsPath, Encoding.GetEncoding(1251));
            var importList = new List<ImportData>();
            try
            {
                sheet.ParseDocument(1);

                int counter = 0;
                for (int irow = 0; irow < sheet.Count; irow++)
                {
                    counter++;

                    ImportData data = new ImportData();
                    data.PartnerUID = sheet.getCell(0, irow).Contents.Trim();
                    data.Header = sheet.getCell(2, irow).Contents.Trim();
                    data.Authors = new List<string>(); //ImportData.CreateAuthorsList(sheet.getCell(3, irow).Contents.Trim());
                    data.PublisherName = sheet.getCell(3, irow).Contents.Trim();
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(10, irow).Contents.Trim());

                    data.ISBN = EAN13.ClearIsbn(sheet.getCell(4, irow).Contents);
                    var ean = ImportData.ParseInt(sheet.getCell(21, irow).Contents);
                    if (data.ISBN.IsNullOrEmpty() && !ean.HasValue) data.EAN = 0;
                    else
                        data.EAN = ean.HasValue ? ean.Value : long.Parse(EAN13.IsbnToEan13(data.ISBN));

                    data.Type = sheet.getCell(16, irow).Contents;
                    data.Year = ImportData.ParseYear(sheet.getCell(18, irow).Contents);

                    var weight = ImportData.ParsePrice(sheet.getCell(17, irow).Contents);
                    if (weight > 0)
                        data.PageCount = (int?)(weight * 500);

                    data.Section = sheet.getCell(26, irow).Contents;
                    var sub = sheet.getCell(28, irow).Contents;
                    if (!sub.IsNullOrEmpty())
                        data.Section += string.Format(" ->> {0}", sub);

                    if (data.Section.IsNullOrEmpty())
                        data.Section = "Игрушки";

                    data.Description = sheet.getCell(22, irow).Contents;

                    data.IsNew = null; //!sheet.getCell(1, irow).Contents.IsNullOrEmpty();
                    data.IsSpec = null; //!sheet.getCell(2, irow).Contents.IsNullOrEmpty();

                    importList.Add(data);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }

            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Прайс лист Канцелярии


        [HttpGet]
        [AuthorizeMaster]
        public ActionResult ServiceTorg()
        {
            return
                View(new DownloadInfo()
                    {
                        URL = "",
                        AdditionalPath = "/Temp/ServiceTorg/Images/",
                        SectionListDownloadInfo = new SectionListDownloadInfo(ProviderImportData, "СервисТорг")
                    });
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult ServiceTorg(DownloadInfo model, HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/ServiceTorg/");
            string target = "";
            if (file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                return View(InitSectionList(null, "СервисТорг"));
            }

            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(null, "СервисТорг"));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }
            var importList = ParseServiceTorgList(xlsPath);
            UpdateCatalog(importList, "СервисТорг", false, null, PostProcessSeriveTorg, model);

            return View(InitSectionList(model, "СервисТорг"));
        }

        private void PostProcessSeriveTorg(int saleid, ImportData args, DownloadInfo dl)
        {
            PostProcessToys(saleid, args, dl);
            if (args.FullSectionInfo != null)
            {
                var db = new DB();
                var dbSale = db.BookSaleCatalogs.First(x => x.ID == saleid);
                if (!dbSale.BookPageRels.Any())
                {
                    var page = db.CMSPages.FirstOrDefault(x => x.SystemData == args.FullSectionInfo.UID);
                    if (page != null)
                    {
                        var rel = new BookPageRel() { SaleCatalogID = saleid, CMSPage = page };
                        db.BookPageRels.InsertOnSubmit(rel);
                        db.SubmitChanges();
                    }
                }
            }

        }

        private List<ImportData> ParseServiceTorgList(string xlsPath, bool isSpec = false)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            ExtendedSectionInfo parentSection = null;
            ExtendedSectionInfo section = null;
            try
            {
                workbook = Workbook.getWorkbook(xlsPath);
                var sheet = workbook.Sheets[0];
                int counter = 0;
                for (int irow = 2; irow < sheet.Rows; irow++)
                {
                    counter++;

                    var uid = sheet.getCell(1, irow);
                    if (uid == null || uid.Contents.IsNullOrEmpty())
                    {
                        //типо новый раздел
                        var name = sheet.getCell(2, irow);
                        if (name == null || name.Contents.IsNullOrEmpty())
                            continue;
                        var spaceIndex = name.Contents.Trim().IndexOf(" ", 0);
                        if (spaceIndex < 0) continue;
                        var sectionID = name.Contents.Trim().Substring(0, spaceIndex);
                        var sectionName = name.Contents.Trim().Substring(spaceIndex + 1).Trim();
                        if (!sectionID.Contains("."))
                        {
                            //главный раздел
                            parentSection = new ExtendedSectionInfo() { Name = sectionName, UID = sectionID, Parent = null };
                        }
                        else
                        {
                            section = new ExtendedSectionInfo() { Name = sectionName, UID = sectionID, Parent = parentSection };
                        }

                    }
                    else
                    {
                        ImportData data = new ImportData();
                        data.PartnerUID = uid.Contents.Trim();
                        data.Header = sheet.getCell(3, irow).Contents.Trim();
                        data.Authors = new List<string>();
                        //ImportData.CreateAuthorsList(sheet.getCell(3, irow).Contents.Trim());
                        data.PublisherName =
                            sheet.getCell(4, irow).Contents.Trim().Replace("[]", "").Replace("<>", "").Replace("()", "");
                        data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(8, irow).Contents.Trim().Replace(" ", ""));

                        data.ISBN = "";
                        data.EAN = 0;


                        var count = sheet.getCell(9, irow).Contents;
                        if (count.Contains("/"))
                        {
                            count = count.Substring(0, count.IndexOf("/"));
                        }
                        int iCount = 0;
                        if (int.TryParse(count, out iCount))
                        {
                            data.PartnerPrice = data.PartnerPrice * iCount;
                        }
                        data.FullSectionInfo = section ?? parentSection;

                        data.Section = data.FullSectionInfo == null ? "[Не определено]" : data.FullSectionInfo.ToString();

                        data.Description = null;

                        data.IsNew = !sheet.getCell(0, irow).Contents.IsNullOrEmpty();
                        data.IsSpec = null; //!sheet.getCell(2, irow).Contents.IsNullOrEmpty();

                        importList.Add(data);
                    }
                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception) { }

            }
            #region Создание подразделов

            /*            //создадим в первый раз структуру подразделов
            DB db = new DB();
            var parent = CMSPage.FullPageTable.FirstOrDefault(x => x.URL == "stationery");
            if (parent == null)
            {
                parent = new CMSPage()
                    {
                        AllCount = 0,
                        ActiveCount = 0,
                        URL = "stationery",
                        PageName = "Канцелярские товары",
                        FullName = "Канцелярские товары",
                        Type = 1,
                        Title = "Канцелярские товары",
                        Description = "Канцелярские товары",
                        Keywords = "Канцелярские товары",
                        Visible = true,
                        ViewMenu = false,
                        ParentID = CMSPage.FullPageTable.First(x => x.URL == "catalog").ID
                    };
                db.CMSPages.InsertOnSubmit(parent);
                db.SubmitChanges();
            }
            var sections =
                importList.Select(x => x.FullSectionInfo).Union(
                    importList.Select(x => x.FullSectionInfo).Where(x => x.Parent != null).Select(x => x.Parent)).
                    Distinct().OrderBy(x => x.Parent == null ? 10 : 20).ToList();

            foreach (ExtendedSectionInfo info in sections)
            {
                int pid = 0;
                if (info.Parent == null)
                    pid = parent.ID;
                else
                {
                    pid = db.CMSPages.FirstOrDefault(x => x.SystemData == info.Parent.UID).ID;
                }
                var exist = db.CMSPages.FirstOrDefault(x => x.SystemData == info.UID);
                if (exist == null)
                {
                    exist = new CMSPage()
                    {
                        AllCount = 0,
                        ActiveCount = 0,
                        URL = info.Name.Translit().ToLower(),
                        PageName = info.Name,
                        FullName = info.Name,
                        Type = 1,
                        Title = info.Name,
                        Description = info.Name,
                        Keywords = info.Name,
                        Visible = true,
                        ViewMenu = false,
                        ParentID = pid,
                        SystemData = info.UID
                    };
                    db.CMSPages.InsertOnSubmit(exist);
                    try
                    {

                        db.SubmitChanges();
                    }
                    catch
                    {

                        exist.URL = info.Name.Translit().ToLower() + "_1";
                        try
                        {
                            db.SubmitChanges();
                        }
                        catch
                        {
                            exist.URL = info.Name.Translit().ToLower() + "_2";

                            try
                            {
                                db.SubmitChanges();
                            }
                            catch
                            {
                                exist.URL = info.Name.Translit().ToLower() + "_3";

                                try
                                {
                                    db.SubmitChanges();
                                }
                                catch
                                {
                                    exist.URL = info.Name.Translit().ToLower() + "_4";

                                    db.SubmitChanges();
                                }
                            }
                        }
                    }
                }
            }*/

            #endregion

            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();


        }

        #endregion

        #region Импорт разделов каталога (общий)

        public List<SectionListProviderDetails> ProviderImportData
        {
            get
            {
                var columnNumbers = new[]
                    {
                        new SectionListProviderDetails
                            {Key = "Абрис", Link = "http://textbook.ru/upload/Price_all.xls", Func = ParseAbrisList},
                        new SectionListProviderDetails {Key = "Британия", Link = "", Func = ParseBritaniaList},
                        new SectionListProviderDetails {Key = "Эксмо", Link = "", Func = ParseEksmoList},
                        new SectionListProviderDetails {Key = "Глобус", Link = "", Func = ParseGlobusList},
                        new SectionListProviderDetails
                            {
                                Key = "Кнорус",
                                Link = "http://www.knorus.ru/upload/price/prc_all_alf.zip",
                                Func = ParseKnorusList
                            },
                        new SectionListProviderDetails {Key = "Лабиринт", Link = "", Func = ParseLabirintList},
                        new SectionListProviderDetails
                            {
                                Key = "Логосфера",
                                Link = "http://www.logobook.ru/FILES/yandex_list.xml",
                                Func = ParseLogosferaList
                            },
                        new SectionListProviderDetails {Key = "Релод", Link = "", Func = ParseRelodList},
                        new SectionListProviderDetails {Key = "36,6", Link = "", Func = ParseThirtyList},
                        new SectionListProviderDetails
                            {
                                Key = "УЧКНИГА",

                                Link = "http://www.book-online.ru/zip1/biblprice/bibl_price_csv.zip",
                                Func = ParseUchknigaList
                            },
                        new SectionListProviderDetails
                            {
                                Key = "Urait",
                                Link = "http://urait.ru/price/Files/Archive/Urait-poln.zip",
                                Func = ParseUraitList
                            },
                        new SectionListProviderDetails {Key = "Белый город", Link = "", Func = ParseWhitetownList},
                        new SectionListProviderDetails {Key = "ИП Яковлев", Link = "", Func = ParseYakovleffList},
                        new SectionListProviderDetails {Key = "Игрушки", Link = "", Func = ParseToysList},
                        new SectionListProviderDetails {Key = "СервисТорг", Link = "", Func = ParseServiceTorgList}
                    };
                return columnNumbers.ToList();
            }
        }

        [AuthorizeMaster]
        [HttpGet]
        public PartialViewResult SectionListImporter(string partnerName)
        {
            var info = new SectionListDownloadInfo(ProviderImportData, partnerName);
            return PartialView(info);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult SectionListImporter(string partnerName, string returnUrl, SectionListDownloadInfo model, HttpPostedFileBase file)
        {
            var path = Server.CreateDir("/Temp/Price/");
            string target = "";
            var xlsPath = "";

            if (file != null && file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
                if (target.IsNullOrEmpty())
                {
                    ModelState.AddModelError("", "Необходимо выбрать файл с прайс-листом");
                    return PartialView();
                }
                WebUnzipper unzipper = new WebUnzipper(path, "", target);
                bool success = unzipper.GetFile();
                if (!success)
                {
                    ModelState.AddModelError("", unzipper.Info.ErrorList);
                    return PartialView();

                }
                xlsPath = unzipper.ResultFileName;
            }
            else if (model.URL.IsFilled())
            {
                WebUnzipper unzipper = new WebUnzipper(path, "", model.URL);
                bool success = unzipper.GetFile();
                if (!success)
                {
                    ModelState.AddModelError("", unzipper.Info.ErrorList);
                    return PartialView();

                }
                xlsPath = unzipper.ResultFileName;
            }
            else
            {
                ModelState.AddModelError("", "Документ не выбран.");
                return Redirect(returnUrl);
            }
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }

            var provider = ProviderImportData.FirstOrDefault(x => x.Key == partnerName);
            var importList = provider != null ? provider.Func(xlsPath) : ParseUniversalList(xlsPath, "");
            var db = new DB();
            var sections = importList.Select(x => x.Section.Trim()).Distinct().ToList();
            var partnerID = db.Partners.First(x => x.Name == partnerName).ID;

            if (!sections.Any())
            {
                ModelState.AddModelError("", "Документ не содержит категорий.");
            }
            else
            {

                db.ExecuteCommand("update PartnerImportSettings set InList = 0 where PartnerID = " + partnerID);
                foreach (var section in sections)
                {
                    var dbsection =
                        db.PartnerImportSettings.FirstOrDefault(
                            x => x.ImportSectionName == section.Trim() && x.PartnerID == partnerID);
                    if (dbsection != null)
                    {
                        dbsection.InList = true;
                    }
                    else
                    {
                        dbsection = new PartnerImportSetting() { CMSPage = null, PartnerID = partnerID, InList = true, ImportSectionName = section.Trim() };
                        db.PartnerImportSettings.InsertOnSubmit(dbsection);
                    }
                }
                db.SubmitChanges();
                if (model.ClearOld)
                {
                    db.PartnerImportSettings.DeleteAllOnSubmit(db.PartnerImportSettings.Where(x => !x.InList));
                    db.SubmitChanges();
                }
            }

            model.ImportSettingList =
                new PagedData<PartnerImportSetting>(
                    db.PartnerImportSettings.Where(x => x.PartnerID == partnerID).OrderBy(x => x.ImportSectionName),
                    Request.QueryString["page"].ToInt(), 30, "Master");

            return Redirect(returnUrl);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ContentResult SaveSetting(int target, int category, string action)
        {
            DB db = new DB();
            var result = new ContentResult();
            var record = db.PartnerImportSettings.FirstOrDefault(x => x.ID == target);
            if (action == "delete")
            {
                if (record == null)
                    result.Content = "";
                else
                {
                    db.PartnerImportSettings.DeleteOnSubmit(record);
                    db.SubmitChanges();
                    result.Content = "1";
                }
            }
            else
            {
                if (record != null)
                {
                    record.PageID = category;
                    db.SubmitChanges();
                    result.Content = CMSPage.FullPageTable.First(x => x.ID == category).FullPath;
                }
                else
                {
                    result.Content = "";
                }
            }
            return result;
        }

        #endregion

        #region Общие функции обработки импортированной структуры, запускаются в отдкльном потоке

        private void ThreadFuncUpdateCatalog(object context)
        {
            DB dbx = new DB();
            var thi = (ThreadCatalogParserInfo)context;
            System.Web.HttpContext.Current = thi.Context;
            // var processed = new List<BookSaleCatalog>();
            var partnerID = dbx.Partners.First(x => x.Name == thi.PartnerName).ID;
            ParseringInfo info = ParseringInfo.Create(thi.PartnerName);
            info.Created = info.Updated = info.Deleted = info.Dirs = info.Prepared = 0;
            info.EndDate = null;
            info.StartDate = DateTime.Now;
            info.getProcessedList(true).Clear();

            var descrProvider =
                dbx.BookDescriptionProviders.FirstOrDefault(x => x.ProviderName == thi.PartnerName);
            if (descrProvider == null)
            {
                descrProvider = new BookDescriptionProvider()
                {
                    ProviderName = thi.PartnerName,
                    Description = "",
                    IsPriceProvider = false,
                    LoadCoversOnDemand = false
                };
                dbx.BookDescriptionProviders.InsertOnSubmit(descrProvider);
                dbx.SubmitChanges();
            }

            thi.DataList.AsParallel().WithDegreeOfParallelism(/*ImportParallelismDegree*/1).Select(data =>
                {
                    try
                    {
                        if (info.IsItemProcessed(data.PartnerUID, true))
                        {
                            ClearData(ref data);
                            return false;
                        }
                        info.AddProcessedItem(data.PartnerUID, true);
                        var dbt = new DB();
                        var dlo = new DataLoadOptions();

                        dlo.LoadWith<BookSaleCatalog>(x => x.BookDescriptionCatalog);
                        dlo.LoadWith<BookDescriptionCatalog>(x => x.BookAuthorsRels);
                        dlo.LoadWith<BookAuthorsRel>(x => x.Author);
                        dlo.LoadWith<BookSaleCatalog>(x => x.BookPageRels);
                        dlo.LoadWith<BookPageRel>(x => x.CMSPage);
                        dlo.LoadWith<BookDescriptionCatalog>(x => x.BookPublisher);
                        dbt.LoadOptions = dlo;

                        var saleItem =
                            dbt.BookSaleCatalogs.FirstOrDefault(
                                x => x.PartnerID == partnerID && x.PartnerUID == data.PartnerUID);

                        if (partnerID == 16 && saleItem != null)
                        {
                            info.Updated++;
                            ClearData(ref data);
                            return true;
                        }


                        if (thi.PrepareRecordFunc != null)
                        {
                            thi.PrepareRecordFunc(ref data);
                        }

                        if (saleItem == null)
                        {
                            if (data.Header.IsNullOrEmpty())
                            {
                                return false;
                            }
                            info.Created++;
                            saleItem = new BookSaleCatalog { PartnerID = partnerID, PartnerUID = data.PartnerUID };
                            dbt.BookSaleCatalogs.InsertOnSubmit(saleItem);
                            saleItem.IsNew = data.IsNew ?? false;
                            saleItem.IsSpec = data.IsSpec ?? false;
                            saleItem.IsTop = data.IsTop ?? false;

                        }
                        else
                        {
                            info.Updated++;
                            saleItem.IsNew = data.IsNew ?? saleItem.IsNew;
                            saleItem.IsSpec = data.IsSpec ?? saleItem.IsSpec;
                            saleItem.IsTop = data.IsTop ?? saleItem.IsTop;

                        }
                        // processed.Add(saleItem);
                        saleItem.IsAvailable = true;
                        saleItem.PartnerPrice = data.PartnerPrice;
                        if (saleItem.BookDescriptionCatalog == null)
                        {
                            //поиск совпадений по EAN, потом по ISBN
                            BookDescriptionCatalog description = null;
                            if (data.EAN > 0)
                                description = dbt.BookDescriptionCatalogs.FirstOrDefault(x => x.EAN == data.EAN);
                            if (description == null && !data.ISBN.IsNullOrEmpty())
                            {
                                //а вдруг...
                                description = dbt.BookDescriptionCatalogs.FirstOrDefault(x => x.ISBN == data.ISBN);
                            }
                            if (description == null)
                            {
                                //спец для провайдера
                                description =
                                    dbt.BookDescriptionCatalogs.FirstOrDefault(
                                        x =>
                                        x.ProviderUID == data.PartnerUID &&
                                        x.BookDescriptionProvider.ProviderName == thi.PartnerName);


                            }
                            //if (description == null)
                            //{
                            //    //спец для провайдера
                            //    description =
                            //        dbt.BookDescriptionCatalogs.FirstOrDefault(
                            //            x =>
                            //            x.ProviderUID == data.PartnerUID && x.Header == data.Header);


                            //}
                            if (description != null)
                            {
                                saleItem.BookDescriptionCatalog = description;
                            }
                            else
                            {
                                if (data.Header.IsNullOrEmpty())
                                {
                                    return false;
                                }

                                description = new BookDescriptionCatalog
                                    {
                                        ISBN = data.ISBN,
                                        EAN = data.EAN,
                                        BookType = data.Type,
                                        Header = data.Header,
                                        OriginalSectionPath = data.Section,
                                        ProviderUID = data.PartnerUID,
                                        PageCount = data.PageCount,
                                        PublishYear = data.Year,
                                        Annotation = data.Description,
                                        BookDescriptionProvider =
                                            dbt.BookDescriptionProviders.First(x => x.ProviderName == thi.PartnerName)

                                    };

                                foreach (var author in data.Authors)
                                {
                                    var dbAuthor = dbt.Authors.FirstOrDefault(x => x.FIO == author);
                                    if (dbAuthor == null)
                                    {
                                        dbAuthor = new Author { FIO = author };
                                        dbt.Authors.InsertOnSubmit(dbAuthor);
                                    }

                                    dbt.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel()
                                        {
                                            Author = dbAuthor,
                                            BookDescriptionCatalog = description
                                        });

                                }

                                var publisher = dbt.BookPublishers.FirstOrDefault(x => x.Name == data.PublisherName);
                                if (publisher == null)
                                {
                                    publisher = new BookPublisher() { Name = data.PublisherName };
                                    dbt.BookPublishers.InsertOnSubmit(publisher);
                                }
                                description.BookPublisher = publisher;

                                saleItem.BookDescriptionCatalog = description;

                            }
                        }
                        else
                        {
                            if (saleItem.BookDescriptionCatalog.ISBN.IsNullOrEmpty() && data.ISBN.IsFilled())
                            {
                                saleItem.BookDescriptionCatalog.ISBN = data.ISBN;
                                saleItem.BookDescriptionCatalog.EAN =
                                    long.Parse(EAN13.IsbnToEan13(EAN13.NormalizeIsbn(data.ISBN)));

                            }
                        }

                        //PartnerID == 16 - поставщик для книг по требованию
                        //Необходимо сделать список или флажок в БД в будущем 

                        if (partnerID != 16 && !saleItem.BookPageRels.Any())
                        {
                            var another =
                                saleItem.BookDescriptionCatalog.BookSaleCatalogs.Where(
                                    x => x.BookPageRels.Any() && x.ID != saleItem.ID);
                            if (another.Any())
                            {
                                var rel = new BookPageRel { BookSaleCatalog = saleItem, PageID = another.First().BookPageRels.First().PageID };
                                dbt.BookPageRels.InsertOnSubmit(rel);
                            }
                            else
                            {
                                if (data.Section != null)
                                {
                                    var searchedRel =
                                        dbt.PartnerImportSettings.FirstOrDefault(
                                            x => x.ImportSectionName == (data.Section ?? "").Trim() && x.PartnerID == partnerID);
                                    if (searchedRel != null && searchedRel.PageID.HasValue)
                                    {
                                        var rel = new BookPageRel
                                            {
                                                BookSaleCatalog = saleItem,
                                                PageID = searchedRel.PageID.Value
                                            };
                                        dbt.BookPageRels.InsertOnSubmit(rel);
                                    }
                                }
                            }
                        }

                        if (data.PageCount.HasValue && !saleItem.BookDescriptionCatalog.PageCount.HasValue)
                            saleItem.BookDescriptionCatalog.PageCount = data.PageCount;

                        if (data.Year.HasValue && !saleItem.BookDescriptionCatalog.PublishYear.HasValue)
                            saleItem.BookDescriptionCatalog.PublishYear = data.Year;

                        if (!data.Type.IsNullOrEmpty() && saleItem.BookDescriptionCatalog.BookType.IsNullOrEmpty())
                            saleItem.BookDescriptionCatalog.BookType = data.Type;

                        if (data.Description.IsFilled() && saleItem.BookDescriptionCatalog.Annotation.IsNullOrEmpty())
                            saleItem.BookDescriptionCatalog.Annotation = data.Description;

                        if (data.Header.IsFilled() && saleItem.BookDescriptionCatalog.Header.IsNullOrEmpty())
                            saleItem.BookDescriptionCatalog.Header = data.Header;

                        if (!saleItem.BookDescriptionCatalog.CoverID.HasValue && data.CoverURL.IsFilled())
                        {
                            try
                            {
                                var client = new WebClient();
                                var imgData = client.DownloadData(data.CoverURL);
                                var ms = new MemoryStream(imgData);
                                var bitmap = Image.FromStream(ms);

                                var cover = new BookCover { Data = imgData, Name = data.CoverURL, Width = bitmap.Width, Height = bitmap.Height };
                                saleItem.BookDescriptionCatalog.BookCover = cover;
                                dbt.BookCovers.InsertOnSubmit(cover);
                                bitmap.Dispose();
                            }
                            catch (Exception)
                            {

                            }
                        }
                        saleItem.LastUpdate = DateTime.Now;
                        if (data.OutOfPrint.HasValue && data.OutOfPrint.Value)
                            saleItem.IsAvailable = false;
                        dbt.SubmitChanges();

                        if (thi.PostProcessingFunc != null && (!saleItem.BookPageRels.Any() || partnerID != 16))
                        {
                            thi.PostProcessingFunc(saleItem.ID, data, thi.DlModel);
                        }
                        //Освождаем что можем в памяти
                        ClearData(ref data);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        info.AddMessage(ex.Message);
                        info.AddMessage(ex.StackTrace);
                        return false;
                    }
                }).Count();


            var forDel =
                dbx.BookSaleCatalogs.Where(
                    x =>
                    x.PartnerID == partnerID && x.IsAvailable && x.LastUpdate < (thi.AllPartsDuringDay
                                                                    ? DateTime.Now.AddDays(-1)
                                                                    : info.StartDate.Value.AddMinutes(-1)));
            //if (partnerID != 16)
            if (!thi.SkipDelete)
            {
                foreach (var oldEntry in forDel)
                {
                    oldEntry.IsAvailable = false;
                }
                info.Deleted = forDel.Count();
                dbx.SubmitChanges();
            }
            info.AddMessage("Обработка завершена.");
            info.EndDate = DateTime.Now;
        }

        private void ClearData(ref ImportData data)
        {
            data.Authors = null;
            data.CoverURL = null;
            data.Description = null;
            data.Header = null;
            data.PageCount = null;
            data.PartnerUID = null;
            data.Section = null;
            data.PublisherName = null;
            data.Type = null;
            data.Year = null;

        }

        private void UpdateCatalog(List<ImportData> importList, string partnerName, bool allPartsDuringDay = false, PrepareRecordDelegate prepareRecordFunc = null, PostProcessingDelegate postProcessingFunc = null, DownloadInfo dl = null, bool skipDelete = false)
        {
            ParseringInfo info = ParseringInfo.Create(partnerName);
            info.StartDate = DateTime.Now;
            var workingThread = new Thread(ThreadFuncUpdateCatalog);
            workingThread.Start(new ThreadCatalogParserInfo() { Context = System.Web.HttpContext.Current, DataList = importList, PartnerName = partnerName, AllPartsDuringDay = allPartsDuringDay, PrepareRecordFunc = prepareRecordFunc, PostProcessingFunc = postProcessingFunc, DlModel = dl, SkipDelete = skipDelete });
        }

        #endregion

        #region Список импортеров
        public ActionResult List()
        {
            /*       var db = new DB();
                   /*
                               var pages =
                                   CMSPage.FullPageTable.Where(x => x.ParentID == CMSPage.FullPageTable.First(z => z.URL == "toys").ID).
                                       Select(x => x.ID);

                               var list =
                                   db.BookSaleCatalogs.Where(x => x.BookPageRels.Any(z => pages.Contains(z.PageID))).Select(
                                       x => x.BookDescriptionCatalog).Distinct();
                               foreach (var catalog in list)
                               {
                                   if(catalog.CoverID!=null)
                                   {
                                       FileStream fs = new FileStream(Server.MapPath("/Temp/Toys/Export/" + catalog.ID+".jpg"), FileMode.Create);
                                       fs.Write(catalog.BookCover.Data.ToArray(), 0, catalog.BookCover.Data.Length);
                                       fs.Close();
                                   }
                               }
                   #1#
                   DirectoryInfo info = new DirectoryInfo(Server.MapPath("/Temp/Toys/Export/"));
                   foreach (FileInfo file in info.GetFiles())
                   {
                       int did = Path.GetFileNameWithoutExtension(file.FullName).ToInt();
                       var descr = db.BookDescriptionCatalogs.FirstOrDefault(x => x.ID == did);
                       if (descr != null/* && descr.CoverID == null#1#)
                       {
                           FileStream fs = new FileStream(file.FullName, FileMode.Open);
                           byte[] data = new byte[fs.Length];
                           fs.Read(data, 0, (int)fs.Length);

                           MemoryStream ms = new MemoryStream(data);
                           Image bitmap = Image.FromStream(ms);

                           BookCover cover = new BookCover
                               {
                                   Data = data,
                                   Name = "",
                                   Height = bitmap.Height,
                                   Width = bitmap.Width,
                               };

                           db.BookCovers.InsertOnSubmit(cover);
                           descr.BookCover = cover;
                           db.SubmitChanges();

                       }
                   }
       */
            return View();


        }
        #endregion

        #region Заливка цен

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Recommended()
        {
            var db = new DB();
            var partners = db.Partners.OrderBy(x => x.Enabled).ThenBy(x => x.Name);
            ViewBag.PartnerList = new SelectList(partners, "ID", "Name");
            return View(new DownloadInfo());
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Recommended(HttpPostedFileBase file, FormCollection collection)
        {
            var db = new DB();

            var partnerId = collection["PartnerList"].ToInt();

            var partner = db.Partners.FirstOrDefault(x => x.ID == partnerId);
            if (partner == null)
            {
                ModelState.AddModelError("", "Поставщик не найден");
                return View(new DownloadInfo());

            }

            var partners = db.Partners.OrderByDescending(x => x.Enabled).ThenBy(x => x.Name);
            ViewBag.PartnerList = new SelectList(partners, "ID", "Name");
            ViewBag.SelectedPartner = partnerId.ToString();
            ViewBag.SelectedPartnerName = partner.Name;
            var path = Server.CreateDir("/Temp/Recommended/");
            string target = "";

            if (file != null && file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл");
                return View(new DownloadInfo());
            }



            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(new DownloadInfo());
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }


            ParseringInfo info = ParseringInfo.Create(partner.Name);
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset(partner.Name);


            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            var importList = ParseRecommendedList(xlsPath, partnerId);
            UpdateCatalog(importList, partner.Name, false, null, null, null, true);

            return View(new DownloadInfo());

        }

        private List<ImportData> ParseRecommendedList(string xlsPath, int partnerID)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            var db = new DB();
            try
            {


                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;

                for (int irow = 0; irow < sheet.Rows; irow++)
                {
                    counter++;

                    var data = new ImportData();
                    var uidCol = sheet.getCell(0, irow);
                    if (uidCol != null && uidCol.Contents != null)
                        data.PartnerUID = uidCol.Contents.Trim();

                    var sprinterCol = sheet.getCell(1, irow);
                    if (sprinterCol != null && sprinterCol.Contents.IsFilled())
                    {
                        BookDescriptionCatalog book;
                        string content = sprinterCol.Contents.Trim();
                        if (content.StartsWith("S"))
                            book =
                                db.BookDescriptionCatalogs.FirstOrDefault(x => x.ID == content.Replace("S", "").ToInt());
                        else
                            book =
                                db.BookDescriptionCatalogs.FirstOrDefault(
                                    x => x.DataSourceID == 10 && x.ProviderUID == content);
                        if (book != null)
                        {
                            var sale = book.BookSaleCatalogs.FirstOrDefault(x => x.PartnerID == partnerID);
                            if (sale != null)
                                data.PartnerUID = sale.PartnerUID;
                        }
                    }

                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(2, irow).Contents.Trim());

                    importList.Add(data);

                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion

        #region Заливка универсальная

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Universal(int? pid)
        {
            var db = new DB();
            var names = ProviderImportData.Select(x => x.Key).ToList();
            var partners =
                db.Partners.OrderBy(x => x.Enabled).ThenBy(x => x.Name).ToList().Where(x => !names.Contains(x.Name)).ToList();
            if (!pid.HasValue)
                return Redirect(Url.Action("Universal", new { pid = partners.First().ID }));
            ViewBag.PartnerList = new SelectList(partners, "ID", "Name");
            ViewBag.SelectedPartner = pid.ToString();
            ViewBag.SelectedPartnerName = partners.First(x => x.ID == pid).Name;

            return View(InitSectionList(null, partners.First(x => x.ID == pid).Name));
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Universal(int? pid, HttpPostedFileBase file, DownloadInfo di, FormCollection collection)
        {
            var db = new DB();
            var names = ProviderImportData.Select(x => x.Key).ToList();
            var partners = db.Partners.OrderByDescending(x => x.Enabled).ThenBy(x => x.Name).ToList().Where(x => !names.Contains(x.Name)); ;

            if (!pid.HasValue)
                return Redirect(Url.Action("Universal", new { pid = partners.First().ID }));

            ViewBag.PartnerList = new SelectList(partners, "ID", "Name");
            ViewBag.SelectedPartner = pid.ToString();


            var partner = db.Partners.FirstOrDefault(x => x.ID == pid);
            if (partner == null)
            {
                ModelState.AddModelError("", "Поставщик не найден");
                return View(InitSectionList(di, ""));

            }
            ViewBag.SelectedPartnerName = partner.Name;

            var path = Server.CreateDir("/Temp/Universal/");
            string target = "";

            if (file != null && file.ContentLength > 0)
            {
                string filePath = Path.Combine(path, Path.GetFileName(file.FileName));
                target = filePath;
                file.SaveAs(filePath);
            }

            if (target.IsNullOrEmpty())
            {
                ModelState.AddModelError("", "Необходимо выбрать файл");
                return View(InitSectionList(di, partner.Name));
            }



            WebUnzipper unzipper = new WebUnzipper(path, "", target);
            bool success = unzipper.GetFile();
            if (!success)
            {
                ModelState.AddModelError("", unzipper.Info.ErrorList);
                return View(InitSectionList(di, partner.Name));
            }

            string xlsPath = unzipper.ResultFileName;
            if (Translitter.IsRussian(xlsPath))
            {
                xlsPath = Translitter.RenameFileToTranslit(xlsPath);
            }


            ParseringInfo info = ParseringInfo.Create(partner.Name);
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset(partner.Name);


            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            var importList = ParseUniversalList(xlsPath, di.AdditionalPath);
            UpdateCatalog(importList, partner.Name, false, null, null, null, true);

            return View(InitSectionList(di, partner.Name));

        }

        private List<ImportData> ParseUniversalList(string xlsPath, string imgPath)
        {
            Workbook workbook = null;
            var importList = new List<ImportData>();
            try
            {


                workbook = Workbook.getWorkbook(xlsPath);

                Sheet sheet = workbook.Sheets[0];
                int counter = 0;

                for (int irow = 0; irow < sheet.Rows; irow++)
                {
                    counter++;

                    var data = new ImportData();
                    var uidCol = sheet.getCell(0, irow);
                    if (uidCol != null && uidCol.Contents.IsFilled())
                        data.PartnerUID = uidCol.Contents.Trim();

                    data.ISBN = sheet.getCell(1, irow).Contents.Trim();
                    data.PartnerPrice = ImportData.ParsePrice(sheet.getCell(2, irow).Contents.Trim());
                    data.Header = sheet.getCell(3, irow).Contents.Trim();
                    data.PublisherName = sheet.getCell(4, irow).Contents.Trim();
                    data.Authors = ImportData.CreateAuthorsList(sheet.getCell(5, irow).Contents.Trim());
                    data.Year = ImportData.ParseYear(sheet.getCell(6, irow).Contents);
                    data.PageCount = (int?)ImportData.ParseInt(sheet.getCell(7, irow).Contents);
                    data.Type = sheet.getCell(8, irow).Contents;
                    data.Section = sheet.getCell(9, irow).Contents;
                    data.CoverURL = sheet.getCell(10, irow).Contents;
                    if (data.CoverURL.IsNullOrEmpty() && imgPath.IsFilled())
                        data.CoverURL = "{0}{1}.jpg".FormatWith(imgPath, data.PartnerUID);

                    data.Description = sheet.getCell(11, irow).Contents;

                    importList.Add(data);

                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            finally
            {
                if ((workbook != null))
                {
                    workbook.close();

                }
                try
                {
                    System.IO.File.Delete(xlsPath);
                }
                catch (Exception)
                {

                }
            }
            return importList.Where(x => x.PartnerUID.IsFilled() && x.PartnerPrice > 0).ToList();
        }

        #endregion
    }

}