using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace Sprinter.Models.ViewModels
{
    public class CountersBlockModel
    {
        protected DB db = new DB();
        public int OrdersCount { get; private set; }

        public CountersBlockModel()
        {
            OrdersCount = db.Orders.Count();
        }
        private int? _goodsCount;
        public int? GoodsCount
        {
            get
            {
                if (_goodsCount == null)
                {
                    //сначала кеш
                    //  var cached = HttpRuntime.Cache.Get("GoodsCount");
                    /*
                                        if (cached != null && cached is int)
                                        {
                                            _goodsCount = (int)cached;
                                        }
                                        else
                                        {
                    */

                    _goodsCount =
                        CMSPage.FullPageTable.Where(x => x.Type == 1 && x.TreeLevel == 1).Sum(x => x.ActiveCount);
                    /*

                                            _goodsCount =
                                                db.BookSaleCatalogs.Where(
                                                    x => x.IsAvailable && x.BookPageRels.Any() && x.PartnerPrice > 0 && x.Partner.Enabled).
                                                    Select(x => x.DescriptionID).Distinct().Count();
                    */
                    /*

                                            HttpRuntime.Cache.Insert("GoodsCount",
                                                                     _goodsCount,
                                                                     new SqlCacheDependency("Sprinter", "BookSaleCatalog"),
                                                                     DateTime.Now.AddDays(1D),
                                                                     Cache.NoSlidingExpiration);
                                        }
                    */
                }
                return _goodsCount;
            }
            set
            {
                if (value == null)
                {
                    HttpRuntime.Cache.Remove("GoodsCount");
                }
            }
        }
    }

    public class HeaderViewModel
    {
        protected DB db = new DB();
        public List<CMSPage> MainMenu { get; private set; }
        public string HeaderPhones { get; private set; }
        public string FooterPhones { get; private set; }
        public HeaderViewModel()
        {

            MainMenu = db.CMSPages.Where(
                x =>
                x.PageType.TypeName != "Catalog" && x.PageType.TypeName != "MainPage" && x.ViewMenu).AsEnumerable().
                Where(x => x.TreeLevel == 0)
                .OrderBy(
                    x => x.OrderNum).ToList();




            HeaderPhones = SiteSetting.Get<string>("HeaderPhone");
            FooterPhones = SiteSetting.Get<string>("FooterAdress");
        }



    }


}