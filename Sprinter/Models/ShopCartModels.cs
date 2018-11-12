using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Linq;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Xml.Linq;
using Sprinter.Extensions;
namespace Sprinter.Models
{

    public class JFieldEntry
    {
        public string name { get; set; }
        public string value { get; set; }
    }

    public partial class ShopCartItem
    {
        public decimal Sum { get { return Count * PriceForOffer; } }

        public decimal PriceForOffer
        {
            get
            {
                return IsSpec && BookSaleCatalog.BookSpecOffers.Any()
                           ? BookSaleCatalog.BookSpecOffers.First().SpecPrice
                           : BookSaleCatalog.TradingPrice;
            }
        }
    }

    public partial class ShopCart
    {
        public decimal SummaryBookWeight
        {
            get
            {
                return ActiveBooks.Sum(x => x.BookSaleCatalog.BookDescriptionCatalog.BookWeight * x.Count) / 1000;
            }
        }

        private ShopCartRight _shopCartRight;
        public ShopCartRight ShopCartRight
        {
            get { return _shopCartRight ?? (_shopCartRight = new ShopCartRight()); }
        }

        private OrderSteps _steps;
        public OrderSteps Steps
        {
            get { return _steps ?? (_steps = new OrderSteps()); }
        }

        public static List<MembershipUser> FindAllUsersByEmail(string mail)
        {
            var users = new List<MembershipUser>();
            users.AddRange(Membership.FindUsersByName(mail).Cast<MembershipUser>());
            users.AddRange(Membership.FindUsersByEmail(mail).Cast<MembershipUser>());
            return users;
        }

        public static string RegisterUser(string name, string pass)
        {
            try
            {
                var db = new DB();
                var newUser = Membership.CreateUser(name, pass, name);
                Roles.AddUserToRole(name, "Client");
                FormsAuthentication.SetAuthCookie(name, true);
                InitCart().SetField("AuthType", 2);
                var profile = new UserProfile()
                {
                    UserID = (Guid)newUser.ProviderUserKey,
                    Name = "",
                    FromIP = HttpContext.Current.Request.UserHostAddress.ToIPInt(),
                    RegDate = DateTime.Now
                };
                db.UserProfiles.InsertOnSubmit(profile);
                db.SubmitChanges();
            }
            catch (Exception e)
            {
                return e.Message;
            }
            return "";
        }

        public static string AuthorizeUser(string name, string pass)
        {
            var res = "Указан неверный пароль или Email.";
            var users = FindAllUsersByEmail(name);
            foreach (var us in users)
            {
                var uname = us.UserName;
                if (us.GetPassword() != pass) continue;
                FormsAuthentication.SetAuthCookie(uname, true);
                MergeShopCarts(us);
                res = "";
                break;
            }
            return res;
        }

        public int MaxAvailableStepForAuth
        {
            get
            {
                if (SelectedProvider == null)
                    return 2;
                return SelectedPayment == null ? 3 : 4;
            }
        }

        public int MaxAvailableStep
        {
            get
            {
                return !HttpContext.Current.User.Identity.IsAuthenticated ? 1 : MaxAvailableStepForAuth;
            }
        }


        //private static DB db = new DB();
        private OrderDeliveryRegion _selectedRegion;
        public OrderDeliveryRegion SelectedRegion
        {
            get
            {
                if (_selectedRegion == null)
                {
                    var rid = GetField<int>("DeliveryRegion");
                    if (rid == 0)
                        _selectedRegion = new OrderDeliveryRegion();
                    else
                    {
                        _selectedRegion = SelectedProvider.OrderDeliveryRegions.FirstOrDefault(x => x.ID == rid) ??
                                          new OrderDeliveryRegion();
                    }
                }
                return _selectedRegion;
            }
        }


        private OrderDeliveryGroup _selectedGroup;
        public OrderDeliveryGroup SelectedGroup
        {
            get
            {
                if (_selectedGroup == null)
                {
                    var gid = GetField<int>("DeliveryGroup");
                    if (gid == 0)
                        _selectedGroup = new OrderDeliveryGroup();
                    else
                    {
                        var db = new DB();
                        _selectedGroup = db.OrderDeliveryGroups.FirstOrDefault(x => x.ID == gid) ??
                                         new OrderDeliveryGroup();
                    }
                }
                return _selectedGroup;
            }
        }

