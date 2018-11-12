using System;
using System.Collections.Generic;
using System.Data.Linq.SqlClient;
using System.Data.Objects;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Sprinter.Extensions;
using Sprinter.Models.ViewModels;

namespace Sprinter.Models
{

    public class MasterSearchData
    {
        public string RedirectAction { get; set; }


        public bool ByName { get; set; }
        public bool ByIsbn { get; set; }
        public bool ByCode { get; set; }
        public bool ByTag { get; set; }

        public bool ByPublisher { get; set; }
        public bool ByAuthor { get; set; }
        public bool ByPartner { get; set; }
        public bool ByAvailableOnly { get; set; }


        public bool ByNoCover { get; set; }
        public bool ByAnnotation { get; set; }
        public bool ByPrice { get; set; }
        public bool ByNoAnnotation { get; set; }

        public bool ByNewsOnly { get; set; }
        public bool BySpecOnly { get; set; }
        public bool ByTopOnly { get; set; }
        public bool ByFixPrice { get; set; }


        public string SearchWords { get; set; }
        public string PageListPlain { get; set; }
        public int ResultCount { get; set; }

        private int? _searchedCount;
        public int SearchedCount
        {
            get
            {
                if (!_searchedCount.HasValue)
                {
                    if (MainFilter != null)
                        _searchedCount = MainFilter.FinalList.Count();
                    else
                    {
                        //if (_search == null) Вызывается в посте, поэтому убиру нахуй, чтобы лишний раз не долбить базу
                        //    Search();
                        _searchedCount = _search != null ? _search.Count() : 0;
                    }
                }
                return (int) _searchedCount;
            }
            set { _searchedCount = value; }
        }

        public bool IsSortDesc { get; set; }
        public bool SortByIsbn { get; set; }
        public bool SortByHeader { get; set; }
        public bool SortByAuthor { get; set; }
        public bool SortByPublisher { get; set; }
        public bool SortByPartner { get; set; }
        public bool SortByPrice { get; set; }

        public bool IsSortTypeSetted
        {
            get { return SortByIsbn || SortByHeader || SortByAuthor || SortByPublisher || SortByPartner || SortByPrice; }
        }

        public RouteValueDictionary RoutesWithoutOrder { get; set; }

        public string OrderArrow
        {
            get
            {
                if (IsSortDesc)
                    return "&uarr;";
                return "&darr;";
            }
        }

        public MvcHtmlString getArrowByAttribute(string name)
        {
            if ((bool)this.GetPropertyValue(name) || (!IsSortTypeSetted && name == "SortByHeader"))
                return new MvcHtmlString(OrderArrow);
            return new MvcHtmlString("");
        }

        public string getSortLinkByAttribute(string name)
        {
            var copy = new RouteValueDictionary(RoutesWithoutOrder);
            copy.Add(name, "True");
            copy.Add("IsSortDesc", (bool)this.GetPropertyValue(name) || (!IsSortTypeSetted && name == "SortByHeader") ? !IsSortDesc : IsSortDesc);
            var urlHelper = new UrlHelper(HttpContext.Current.Request.RequestContext);
            string url = urlHelper.Action(RedirectAction, "Catalog", copy);
            return url;
        }

        public static MasterSearchData InitFromQuery(string qs)
        {
            var data = new MasterSearchData();
            var pairs = qs.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            var paramList =
                pairs.Select(x => x.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries)).Where(x => x.Count() == 2);

            foreach (string[] args in paramList)
            {
                data.SetPropertyValueByString(args[0], HttpUtility.UrlDecode(args[1]));
            }
            if (data.ResultCount == 0)
                data.ResultCount = 30;
            return data;
        }

