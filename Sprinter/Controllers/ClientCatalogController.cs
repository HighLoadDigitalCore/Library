using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Data.Linq.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class ClientCatalogController : Controller
    {

        private Random _rnd;
        private Random RND
        {
            get { return _rnd ?? (_rnd = new Random(DateTime.Now.Millisecond)); }
        }


        private static DataLoadOptions LoadOptions
        {
            get
            {
                var dlo = new DataLoadOptions();
                dlo.LoadWith<BookSaleCatalog>(x => x.BookDescriptionCatalog);
                dlo.LoadWith<BookSaleCatalog>(x => x.BookPageRels);
                dlo.LoadWith<BookSaleCatalog>(x => x.Partner);
                dlo.LoadWith<Partner>(x => x.PartnerPriorities);
                dlo.LoadWith<BookDescriptionCatalog>(x => x.BookPublisher);
                dlo.LoadWith<BookPublisher>(x => x.BookPublisherMargins);
                return dlo;
            }
        }

        private readonly DB db = new DB() { LoadOptions = LoadOptions };
        private readonly CommonPageInfo info = AccessHelper.CurrentPageInfo;

        [HttpPost]
        public ContentResult SaveMark(int mark, int book)
        {
            if (mark < 1 || mark > 5)
                return new ContentResult() { Content = "-1" };
            var av = db.BookDescriptionCatalogs.FirstOrDefault(x => x.ID == book);
            if (!av.TotalSum.HasValue)
                av.TotalSum = 0;
            if (!av.TotalCount.HasValue)
                av.TotalCount = 0;
            if (!av.Average.HasValue)
                av.Average = 0;
            av.TotalSum += mark;
            av.TotalCount++;
            av.Average = (decimal)av.TotalSum / (decimal)av.TotalCount;
            db.SubmitChanges();
            return new ContentResult() { Content = av.AverageRounded.ToString() };
        }

        [HttpGet]
        public PartialViewResult TagComplexFilter()
        {
            return PartialView(info.CurrentPage.TagComplexFilters.FirstOrDefault());
        }


        [HttpGet]
        public PartialViewResult CatalogSearch(int? page)
        {
            const int COUNT_FOR_EACH_CATEGORY = 1000;

            var search = new CommonSearch();
            long ean = 0;
            long.TryParse(EAN13.IsbnToEan13(search.SearchQuery), out ean);

            long sid = 0;
            long.TryParse(search.SearchQuery, out sid);

            IQueryable<BookSaleCatalog> union;

            var isbnResut =
                db.BookDescriptionCatalogs.Where(
                    x =>
                    x.ISBN == search.SearchQuery || (ean > 0 && x.EAN == ean) || (sid > 0
                                                                                      ? (x.DataSourceID == 10
                                                                                             ? x.ProviderUID ==
                                                                                               sid.ToString()
                                                                                             : x.ID == sid)
                                                                                      : false))
                  .SelectMany(x => x.BookSaleCatalogs).Take(100).ToList();

            if (isbnResut.Any())
                union = isbnResut.AsQueryable();

            else
            {
                var nameResult =
                    db.getBooksByAnnotation(search.SearchQuery, 1, 0)
                      .OrderByDescending(x => x.Rank)
                      .Take(COUNT_FOR_EACH_CATEGORY)
                      .Join(db.BookDescriptionCatalogs, x => x.ID, y => y.ID, (x, y) => y)
                      .SelectMany(x => x.BookSaleCatalogs)
                      .ToList().Select((x,index)=> new {Key=x, OrderNum = index}).OrderBy(x=> x.Key.BookDescriptionCatalog.CoverID.HasValue?0:1).ThenBy(x=> x.OrderNum).Select(x=> x.Key);
                var authorsResult =
                    db.Authors.Where(x => SqlMethods.Like(x.FIO, string.Format("{0}%", search.SearchQuery.Trim())))
                      .SelectMany(x => x.BookAuthorsRels)
                      .SelectMany(x => x.BookDescriptionCatalog.BookSaleCatalogs).Take(COUNT_FOR_EACH_CATEGORY).ToList();

                union = authorsResult.Union(nameResult).ToList().AsQueryable();
            }

            IQueryable<CMSPage> pages = null;
            if (search.SectionID > 0)
            {
                var pg = CMSPage.FullPageTable.FirstOrDefault(x => x.ID == search.SectionID);
                if (pg != null)
                    pages =
                        db.getIntListByJoinedString(string.Join(";", pg.FullChildrenList.ToArray()), ";")
                          .Join(db.CMSPages, x => x.ID, y => y.ID, (x, y) => y).Take(COUNT_FOR_EACH_CATEGORY);
            }

            if (pages != null)
                union = union.Where(x => x.BookPageRels.Any(z => pages.Any(c => c.ID == z.PageID)));

            union = union.Where(x => x.Partner.Enabled && x.IsAvailable && x.PartnerPrice > 0 && x.BookPageRels.Any());

            union = union.GroupBy(x => x.DescriptionID).
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
                         .Select(
                             x =>
                             x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()
                                 )
                              .FirstOrDefault(z => z.Partner.SalePriority == x.Priority))
                         .Where(x => x != null)/*
                         .OrderBy(x => x.BookDescriptionCatalog.CoverID.HasValue ? 0 : 1)
                         .ThenBy(x => x.BookDescriptionCatalog.Header)*/;
            var routes = info.Routes;
            routes.Add("search", Microsoft.JScript.GlobalObject.escape(search.SearchQuery));
            routes.Add("section", search.SectionID ?? 0);

            return PartialView(new PagedData<BookSaleCatalog>(union, page ?? 0, 24, "Default", routes));
        }

        [HttpGet]
        public PartialViewResult BooksPopularList(int? type, bool? viewOnBookPage, int? bookID)
        {
            var subPages =
                CMSPage.FullPageTable.Where(
                    x =>
                    ((viewOnBookPage ?? false)
                         ? CMSPage.FullPageTable.Where(z => z.ParentID == info.CurrentPage.ParentID).Select(y => y.ID)
                         : info.CurrentPage.ShortChildrenList(5)).
                        Contains(x.ID)).Take(5).ToList();

            switch (type)
            {
                //сортируем по популярности все в подкатегориях
                case 0:
                default:

                    var count =
                        subPages.Sum(z => z.ActiveCount);
                    if (count <= 20)
                    {
                        subPages =
                            CMSPage.FullPageTable.Where(
                                x =>
                                (CMSPage.FullPageTable.FirstOrDefault(z => z.ID == info.CurrentPage.ParentID) ??
                                 info.CurrentPage).ShortChildrenList(5).Contains(x.ID)).Take(5).ToList();
                    }



                    var subPagesIDs = subPages.Select(x => x.ID).ToList();
                    if (bookID.HasValue && db.BookSaleCatalogs.Any(x => x.ID == bookID))
                    {
                        var descrEntry = db.BookSaleCatalogs.First(x => x.ID == bookID).BookDescriptionCatalog.ID;

                        Random random = new Random(DateTime.Now.Millisecond);
                        int seed = random.Next(1000, 9999);
                        //
                        var descrList =
                            db.CMSPages.Where(x => subPagesIDs.Contains(x.ID)).SelectMany(x => x.BookPageRels).Select(x => x.BookSaleCatalog.BookDescriptionCatalog).Where(x => x.ID != descrEntry).OrderByDescending(x => x.Average ?? 0).Take(150).
                            SelectMany(x => x.BookSaleCatalogs).
                                Where(x =>
                                      x.IsAvailable && x.Partner.Enabled && x.PartnerPrice > 0 &&
                                      x.BookPageRels.Any(z => subPagesIDs.Contains(z.PageID))
                                ).Select(x => x.BookDescriptionCatalog).OrderBy(s => (~(s.ID & seed)) & (s.ID | seed)).Select(x => x.ID).Take(12).ToList();

                        var list = db.BookDescriptionCatalogs.Where(x => descrList.Contains(x.ID))
                       .SelectMany(x => x.BookSaleCatalogs).GroupBy(x => x.DescriptionID).
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
                       .Select(x => x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()).FirstOrDefault(z => z.Partner.SalePriority == x.Priority)).Where(x => x != null).Take(12)
                       .ToList();/*
                        var list = db.BookSaleCatalogs.Where(x => x.DescriptionID != descrEntry).Where(
                            x =>
                            x.PartnerPrice > 0 && x.IsAvailable && x.Partner.Enabled && x.BookPageRels.Any(s => subPagesIDs.Contains(s.PageID))
                            ).
                            OrderByDescending(
                                x => (x.BookDescriptionCatalog.Average??0)).Take(12).GroupBy(x => x.DescriptionID).
                            Select(
                                x =>
                                new
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
                            .Select(x => x.Item.First(z => z.Partner.SalePriority == x.Priority))
                            .ToList();*/

                        return PartialView(list.OrderBy(x => RND.Next()));


                    }
                    else
                    {
                        Random random = new Random(DateTime.Now.Millisecond);
                        int seed = random.Next(1000, 9999);
                        //
                        var descrList =
                            db.CMSPages.Where(x => subPagesIDs.Contains(x.ID)).SelectMany(x => x.BookPageRels).Select(x => x.BookSaleCatalog.BookDescriptionCatalog).OrderByDescending(x => x.Average ?? 0).Take(150).
                            SelectMany(x => x.BookSaleCatalogs).
                                Where(x =>
                                      x.IsAvailable && x.Partner.Enabled && x.PartnerPrice > 0 &&
                                      x.BookPageRels.Any(z => subPagesIDs.Contains(z.PageID))
                                ).Select(x => x.BookDescriptionCatalog).OrderBy(s => (~(s.ID & seed)) & (s.ID | seed)).Select(x => x.ID).Take(12).ToList();
                        var list = db.BookDescriptionCatalogs.Where(x => descrList.Contains(x.ID))
                            .SelectMany(x => x.BookSaleCatalogs).GroupBy(x => x.DescriptionID).
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
                            .Select(x => x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()).First(z => z.Partner.SalePriority == x.Priority)).Where(x => x != null).Take(12)
                            .ToList();
                        return PartialView(list.OrderBy(x => RND.Next()));

                    }
                    break;

                //похожие книги
                case 1:
                    var similarBooks =
                        db.getSimilarBooks(bookID.Value, 20, 1).OrderByDescending(x => x.Rating).Join(
                            db.BookDescriptionCatalogs, x => x.ID, y => y.ID, (x, y) => y).SelectMany(
                                x => x.BookSaleCatalogs).Where(
                                    x => x.BookPageRels.Any() && x.IsAvailable && x.Partner.Enabled).GroupBy(
                                        x => x.DescriptionID).
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
                          .Select(
                              x =>
                              x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any())
                               .FirstOrDefault(z => z.Partner.SalePriority == x.Priority))
                          .Where(x => x != null)
                          .Take(12);
                    return PartialView(similarBooks);
                    break;
            }
        }

        [HttpGet]
        public PartialViewResult TagCloud()
        {
            return PartialView(new CatalogTagCloudViewModel(info.CurrentPage));
        }


        [HttpGet]
        public PartialViewResult TagSimpleFilter()
        {
            return PartialView(info.CurrentPage.TagSimpleFilters.FirstOrDefault());
        }


        [HttpGet]
        public ActionResult CatalogFilteredByTags(int? page)
        {
            ViewBag.BaseURL = string.Format("{0}/", info.CurrentPage.FullUrl);

            var tags = db.BookTags.Where(x => info.TagFilter.Contains(x.ID));

            var books =
                db.BookDescriptionCatalogs.Where(
                    x =>
                    x.BookTagRels.Select(y => y.BookTag).Intersect(tags).Count() == info.TagFilter.Count &&
                    x.BookSaleCatalogs.Any()).SelectMany(x => x.BookSaleCatalogs).Where(
                        x => x.IsAvailable && x.PartnerPrice > 0 && x.Partner.Enabled && x.BookPageRels.Any())
                  .GroupBy(x => x.DescriptionID)
                  .
                   Select(
                       x => new
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
                  .Select(
                      x =>
                      x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()).FirstOrDefault(z => z.Partner.SalePriority == x.Priority))
                  .Where(x => x != null)
                  .OrderBy(x => x.BookDescriptionCatalog.CoverID.HasValue ? 0 : 1).ThenBy(
                      x => x.BookDescriptionCatalog.Header);



            var routes = info.Routes;
            routes.Add("tags", string.Join(",", info.TagFilter));
            return PartialView(new PagedData<BookSaleCatalog>(books, page ?? 0, 24, "Default", routes));
        }

        public ActionResult CatalogDetails(int? book)
        {
            if (!book.HasValue)
                book = info.Routes.ContainsKey("bookId") ? info.Routes["bookId"].ToString().ToInt() : 0;
            if (info.CurrentBook != null && info.CurrentBook.ID == book)
                return PartialView(info.CurrentBook);
            var dbBook = db.BookSaleCatalogs.FirstOrDefault(x => x.ID == book);
            return PartialView(dbBook);
        }

        public ActionResult CatalogSpecPage(string view, int? page)
        {
            ViewBag.BaseURL = string.Format("{0}/", info.CurrentPage.FullUrl);

            IQueryable<BookSaleCatalog> specList;
            switch (view)
            {
                case "novelty":
                default:
                    specList = db.BookSaleCatalogs.Where(
                        x =>
                        x.IsAvailable && x.PartnerPrice > 0 && x.Partner.Enabled && x.IsNew);
                    break;


            }

            specList = specList.GroupBy(x => x.DescriptionID).Select(x => new
                {
                    Priority =
                                                                              x.Min(
                                                                                  z =>
                                                                                  z.BookPageRels.First().CMSPage.
                                                                                      PartnerPriorities.Any(
                                                                                          v =>
                                                                                          v.PartnerID == z.PartnerID)
                                                                                      ? z.BookPageRels.First().CMSPage.
                                                                                            PartnerPriorities.First(
                                                                                                v =>
                                                                                                v.PartnerID ==
                                                                                                z.PartnerID)
                                                                                            .
                                                                                            Priority
                                                                                      : z.Partner.SalePriority),
                    Item = x
                })
                .Select(x => x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()).FirstOrDefault(z => z.Partner.SalePriority == x.Priority)).Where(x => x != null)
                                 .OrderBy(x => x.BookDescriptionCatalog.CoverID.HasValue ? 0 : 1).ThenBy(
                    x => x.BookDescriptionCatalog.Header);
            var routes = info.Routes;
            routes.Add("view", view);
            return PartialView(new PagedData<BookSaleCatalog>(specList, page ?? 0, 24, "Default", routes));

        }
        public ActionResult CatalogSection(int? page)
        {
            ViewBag.BaseURL = string.Format("{0}/", info.CurrentPage.FullUrl);


            var books = db.BookSaleCatalogs.Where(
                x =>
                x.IsAvailable && x.PartnerPrice > 0 && x.Partner.Enabled && x.BookPageRels.Any(y => y.PageID == info.ID))
                .GroupBy(x => x.DescriptionID).Select(x => new
                    {
                        Priority =
                                                               x.Min(
                                                                   z =>
                                                                   z.BookPageRels.First().CMSPage.PartnerPriorities.Any(
                                                                       v => v.PartnerID == z.PartnerID)
                                                                       ? z.BookPageRels.First().CMSPage.
                                                                             PartnerPriorities.First(
                                                                                 v => v.PartnerID == z.PartnerID).
                                                                             Priority
                                                                       : z.Partner.SalePriority),
                        Item = x
                    })
                .Select(x => x.Item.Where(z => z.PartnerPrice > 0 && z.Partner.Enabled && z.IsAvailable && z.BookPageRels.Any()).FirstOrDefault(z => z.Partner.SalePriority == x.Priority)).Where(x => x != null)
                                 .OrderBy(x => x.BookDescriptionCatalog.CoverID.HasValue ? 0 : 1).ThenBy(
                    x => x.BookDescriptionCatalog.Header);


            var routes = info.Routes;
            return PartialView(new PagedData<BookSaleCatalog>(books, page ?? 0, 24, "Default", routes));
        }

        public ActionResult CatalogSectionList()
        {
            return PartialView(new CatalogSectionViewModel(info.CurrentPage));
        }

    }
}
