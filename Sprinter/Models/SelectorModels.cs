using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Sprinter.Extensions;

namespace Sprinter.Models
{

    public class CommonPageInfo
    {

        public static CommonPageInfo InitFromQueryParams()
        {
            return InitFromQueryParams(HttpContext.Current.Request.Url.ToString().Split<string>("/").ToList());
        }
        public static CommonPageInfo InitFromQueryParams(List<string> slashedParams)
        {
            return InitFromQueryParams(slashedParams,
                                       HttpContext.Current.Request.QueryString.ToString()
                                                  .Split<string>("?", "&")
                                                  .Select(x => x.Split<string>("=").ToArray()).Where(x => x.Length == 2)
                                                  .Select(x => new KeyValuePair<string, string>(x[0], x[1])).ToList());

        }
        public static CommonPageInfo InitFromQueryParams(List<string> slashedParams, List<KeyValuePair<string, string>> queryParams)
        {

            var db = new DB();
            var url = "";
            var request = HttpContext.Current.Request;
            url = slashedParams.All(x => x.IsNullOrEmpty())
                      ? db.CMSPages.First(x => x.PageType.TypeName == "MainPage").URL
                      : slashedParams.Last(x => !x.IsNullOrEmpty());

            var routes = new RouteValueDictionary();
            CommonPageInfo info;
            if (slashedParams[0] == "Master")
            {
                info = new CommonPageInfo()
                    {
                        Controller = slashedParams[1],
                        Action = slashedParams[2],
                        CurrentPage = CMSPage.FullPageTable.First(x => x.URL == "main")
                    };
                return info;
            }

            var pathPairs =
                slashedParams.Where(x => !x.IsNullOrEmpty()).Select((x, index) => new { Key = "url" + (index + 1), Value = x });

            foreach (var pair in pathPairs)
            {
                routes.Add(pair.Key, pair.Value);
            }
            if (request.QueryString["book"].IsFilled())
                routes.Add("bookId", request.QueryString["book"]);


            var cmsPage = db.CMSPages.FirstOrDefault(x => x.URL.ToLower() == url.ToLower());
            if (cmsPage == null || slashedParams[0] == "404")
            {
                info = new CommonPageInfo() { URL = "404", Action = "NotFound", Controller = "TextPage" };
            }
            else
            {
                info = new CommonPageInfo()
                {
                    ID = cmsPage.ID,
                    URL = url,
                    CurrentPage = cmsPage,
                    Routes = routes
                };
                info.CurrentPage.Title = cmsPage.Title.IsNullOrEmpty() ? cmsPage.PageName : cmsPage.Title;
            }
            if (cmsPage != null && cmsPage.PageType.TypeName == "Catalog")
            {
                List<int> tags =
                    (request.QueryString["tags"] ?? "").Split<int>(",").ToList();
                info.TagFilter = tags;
                info.Controller = "ClientCatalog";
                if (tags.Any())// фильтр по тегам
                {
                    info.Action = "CatalogFilteredByTags";
                }
                else
                {
                    var specView = request.QueryString["view"];
                    if (specView.IsFilled())
                    {
                        info.Action = "CatalogSpecPage";
                    }
                    else
                    {
                        if (request["book"].IsNullOrEmpty())
                        {
                            if (request.QueryString["search"].IsFilled())
                            {
                                info.Action = "CatalogSearch";
                            }
                            else
                            {
                                if (cmsPage.Children.Any())
                                    info.Action = "CatalogSectionList";
                                else
                                    info.Action = "CatalogSection";
                            }
                        }
                        else
                        {
                            var exist = db.BookSaleCatalogs.Where(x => x.ID == (request["book"].ToInt()));
                            if (exist.Any())
                            {
                                info.CurrentBook = exist.First();
                                info.Action = "CatalogDetails";
                            }
                            else
                            {
                                info.Action = "NotFound";
                                info.Controller = "TextPage";
                            }
                        }
                    }
                }
            }
            else if (cmsPage != null && cmsPage.PageType.TypeName == "MainPage")
            {
                info.Controller = "MainPage";
                info.Action = "Index";
            }
            else if (cmsPage != null && cmsPage.PageType.TypeName == "ShopCart")
            {
                info.Controller = "ShopCart";
                info.Action = "Index";
            }
            else if (cmsPage != null && cmsPage.PageType.TypeName == "Cabinet")
            {
                info.Controller = "Cabinet";
                info.Action = "Index";
            }
            else if (cmsPage != null)
            {
                info.Controller = "TextPage";
                info.Action = "Index";
            }
            else
            {
                info.Controller = "TextPage";
                info.Action = "NotFound";
            }
            return info;
        }