        private OrderDeliveryProvider _selectedProvider;
        public OrderDeliveryProvider SelectedProvider
        {
            get
            {
                if (_selectedProvider == null)
                {
                    var pid = GetField<int>("DeliveryProvider");
                    if (pid == 0)
                        _selectedProvider = new OrderDeliveryProvider();
                    else
                    {
                        _selectedProvider = SelectedGroup.OrderDeliveryProviders.FirstOrDefault(x => x.ID == pid) ??
                                            new OrderDeliveryProvider();
                    }
                }
                return _selectedProvider;
            }
        }

        private OrderPaymentProvider _selectedPayment;
        public OrderPaymentProvider SelectedPayment
        {
            get
            {
                if (_selectedPayment == null)
                {
                    var pid = GetField<int>("DeliveryPayment");
                    if (pid == 0)
                        _selectedPayment = new OrderPaymentProvider();
                    else
                    {
                        var rel = SelectedProvider.OrderPaymentDeliveryRels.FirstOrDefault(x => x.PaymentProviderID == pid);
                        _selectedPayment = rel == null ? new OrderPaymentProvider() : rel.OrderPaymentProvider;
                    }
                }
                return _selectedPayment;
            }
        }

        public string UserMail
        {
            get
            {
                var mail = GetField<string>("UserMail");
                if (mail.IsNullOrEmpty() && HttpContext.Current.User.Identity.IsAuthenticated)
                    return Membership.GetUser().Email;
                return mail;
            }
        }

        public string UserPass
        {
            get
            {
                var pass = GetField<string>("UserPass");

                if (pass.IsNullOrEmpty() && HttpContext.Current.User.Identity.IsAuthenticated)
                    return Membership.GetUser().GetPassword();
                return pass;
            }
        }

        public int AuthType
        {
            get
            {
                var at = GetField<int>("AuthType");
                if (at == 0 && HttpContext.Current.User.Identity.IsAuthenticated)
                    at = 2;
                if (at == 0)
                    at = 1;
                return at;
            }
        }

        public T GetField<T>(string filedName)
        {
            var db = new DB();
            var field = db.ShopCartFields.FirstOrDefault(x => x.Name == filedName && x.ShopCartID == ID);
            if (field == null) return default(T);
            return (T)Convert.ChangeType(field.Value, typeof(T), CultureInfo.InvariantCulture);
        }

        public void SetField(string fieldName, object value)
        {
            var val = (string)Convert.ChangeType(value, typeof(string), CultureInfo.InvariantCulture);
            if (val == null) return;
            var db = new DB();
            var exist = db.ShopCartFields.FirstOrDefault(x => x.ShopCartID == ID && x.Name == fieldName);


            if (exist == null)
            {
                exist = new ShopCartField() { Name = fieldName, ShopCartID = ID, Value = val };
                db.ShopCartFields.InsertOnSubmit(exist);
            }
            else
                exist.Value = val;
            db.SubmitChanges();
        }

        public void AddItem(int id, int count, bool spec = false)
        {
            try
            {
                var db = new DB();
                var item =
                    db.ShopCartItems.FirstOrDefault(x => x.SaleCatalogID == id && x.ShopCartID == ID && x.IsSpec == spec);
                if (item == null)
                {
                    item = new ShopCartItem
                        {
                            ShopCartID = ID,
                            IsDelayed = false,
                            Count = count,
                            SaleCatalogID = id,
                            IsSpec = spec
                        };
                    db.ShopCartItems.InsertOnSubmit(item);
                }
                else
                {
                    item.Count += count;
                    item.IsDelayed = false;
                }
                db.SubmitChanges();
                Reset();
            }
            catch (Exception) { }

        }

        public int AllTypesCount
        {
            get
            {
                return ShopCartItems.Any() ? ShopCartItems.Sum(x => x.Count) : 0;
            }
        }

        public int TotalCount
        {
            get
            {
                return ShopCartItems.Any() ? ShopCartItems.Where(x => !x.IsDelayed).Sum(x => x.Count) : 0;
            }
        }
        public decimal TotalSumWithoutSpecs
        {
            get
            {
                return !ShopCartItems.Any() ? 0 : ShopCartItems.Where(x => !x.IsDelayed && !x.IsSpec).Sum(x => x.Count*x.BookSaleCatalog.TradingPrice);
            }
        }       
        
