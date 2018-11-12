using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;
using Sprinter.Extensions;
namespace Sprinter.Controllers
{
    public class SelectorController : Controller
    {
        public ActionResult OldPageCatL1(int c1)
        {
            SetRedirect("/catalog/{0}/".FormatWith(c1));
            return new ContentResult();
        }

        public ActionResult OldPageCatL2(int c1, string c2)
        {
            SetRedirect("/catalog/{0}/{1}/".FormatWith(c1, c2));
            return new ContentResult();

        }

        private void SetRedirect(string defPath)
        {
            var db = new DB();
            var page = db.CMSPages.FirstOrDefault(x => x.OriginalURL == defPath);
            if(page==null)
            {
                Response.StatusCode = 404;
                return;
            }
            Response.StatusCode = 301;
            Response.RedirectLocation = page.FullUrl;
        }

        public ActionResult OldPage(string url)
        {
            var db = new DB();
            var rx = new Regex(@"-?[a-zA-Z\d]+-");
            var pid = rx.Replace(url, "");
            CommonPageInfo info;
            var dbBook = db.BookDescriptionCatalogs.FirstOrDefault(x => x.ProviderUID == pid);

            if (dbBook == null || !dbBook.BookSaleCatalogs.Any())
            {
                info = new CommonPageInfo { URL = "404", Action = "NotFound", Controller = "TextPage" };
                info.Routes = new RouteValueDictionary();
            }
            else
            {
                var routes = new RouteValueDictionary {{"bookId", dbBook.BookSaleCatalogs.First().ID}};
                var page = dbBook.BookSaleCatalogs.First().BookPageRels.Any()
                                   ? dbBook.BookSaleCatalogs.First().BookPageRels.First().CMSPage
                                   : db.CMSPages.First(x => x.URL == "catalog");
                info = new CommonPageInfo
                    {
                    ID = page.ID,
                    URL = page.FullUrl,
                    CurrentPage = page,
                    Routes = routes
                };
                info.CurrentPage.Title = page.Title.IsNullOrEmpty() ? page.PageName : page.Title;
                info.Controller = "ClientCatalog";
                info.Action = "CatalogDetails";
            }

            info.TagFilter = new List<int>();
            AccessHelper.AddToRepository("CommonInfo", info);
            ViewBag.CommonInfo = info;
            return View(info);
        }

        public ActionResult Index(string url1, string url2, string url3, string url4, string url5, string url6,string url7, string url8, string url9, int? book, int? page)
        {
            var path = new List<string> {url1, url2, url3, url4, url5, url6, url7, url8, url9};
            var info = CommonPageInfo.InitFromQueryParams(path);
            AccessHelper.CurrentPageInfo = info;
            ViewBag.CommonInfo = info;
            return View(info);
        }

    }
}
