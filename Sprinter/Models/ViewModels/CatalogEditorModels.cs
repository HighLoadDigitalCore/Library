using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Sprinter.Extensions;
namespace Sprinter.Models.ViewModels
{
    public class CatalogEditor
    {
        public string BackLink { get; set; }
        public List<BookSaleCatalog> AllSaleList { get; set; }
        public BookSaleCatalog CurrentItem { get; set; }

        public SelectList PartnerList { get; set; }

        public SelectList SectionList { get; set; }

        public RouteValueDictionary Routes
        {
            get
            {
                var list = new RouteValueDictionary();
                list.Add("ReturnURL", HttpContext.Current.Request.QueryString["ReturnURL"]);
                if (HttpContext.Current.Request.QueryString["type"].IsFilled())
                    list.Add("type", HttpContext.Current.Request.QueryString["type"]);
                if (HttpContext.Current.Request.QueryString["type"].IsFilled())
                    list.Add("bookID", HttpContext.Current.Request.QueryString["bookID"]);
                return list;
            }
        }

        public CatalogEditor(int saleID, int? type, int? bookID)
        {
            var db = new DB();
            BookSaleCatalog item;
            if (type == 1 || type == 2)
            {
                item = new BookSaleCatalog();
                if (type == 1)
                {
                    item.BookDescriptionCatalog = db.BookDescriptionCatalogs.FirstOrDefault(x => x.ID == bookID);
                }
                else
                {
                    item.BookDescriptionCatalog = new BookDescriptionCatalog();
                }
            }
            else
            {
                item = db.BookSaleCatalogs.FirstOrDefault(x => x.ID == saleID);
            }
            if (item == null)
                return;
            CurrentItem = item;
            BackLink = HttpContext.Current.Request.QueryString["ReturnURL"];
            if (BackLink.IsNullOrEmpty())
                BackLink = "/Master/Catalog";
            if (item.ID > 0 && item.BookDescriptionCatalog != null)
            {
                AllSaleList = item.BookDescriptionCatalog.BookSaleCatalogs.Where(x => x.ID != saleID).ToList();
            }
            else
            {
                AllSaleList = new List<BookSaleCatalog>();
            }
            PartnerList = new SelectList(db.Partners.OrderBy(x => x.SalePriority), "ID", "Description", CurrentItem.PartnerID);

            var pairs = db.CMSPages.Where(x => x.PageType != null && x.PageType.TypeName == "Catalog").Select(x => createPair(x.ID)).ToList();
            pairs.Insert(0, new KeyValuePair<string, string>("0", "--Не определен--"));
            SectionList = new SelectList(pairs, "Key", "Value",
                                         CurrentItem.BookPageRels.Any() ? CurrentItem.BookPageRels.First().PageID : 0);
        }


        private KeyValuePair<string, string> createPair(int pageID)
        {
            var page = CMSPage.FullPageTable.First(x => x.ID == pageID);

            var names = string.Join(" --> ",
                                    page.UrlPath.Select(x => CMSPage.FullPageTable.First(y => y.URL == x).PageName).
                                        ToArray());

            return new KeyValuePair<string, string>(page.ID.ToString(), names);

        }
    }
}