        public decimal TotalSum
        {
            get
            {
                if (!ShopCartItems.Any()) return 0;
                return
                    ShopCartItems.Where(x => !x.IsDelayed && !x.IsSpec).Sum(x => x.Count*x.BookSaleCatalog.TradingPrice) +
                    ShopCartItems.Where(x => !x.IsDelayed && x.IsSpec)
                                 .Sum(
                                     x =>
                                     (x.BookSaleCatalog.BookSpecOffers.Any()
                                          ? x.BookSaleCatalog.BookSpecOffers.First().SpecPrice
                                          : x.BookSaleCatalog.TradingPrice)*x.Count);
            }
        }

        public void Reset()
        {
            _selectedGroup = null;
            _selectedProvider = null;
            _selectedRegion = null;
        }

        private static void MergeShopCarts(MembershipUser us)
        {

            var uid = (Guid)us.ProviderUserKey;
            Guid? cKey = null;
            if (HttpContext.Current.Request.Cookies["ck"] != null)
                cKey = new Guid(HttpContext.Current.Request.Cookies["ck"].Value);
            if (!cKey.HasValue) return;

            DB db = new DB();
            var carts = db.ShopCarts.Where(x => x.UserID == uid || x.TemporaryKey == cKey);
            if (carts.Count() <= 1) return;
            //добавляем в старую корзину новые записи (отложенные) и заменяем активные

            //новая карта
            var last = carts.OrderByDescending(x => x.LastRequested).First();


            var first = carts.OrderBy(x => x.LastRequested).First(); //старая карта

            //удаляем старые записи (активные)
            db.ShopCartItems.DeleteAllOnSubmit(db.ShopCartItems.Where(x => x.ShopCartID == first.ID && !x.IsDelayed));
            db.SubmitChanges();


            //переносим отложенные
            foreach (var item in last.ShopCartItems)
            {
                var exist =
                    db.ShopCartItems.FirstOrDefault(
                        x => x.SaleCatalogID == item.SaleCatalogID && x.ShopCartID == first.ID);

                if (exist != null)
                    exist.Count += item.Count;
                else
                    item.ShopCartID = first.ID;
            }
            first.LastRequested = DateTime.Now;
            db.SubmitChanges();


            var forDel = db.ShopCarts.Where(x => (x.UserID == uid || x.TemporaryKey == cKey) && x.ID != first.ID);
            db.ShopCarts.DeleteAllOnSubmit(forDel);
            db.SubmitChanges();


            var cook = HttpContext.Current.Request.Cookies.Get("ck");

            if (cook != null && cook.Value.IsGuid())
            {
                cook.Expires = DateTime.Now.AddYears(1);
            }
            else
            {
                cook = new HttpCookie("ck", first.TemporaryKey.ToString()) {Expires = DateTime.Now.AddYears(1)};
            }
            cook.Value = first.TemporaryKey.ToString();
            HttpContext.Current.Response.Cookies.Add(cook);
        }


        public static ShopCart InitCart()
        {
            //if (Roles.IsUserInRole("Administrator")) return new ShopCart();
            DB db = new DB();
            ShopCart shopcart = null;
            string cKey = "";
            if (HttpContext.Current.Request.Cookies["ck"] != null)
                cKey = HttpContext.Current.Request.Cookies["ck"].Value;
            if (cKey.IsNullOrEmpty() || !cKey.IsGuid())
                cKey = Guid.NewGuid().ToString();
            if (HttpContext.Current.Request.IsAuthenticated)
            {
                shopcart = db.ShopCarts.FirstOrDefault(x => x.UserID == (Guid)Membership.GetUser().ProviderUserKey);
            }
            if (shopcart == null || !HttpContext.Current.Request.IsAuthenticated)
            {
                shopcart = db.ShopCarts.FirstOrDefault(x => x.TemporaryKey == new Guid(cKey));
            }
            if (shopcart == null)
            {
                shopcart = new ShopCart();
                if (HttpContext.Current.Request.IsAuthenticated)
                    shopcart.UserID = (Guid)Membership.GetUser().ProviderUserKey;
                shopcart.TemporaryKey = new Guid(cKey);
                db.ShopCarts.InsertOnSubmit(shopcart);
            }

            shopcart.LastRequested = DateTime.Now;
            if (shopcart.UserID == null && HttpContext.Current.User.Identity.IsAuthenticated)
            {
                var uid = (Guid)Membership.GetUser().ProviderUserKey;
                var exist = db.ShopCarts.FirstOrDefault(x => x.UserID == uid);
                if (exist == null)
                    shopcart.UserID = uid;
            }
            db.SubmitChanges();
            db.ShopCarts.DeleteAllOnSubmit(db.ShopCarts.Where(x => x.LastRequested < DateTime.Now.AddYears(-1)));//?
            db.SubmitChanges();
            var cook = HttpContext.Current.Request.Cookies.Get("ck");

            if (cook != null && cook.Value.IsGuid())
                cook.Expires = DateTime.Now.AddYears(1);
            else
            {
                cook = new HttpCookie("ck", cKey);
                cook.Expires = DateTime.Now.AddYears(1);

            }
            cook.Value = shopcart.TemporaryKey.ToString();
            try
            {
                HttpContext.Current.Response.Cookies.Add(cook);
            }
            catch { }
            return shopcart;

        }