        public RouteValueDictionary CreateRoutes(string qs)
        {
            var list = CreateRoutes();
            var pairs = qs.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            var paramList =
                pairs.Select(x => x.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries)).Where(x => x.Count() == 2);
            foreach (string[] pair in paramList)
            {
                if (!list.ContainsKey(pair[0]))
                    list.Add(pair[0], HttpUtility.UrlDecode(pair[1]));
            }
            RoutesWithoutOrder = new RouteValueDictionary();
            foreach (var pair in list)
            {
                if (!pair.Key.StartsWith("Sort") && pair.Key != "IsSortDesc")
                {
                    RoutesWithoutOrder.Add(pair.Key, pair.Value);
                }
            }
            return list;
        }
        public RouteValueDictionary CreateRoutes()
        {
            var list = new RouteValueDictionary();

            var props = this.GetType().GetProperties();
            foreach (PropertyInfo prop in props)
            {
                if (prop.Name.StartsWith("By"))
                {
                    list.Add(prop.Name, this.GetPropertyValue(prop.Name));
                }
            }
            list.Add("SearchWords", this.GetPropertyValue("SearchWords"));
            list.Add("PageListPlain", this.GetPropertyValue("PageListPlain"));
            list.Add("ResultCount", this.GetPropertyValue("ResultCount"));
            list.Add("IsSortAsc", this.GetPropertyValue("IsSortAsc"));
            foreach (PropertyInfo prop in props)
            {
                if (prop.Name.StartsWith("SortBy"))
                {
                    var sortType = (bool)this.GetPropertyValue(prop.Name);
                    if (sortType)
                    {
                        list.Add(prop.Name, true);
                    }
                }
            }
            return list;
        }

        public string SearchQuery
        {
            get
            {
                return
                    "%{0}%".FormatWith(
                        SearchWords.Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").Trim().Replace(" ", "%")).
                        ToLower();
            }
        }

        public List<int> PageList
        {
            get
            {
                return
                    (PageListPlain ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToInt()).
                        ToList();
            }
        }

        public List<string> SearchWordList
        {
            get
            {
                return
                    SearchWords.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Select(
                        x => string.Format("%{0}%", x.Trim().ToLower())).ToList();
            }
        }

        public MasterSearchData()
        {
            ByName = ByTag = ByAvailableOnly = true;
            ByIsbn = ByPublisher = ByAuthor = ByAnnotation = ByPartner = ByPrice = ByNoAnnotation = ByNoCover = ByCode = false;
            SearchWords = "";
        }

        public string ErrorMessage
        {
            get
            {
                if (SearchWords.IsNullOrEmpty()) return "Необходимо указать поисковую фразу";
                if (SearchWords.Length < 2) return "Минимальная длина поисковой фразы - 2 символа";
                if (!(ByAuthor || ByIsbn || ByName || ByPublisher || ByTag))
                    return "Необходимы выбрать тип поиска";
                return "";
            }
        }

        public MasterSearchViewModel MainFilter { get; set; }

        private IQueryable<BookSaleCatalog> _search;
        public IQueryable<BookSaleCatalog> Search()
        {
            if (_search == null)
            {
                DB db = new DB();
                db.CommandTimeout = 3600;
                var primary = db.BookSaleCatalogs.Where(x => x.BookDescriptionCatalog != null);
                IQueryable<BookDescriptionCatalog> result = null;
                if (ByAnnotation)
                {
                    if (PageList.Any())
                    {
                        primary = primary.Where(x => x.BookPageRels.Any(z => PageList.Contains(z.PageID)));
                    }
                    if (SearchWords.Any())
                    {
                        var byAnno = db.getBooksByAnnotation(SearchWords, 1, 1 /*2*/);
                        result = primary.Join(byAnno, x => x.BookDescriptionCatalog.ID, y => y.ID,
                                              (x, y) => x.BookDescriptionCatalog);

                    }

                }
                else
                {

                    if (ByAvailableOnly)
                    {
                        primary = primary.Where(x => x.IsAvailable);
                    }
                    if (ByNoCover)
                    {
                        primary = primary.Where(x => !x.BookDescriptionCatalog.CoverID.HasValue);
                    }
                    if (ByTopOnly)
                    {
                        primary = primary.Where(x => x.IsTop);
                    }
                    if (ByNewsOnly)
                    {
                        primary = primary.Where(x => x.IsNew);
                    }
                    if (BySpecOnly)
                    {
                        primary = primary.Where(x => x.IsSpec);
                    }
                    if (ByNoAnnotation)
                    {
                        primary =
                            primary.Where(
                                x =>
                                x.BookDescriptionCatalog.Annotation == null || x.BookDescriptionCatalog.Annotation == "");
                    }
                    if (ByFixPrice)
                    {
                        primary = primary.Where(x => x.PriceOverride.HasValue);
                    }
                    if (PageList.Any())
                    {
                        primary = primary.Where(x => x.BookPageRels.Any(z => PageList.Contains(z.PageID)));
                    }

                    if (SearchWords.Any())
                    {

                        if (ByPrice)
                        {

                            decimal price;
                            if (decimal.TryParse(
                                SearchWords.Replace("р.", "").Replace("$", "").Trim().Replace(".", ","),
                                out price))
                                primary = primary.Where(x => x.PartnerPrice == price);
                        }
                        if (ByPartner)
                        {
                            primary = primary.Where(x => SqlMethods.Like(x.Partner.Name, SearchQuery));
                        }

                        var bookPrimary = primary.Select(x => x.BookDescriptionCatalog);

                        if (ByName)
                        {
                            result =
                                bookPrimary.Where(x => SqlMethods.Like(x.Header.ToLower(), SearchQuery));
                        }



                        if (ByIsbn)
                        {
                            if (result == null)
                                result = bookPrimary.Where(
                                    x => SqlMethods.Like(x.ISBN.ToLower(), SearchQuery));
                            else
                                result =
                                    result.Union(
                                        bookPrimary.Where(
                                            x => SqlMethods.Like(x.ISBN.ToLower(), SearchQuery)));

                        }
                        if (ByCode)
                        {
                            if (result == null)
                                result = bookPrimary.Where(
                                    x =>
                                    SqlMethods.Like(x.ID.ToString().ToLower(), SearchQuery) ||
                                    SqlMethods.Like(x.ProviderUID.ToLower(), SearchQuery));
                            else
                                result =
                                    result.Union(
                                        bookPrimary.Where(
                                            x =>
                                            SqlMethods.Like(x.ID.ToString().ToLower(), SearchQuery) ||
                                            SqlMethods.Like(x.ProviderUID.ToLower(), SearchQuery)));

                        }
                        if (ByTag)
                        {
                            if (result == null)

                                result = bookPrimary.Where(
                                    x =>
                                    x.BookTagRels.Any(
                                        y =>
                                        SqlMethods.Like(y.BookTag.Tag.ToLower(), SearchWordList.First())));
                            else
                                result = SearchWordList.Skip(1).Aggregate(result,
                                                                          (current, searchWord) =>
                                                                          current.Union(
                                                                              bookPrimary.Where(
                                                                                  x =>
                                                                                  x.BookTagRels.Any(
                                                                                      y =>
                                                                                      SqlMethods.Like(
                                                                                          y.BookTag.Tag.ToLower(),
                                                                                          searchWord)))));

                        }
                        if (ByAuthor)
                        {
                            if (result == null)
                                result = bookPrimary.Where(
                                    x =>
                                    x.BookAuthorsRels.Any(
                                        y => SqlMethods.Like(y.Author.FIO, SearchQuery)));
                            else
                                result =
                                    result.Union(
                                        bookPrimary.Where(
                                            x =>
                                            x.BookAuthorsRels.Any(
                                                y => SqlMethods.Like(y.Author.FIO, SearchQuery))));

                        }
                        if (ByPublisher)
                        {
                            if (result == null)
                                result = bookPrimary.Where(
                                    x =>
                                    SqlMethods.Like(x.BookPublisher.Name, SearchQuery));
                            else
                                result =
                                    result.Union(
                                        bookPrimary.Where(
                                            x =>
                                            SqlMethods.Like(x.BookPublisher.Name, SearchQuery)));
                        }

                    }
                }
                if (result == null)
                    result = primary.Select(x => x.BookDescriptionCatalog);

                result = result.Distinct();

                if (!IsSortDesc)
                {
                    if (SortByHeader || !IsSortTypeSetted)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderBy(x => x.BookDescriptionCatalog.Header);
                    if (SortByIsbn)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderBy(x => x.BookDescriptionCatalog.ISBN);
                    if (SortByPrice)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderBy(x => x.PartnerPrice);
                    if (SortByPublisher)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderBy(x => x.BookDescriptionCatalog.BookPublisher.Name);
                    if (SortByPartner)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderBy(x => x.Partner.Name);
                    if (SortByAuthor)
                        _search =
                            result.SelectMany(x => x.BookSaleCatalogs).OrderBy(
                                x =>
                                x.BookDescriptionCatalog.BookAuthorsRels.Any()
                                    ? x.BookDescriptionCatalog.BookAuthorsRels.First().Author.FIO
                                    : "");
                }
                else
                {
                    if (SortByHeader || !IsSortTypeSetted)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderByDescending(x => x.BookDescriptionCatalog.Header);
                    if (SortByIsbn)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderByDescending(x => x.BookDescriptionCatalog.ISBN);
                    if (SortByPrice)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderByDescending(x => x.PartnerPrice);
                    if (SortByPublisher)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderByDescending(x => x.BookDescriptionCatalog.BookPublisher.Name);
                    if (SortByPartner)
                        _search = result.SelectMany(x => x.BookSaleCatalogs).OrderByDescending(x => x.Partner.Name);
                    if (SortByAuthor)
                        _search =
                            result.SelectMany(x => x.BookSaleCatalogs).OrderByDescending(
                                x =>
                                x.BookDescriptionCatalog.BookAuthorsRels.Any()
                                    ? x.BookDescriptionCatalog.BookAuthorsRels.First().Author.FIO
                                    : "");
                }
            }
            //SearchedCount = _search.Count();
            return _search;
        }
    }
}