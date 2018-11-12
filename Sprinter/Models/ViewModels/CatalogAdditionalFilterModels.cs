using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Sprinter.Extensions;

namespace Sprinter.Models.ViewModels
{

    public class CatalogOverPriceFilter : IAdditionalFilter
    {
        private bool overpriced;
        public CatalogOverPriceFilter(bool overpriced)
        {
            this.overpriced = overpriced;
        }
        public IQueryable<BookSaleCatalog> Filter(IQueryable<BookSaleCatalog> primarySource)
        {
            //вырубаем нахуй печать по требованию из проверки
            var overPriced =
                primarySource.Where(x => x.Partner.Enabled && x.PartnerID!=16).GroupBy(x => x.DescriptionID).
                    Select(x => new {Priority = x.Max(z => z.Partner.SalePriority), Item = x})
                    .Select(x => x.Item.First(z => z.Partner.SalePriority == x.Priority)).Where(overpriced
                                                                                                    ? BookSaleCatalog.
                                                                                                          IsMaxOverPricedExpr
                                                                                                    : BookSaleCatalog.
                                                                                                          IsOverPricedExpr)
                    .
                    OrderBy(
                        x => x.BookDescriptionCatalog.Header);
            return overPriced;
        }

        public RouteValueDictionary Routes
        {
            get
            {
                var list = new RouteValueDictionary();
                list.Add("overpriced", overpriced);
                return list;
            }
        }
    }

    public class CatalogDistributionFilter : IAdditionalFilter
    {


        public int ViewMode { get; private set; }
        public SelectList ViewModes { get; private set; }
        public string SectionPath { get; private set; }
        public SelectList Sections { get; private set; }
        public CMSPage CatalogPage { get; private set; }


        DB db = new DB();

        public CatalogDistributionFilter(int? viewMode, string section, IQueryable<BookSaleCatalog> primarySource)
        {
            ViewMode = viewMode ?? 1;
            SectionPath = section;

            var items = new List<SelectListItem>
                            {
                                new SelectListItem {Text = "Новые, нераспределенные записи", Value = "1"},
                                new SelectListItem {Text = "Записи, распределенные по категориям", Value = "2"}
                            };

            ViewModes = new SelectList(items, "Value", "Text", ViewMode);

            List<KeyValuePair<string, string>> sections = null;
            if (ViewMode == 1)
            {
                var dbsections =
                    primarySource.Where(x => x.Partner.Enabled && x.IsAvailable && !x.BookPageRels.Any()).Select(
                        x =>
                        x.BookDescriptionCatalog.OriginalSectionPath.Length > 0
                            ? x.BookDescriptionCatalog.OriginalSectionPath
                            : "--Раздел не определен--").
                        Distinct().OrderBy(x => x);
                sections = dbsections.Select(x => new KeyValuePair<string, string>(x, x)).ToList();
            }
            else
                sections = primarySource.Where(x => x.Partner.Enabled && x.BookPageRels.Any()).Select(x =>
                                                                                       x.BookPageRels.First().CMSPage
                    ).Select(x => x.ID).Distinct().AsEnumerable().Select(x => createPair(x)).OrderBy(x => x.Value).ToList();

            sections.Insert(0, new KeyValuePair<string, string>("Все разделы", "Все разделы"));
            if (SectionPath.IsNullOrEmpty()) SectionPath = "Все разделы";

            Sections = new SelectList(sections, "Key", "Value", SectionPath);

            CatalogPage = db.CMSPages.FirstOrDefault(x => x.URL == "catalog" && x.PageType.TypeName == "Catalog");
            if (CatalogPage == null)
                throw new Exception("Должна быть создана страница каталога (URL = catalog)");

        }

        private KeyValuePair<string, string> createPair(int pageID)
        {
            var page = CMSPage.FullPageTable.FirstOrDefault(x => x.ID == pageID);

            var names = string.Join(" --> ",
                                    page.UrlPath.Select(x => CMSPage.FullPageTable.First(y => y.URL == x).PageName).
                                        ToArray());

            return new KeyValuePair<string, string>(page.ID.ToString(), names);

        }

        private IQueryable<BookSaleCatalog> _filtered;
        public IQueryable<BookSaleCatalog> Filter(IQueryable<BookSaleCatalog> primarySource)
        {
            if (_filtered == null)
            {
                var entryes = primarySource.Where(x => x.Partner.Enabled);
                if (ViewMode == 1)
                {
                    entryes = entryes.Where(x => !x.BookPageRels.Any());
                    if (SectionPath == "--Раздел не определен--")
                        entryes = entryes.Where(x => x.BookDescriptionCatalog.OriginalSectionPath.IsNullOrEmpty());
                    else if (SectionPath != "Все разделы")
                        entryes = entryes.Where(x => x.BookDescriptionCatalog.OriginalSectionPath == SectionPath);
                }
                else
                {
                    entryes = entryes.Where(x => x.BookPageRels.Any());
                    if (SectionPath != "Все разделы")
                    {
                        int pageID = 0;
                        if (int.TryParse(SectionPath, out pageID) && pageID > 0)
                            entryes = entryes.Where(x => x.BookPageRels.Any(y => y.CMSPage.ID == pageID));
                    }
                }
                _filtered = entryes;
            }
            return _filtered;
        }

        public RouteValueDictionary Routes
        {
            get
            {
                var list = new RouteValueDictionary();
                list.Add("ViewMode", ViewMode);
                list.Add("SectionPath", SectionPath);
                return list;
            }
        }
    }
}