        public int ID { get; set; }
        public string URL { get; set; }
        public string Action { get; set; }
        public string Controller { get; set; }
        public CMSPage CurrentPage { get; set; }
        public BookSaleCatalog CurrentBook { get; set; }
        public RouteValueDictionary Routes { get; set; }
        public List<int> TagFilter { get; set; }

        private string _keywords;
        public string Keywords
        {
            get
            {
                if (_keywords.IsNullOrEmpty())
                {
                    if (CurrentBook != null)
                    {
                        _keywords =
                            "{0}, {1}, издательство {2}, купить книгу".FormatWith(
                                CurrentBook.BookDescriptionCatalog.Header.ClearHTML(),
                                CurrentBook.BookDescriptionCatalog.AuthorsFIOByComma,
                                CurrentBook.BookDescriptionCatalog.BookPublisher.Name);
                    }
                    else
                    {
                        _keywords = CurrentPage == null ? "" : CurrentPage.Keywords;
                    }
                }
                return _keywords;
            }
            set { _keywords = value; }
        }

        private string _description;
        public string Description
        {
            get
            {
                if (_description.IsNullOrEmpty())
                {
                    if (CurrentBook != null)
                    {
                        _description =
                            "{0}, {1}, {2} руб., издательство {3} {4} {5}".FormatWith(
                                CurrentBook.BookDescriptionCatalog.Header.ClearHTML(),
                                CurrentBook.BookDescriptionCatalog.AuthorsFIOByComma, CurrentBook.TradingPrice.ToString("f2"),
                                CurrentBook.BookDescriptionCatalog.BookPublisher.Name,
                                CurrentBook.BookDescriptionCatalog.ISBN.IsFilled() ? ", ISBN" : "",
                                CurrentBook.BookDescriptionCatalog.ISBN);
                    }
                    else
                    {
                        _description = CurrentPage == null ? "" : CurrentPage.Description;
                    }
                }
                return _description;
            }
            set { _description = value; }
        }

        private string _title;
        public string Title
        {
            get
            {
                if (_title.IsNullOrEmpty())
                {
                    if (CurrentBook != null)
                    {
                        _title = "Купить {2} {0}{3}{1}".FormatWith(CurrentBook.BookDescriptionCatalog.Header,
                                                                   CurrentBook.BookDescriptionCatalog.AuthorsByComma,
                                                                   GoodName,
                                                                   CurrentBook.BookDescriptionCatalog.AuthorsByComma
                                                                              .IsFilled()
                                                                       ? " - "
                                                                       : "");
                    }
                    else
                    {
                        _title = CurrentPage == null ? "Страница не найдена" : CurrentPage.Title;
                    }
                }
                return _title;
            }
            set { _title = value; }
        }

        protected string GoodName
        {
            get
            {
                if (CurrentBook != null)
                {
                    if (CurrentBook.PartnerID <= 16)
                        return "книгу";
                }
                return "";
            }
        }

        public string CurrentPageType
        {
            get
            {
                if (CurrentPage == null)
                    return "TextPage";
                return CurrentPage.PageType.TypeName;
            }

        }

        public bool IsProfilePage
        {
            get { return CurrentPageType == "ShopCart" || CurrentPageType == "Cabinet"; }
        }
    }

}