        public IEnumerable<ShopCartItem> ActiveBooks
        {
            get
            {
                var db = new DB();
                return db.ShopCartItems.Where(x => x.BookSaleCatalog.IsAvailable && !x.IsDelayed && x.ShopCartID == ID);
            }
        }
        public IEnumerable<ShopCartItem> DelayesBooks
        {
            get
            {
                var db = new DB();
                return db.ShopCartItems.Where(x => x.BookSaleCatalog.IsAvailable && x.IsDelayed && x.ShopCartID == ID);
            }
        }
        public IEnumerable<ShopCartItem> AbsentBooks
        {
            get
            {
                var db = new DB();
                return db.ShopCartItems.Where(x => !x.BookSaleCatalog.IsAvailable && x.ShopCartID == ID);
            }
        }

        public IEnumerable<BookSpecOffer> SpecBooks
        {
            get { 
                var db = new DB();
                return db.BookSpecOffers.Where(x => TotalSumWithoutSpecs >= x.MinPrice);
            }
        }

        public bool FirstTime
        {
            get { return Membership.GetUser().UserEntity().Profile.Name.IsNullOrEmpty(); }
        }

        private List<KeyValuePair<string, string>> _relations;
        protected List<KeyValuePair<string, string>> Relations
        {
            get
            {
                return _relations ?? (_relations = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("UserFirstName", "Name"),
                        new KeyValuePair<string, string>("UserSurname", "Surname"),
                        new KeyValuePair<string, string>("UserSecName", "Patrinomic"),
                        new KeyValuePair<string, string>("UserPhone", "HomePhone"),
                        new KeyValuePair<string, string>("UserMobile", "MobilePhone"),
                        new KeyValuePair<string, string>("DeliveryIndex", "ZipCode"),
                        new KeyValuePair<string, string>("DeliveryTown", "Town"),
                        new KeyValuePair<string, string>("DeliveryStreet", "Street"),
                        new KeyValuePair<string, string>("DeliveryHouse", "House"),
                        new KeyValuePair<string, string>("DeliveryFlat", "Flat"),
                        new KeyValuePair<string, string>("DeliverySection", "Building"),
                        new KeyValuePair<string, string>("DeliveryDoorway", "Doorway"),
                        new KeyValuePair<string, string>("DeliveryFloor", "Floor"),
                        new KeyValuePair<string, string>("OrgName", "OrgName"),
                        new KeyValuePair<string, string>("OrgINN", "OrgINN"),
                        new KeyValuePair<string, string>("OrgKPP", "OrgKPP"),
                        new KeyValuePair<string, string>("OrgKS", "OrgKS"),
                        new KeyValuePair<string, string>("OrgRS", "OrgRS"),
                        new KeyValuePair<string, string>("OrgBank", "OrgBank"),
                        new KeyValuePair<string, string>("OrgBik", "OrgBik"),
                        new KeyValuePair<string, string>("OrgJurAddr", "OrgJurAddr"),
                        new KeyValuePair<string, string>("OrgFactAddr", "OrgFactAddr"),
                        new KeyValuePair<string, string>("OrgDirector", "OrgDirector"),
                        new KeyValuePair<string, string>("OrgAccountant", "OrgAccountant")
                    });
            }
        }



        public bool IsPersonalDataCorrect
        {
            get { return GetField<bool>("PersonalCorrect"); }
        }


