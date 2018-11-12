using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;

namespace Sprinter.Models
{
    public class CommonSearch
    {
        public bool NeedShowSection { get; set; }
        public string SearchQuery { get; set; }
        public bool OnlyInCurrentSection { get; set; }
        public int? SectionID { get; set; }

        public CommonSearch()
        {
            NeedShowSection = AccessHelper.CurrentPageInfo.CurrentPageType == "Catalog";
            var decode = HttpUtility.UrlDecode(HttpContext.Current.Request.QueryString["search"]);
            if (decode != null)
                SearchQuery = decode.Trim();
            int sid = HttpContext.Current.Request.QueryString["section"].ToInt();
            SectionID = sid > 0 ? sid : (int?)null;
            OnlyInCurrentSection = SectionID.HasValue;
        }

        public override string ToString()
        {
            var info = AccessHelper.CurrentPageInfo;
            string targetPage = "/catalog";
            if (info.CurrentPageType == "Catalog")
                targetPage = info.CurrentPage.FullUrl;
            return targetPage +
                   "?section={0}&search={1}".FormatWith(OnlyInCurrentSection ? info.CurrentPage.ID : 0, Microsoft.JScript.GlobalObject.escape(SearchQuery));
        }
    }

    public class ShopCartRight
    {
        private ShopCart _cart;
        public ShopCart Cart
        {
            get { return _cart ?? (_cart = ShopCart.InitCart()); }
        }

        public decimal Discount
        {
            get { return 0; }
        }

        public decimal FinalSum
        {
            get { return Cart.TotalSum - Discount + Cart.SelectedRegion.OrderDeliveryCost; }
        }

        private OrderSteps _steps;
        public OrderSteps Steps
        {
            get { return _steps ?? (_steps = new OrderSteps()); }
        }
    }

    public class OrderStep
    {
        public string CSS { get; set; }
        public int Arg { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public bool IsStepAvailable
        {
            get
            {
                int step = OrderSteps.CurrentStep;
                if (step >= Arg) return true;
                var cart = ShopCart.InitCart();
                if (!cart.ActiveBooks.Any() && Arg > 0) return false;
                if (!HttpContext.Current.User.Identity.IsAuthenticated && Arg > 1) return false;
                if (cart.SelectedRegion.ID == 0 && Arg > 2) return false;
                var correct = cart.IsPersonalDataCorrect;
                if (!correct && Arg > 3)
                    return false;
                return true;
            }
        }
    }

    public class OrderSteps : List<OrderStep>
    {

        public bool AllCorrect
        {
            get { return this.Where(x => x.Arg < 4).All(x => x.IsStepAvailable); }
        }

        public string LastStepUrl
        {
            get { return this.First(x => x.Arg == 4).Url; }
        }

        public OrderSteps()
        {
            var menus = new List<string> { "Корзина заказов", "Оформление заказа", "Доставка", "Персональная информация", "Подтверждение заказа" };
            var current = CurrentStep;
            AddRange(
                menus.Select(
                    (x, i) =>
                    new OrderStep { Name = x, Arg = i, Url = "/order?step=" + i, CSS = getCSS(i, current) }));
        }
        public static int CurrentStep
        {
            get
            {
                var current = HttpContext.Current.Request["step"].IsNullOrEmpty()
                          ? 0
                          : HttpContext.Current.Request["step"].ToInt();
                return current;
            }
        }

        public static string NextStepUrl
        {
            get
            {

                return "/order?step=" + (CurrentStep + 1);
            }
        }



        public static string CurrentStepAction
        {
            get { return "Step" + CurrentStep; }
        }

        private string getCSS(int index, int current)
        {
            if (current == 0)
            {
                if (index == 0) return "active-l";
                if (index == 4) return "f-step-r";
                return "f-step";
            }
            if (current == 1)
            {
                if (index == 0) return "f-step-act-p";
                if (index == 1) return "f-step-active";
                if (index == 4) return "f-step-r";
                return "f-step";
            }
            if (current == 2)
            {
                if (index == 1) return "f-step-act-p";
                if (index == 2) return "f-step-active";
                if (index == 4) return "f-step-r";
                return "f-step";
            }
            if (current == 3)
            {
                if (index == 2) return "f-step-act-p";
                if (index == 3) return "f-step-active";
                if (index == 4) return "f-step-r";
                return "f-step";
            }
            if (current == 4)
            {
                if (index == 3) return "f-step-act-p";
                if (index == 4) return "active-r";
                return "f-step";
            }
            if (current == 5)
            {
                if (index == 4) return "f-step-r";
            }
            return "f-step";
        }
    }
}