        public void InitFieldsIfEmpty()
        {
            var profile = Membership.GetUser().UserEntity().Profile;
            if (profile == null || profile.Name.IsNullOrEmpty()) return;
            var db = new DB();
            var keys = Relations.Select(x => x.Key);
            var fields = db.ShopCartFields.Where(x => keys.Contains(x.Name) && x.ShopCartID == ID).ToList();
            foreach (var key in keys)
            {
                var field = fields.FirstOrDefault(x => x.Name == key);
                var data = profile.GetPropertyValue(Relations.First(x => x.Key == key).Value);
                if (data != null)
                {
                    if (field == null || field.Value.IsNullOrEmpty() || field.Value != data.ToString())
                    {
                        SetField(key, data);
                    }
                }
            }
        }



        public void SaveFieldsInProfile(bool overwrite = false)
        {
            var db = new DB();
            var profile = db.UserProfiles.FirstOrDefault(x => x.UserID == (Guid)Membership.GetUser().ProviderUserKey);
            if (profile == null)
            {
                profile = new UserProfile() { UserID = (Guid)Membership.GetUser().ProviderUserKey };
                db.UserProfiles.InsertOnSubmit(profile);
            }
            var keys = Relations.Select(x => x.Key);
            foreach (var key in keys)
            {
                var field = GetField<object>(key);
                var data = profile.GetPropertyValue(Relations.First(x => x.Key == key).Value);
                if (field != null && (data == null || data.ToString().IsNullOrEmpty() || overwrite))
                {
                    profile.SetPropertyValue(Relations.First(x => x.Key == key).Value, field);
                }
            }

            if (SelectedProvider.ShowRegions && profile.Region.IsNullOrEmpty())
                profile.Region = SelectedRegion.Name;
            else profile.Region = SelectedProvider.DefaultCity;

            profile.Address = profile.FullAdress;
            db.SubmitChanges();

        }


        public Order CreateOrder()
        {
            var order = new Order()
                {
                    CreateDate = DateTime.Now,
                    StatusID = OrderStatus.GetStatusID("Accepted"),
                    UserID = (Guid)Membership.GetUser().ProviderUserKey
                };
            var details = new OrderDetail()
                {
                    Address = OrderAddress,
                    DeliveryCost = SelectedRegion.OrderDeliveryCost,
                    DeliveryType = SelectedProvider.SprinterID,
                    PaymentType = SelectedPayment.SprinterUID,
                    Order = order,
                    OrgData = OrderOrgData,
                    RegionID = SelectedRegion.ID
                };

            var db = new DB();
            db.Orders.InsertOnSubmit(order);
            db.OrderDetails.InsertOnSubmit(details);

            foreach (var x in ActiveBooks)
            {
                db.OrderedBooks.InsertOnSubmit(
                    new OrderedBook
                        {
                            Amount = x.Count,
                            SalePrice = x.PriceForOffer,
                            BookDescriptionID = x.BookSaleCatalog.DescriptionID,
                            Order = order,
                            PartnerID = x.BookSaleCatalog.Partner.ID
                        });

            }
            db.SubmitChanges();
            return order;
        }

        public string HTMLForLetterBookList
        {
            get
            {
                string message = "<table>";
                message += "<tr><td><b>Название</b></td><td><b>Количество</b></td><td><b>Цена</b></td></tr>";
                message += string.Join("",
                                       ActiveBooks.Select(
                                           x =>
                                           "<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>".FormatWith(
                                               x.BookSaleCatalog.BookDescriptionCatalog.Header, x.Count.ToString(),
                                               x.Sum.ForDisplaing())));
                message += "<tr><td colspan=\"3\"><b>Итого к оплате (включая доставку)&nbsp;&mdash;&nbsp;" +
                           ShopCartRight.FinalSum.ForDisplaing() + " руб.</b></td></tr> </table>";
                return message;
            }
        }

        public string HTMLForUserData
        {
            get
            {
                string message = "";
                message += TranslateToHtml(OrderUserData, "UserData");
                var orgData = OrderOrgData;
                if (!string.IsNullOrEmpty(orgData))
                {
                    message += "<h3>Информация о юридическом лице:</h3><br>";
                    message += TranslateToHtml(orgData, "OrgData");
                }
                return message;
            }
        }

        public string HtmlForDelivery
        {
            get {
                string message = "<b>{0}, {1}, стоимость - {2} руб.</b><br>".FormatWith(
                         SelectedProvider.Name,
                         SelectedRegion.Name, SelectedRegion.OrderDeliveryCost.ForDisplaing());
                message += TranslateToHtml(OrderAddress, "Address");
                return message;
            }
        }

        public string HTMLForFullLetter
        {
            get
            {
                string message = "<h3>Заказанные товары:</h3><br>";
                message += HTMLForLetterBookList;
                message += "<br><br>";
                message += "<h3>Информация о покупателе:</h3><br>";
                message += HTMLForUserData;
                message +=
                    "<h3>Информация о доставке:</h3><br>";
                message += HtmlForDelivery;
                return message;
            }
        }

        public static string TranslateToHtml(string xml, string parent)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(xml);
            }
            catch (Exception)
            {
                return "";
            }

            var message = "<table style=\"width:100%\">";
            var profile = new UserProfile.ProfileDataAnnotations();
            message += string.Join("",
                                   document.Descendants(parent).Elements().Select(
                                       x =>
                                       "<tr><td style=\"width:300px\">{0}:</td><td><b>{1}</b></td><tr>".FormatWith(
                                           profile.GetPropertyAttribute<DisplayNameAttribute>(x.Name.LocalName,
                                                                                              "DisplayName"), x.Value)));
            message += "</table><br>";
            return message;

        }

        public void ClearActive()
        {
            var db = new DB();
            db.ShopCartItems.DeleteAllOnSubmit(
                db.ShopCartItems.Where(x => x.ShopCartID == ID && !x.IsDelayed && x.BookSaleCatalog.IsAvailable));
            db.SubmitChanges();
        }

        public string OrderUserData
        {
            get
            {
                var profile = Membership.GetUser().UserEntity().Profile;
                var doc = new XDocument();
                var data = new XElement("UserData");
                doc.Add(data);
                data.Add(new XElement("Email", profile.Email));
                data.Add(new XElement("Surname", profile.Surname));
                data.Add(new XElement("Name", profile.Name));
                data.Add(new XElement("Patrinomic", profile.Patrinomic));
                data.Add(new XElement("HomePhone", profile.HomePhone));
                data.Add(new XElement("MobilePhone", profile.MobilePhone));
                return doc.ToString();
            }
        }

        public string OrderRegion
        {
            get
            {
                if (SelectedProvider.ShowRegions)
                    return SelectedRegion.Name;
                return SelectedProvider.DefaultCity;
            }
        }

        public string OrderOrgData
        {
            get
            {
                if (!GetField<bool>("ShowOrg")) return "";
                var keys = Relations.Where(x => x.Key.StartsWith("Org")).Select(x => x.Key);
                var doc = new XDocument();
                var data = new XElement("OrgData");
                doc.Add(data);
                foreach (var key in keys)
                {
                    data.Add(new XElement(key, GetField<string>(key)));
                }
                return doc.ToString();
            }
        }

        public string OrderAddress
        {
            get
            {
                var doc = new XDocument();
                var address = new XElement("Address");
                doc.Add(address);
                if (!SelectedProvider.ShowAdress) return doc.ToString();
                var fields = new List<KeyValuePair<string, string>>();
                if (SelectedProvider.ShowIndex)
                    fields.Add(new KeyValuePair<string, string>("ZipCode", GetField<string>("DeliveryIndex")));
                if (SelectedProvider.ShowTown)
                    fields.Add(new KeyValuePair<string, string>("Town", GetField<string>("DeliveryTown")));
                fields.Add(new KeyValuePair<string, string>("Street", GetField<string>("DeliveryStreet")));
                fields.Add(new KeyValuePair<string, string>("House", GetField<string>("DeliveryHouse")));
                fields.Add(new KeyValuePair<string, string>("Building", GetField<string>("DeliverySection")));
                fields.Add(new KeyValuePair<string, string>("Flat", GetField<string>("DeliveryFlat")));
                if (SelectedProvider.ShowTime)
                {
                    fields.Add(new KeyValuePair<string, string>("Doorway", GetField<string>("DeliveryDoorway")));
                    fields.Add(new KeyValuePair<string, string>("Floor", GetField<string>("DeliveryFloor")));
                }

                foreach (var pair in fields)
                {
                    address.Add(new XElement(pair.Key, pair.Value));
                }
                return doc.ToString();
            }
        }


    }
}