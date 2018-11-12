using System;
using System.Collections.Generic;
using System.Linq;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;

namespace Sprinter.Models
{

    public class CatalogTagCloudItem
    {
        public BookTag Tag { get; set; }
        public int Size { get; set; }
        public int FontSize { get; set; }
        public string Link { get; set; }
    }

    public class CatalogTagCloudViewModel
    {
        private CommonPageInfo info = AccessHelper.CurrentPageInfo;
        private const int MinFont = 12;
        private const int MaxFont = 30;
        private const int MaxDist = 18;
        public IEnumerable<CatalogTagCloudItem> TagList { get; private set; }
        public CatalogTagCloudViewModel(CMSPage page)
        {
            DB db = new DB();
            //TagList = new List<CatalogTagCloudItem>();
            //return;
            var fullList = CMSPage.FullPageTable.Where(x => page.ShortChildrenList(10).Contains(x.ID)).Select(x=> x.ID).ToList();
            var tagList = db.BookSaleCatalogs.Where(x => x.BookPageRels.Any(z=> fullList.Contains(z.PageID)) && x.IsAvailable).SelectMany(
                                                         x => x.BookDescriptionCatalog.BookTagRels).GroupBy(x => x.TagID).OrderByDescending(x=> x.Count()).Take(50).AsEnumerable();


            Random rnd = new Random(DateTime.Now.Millisecond);

            if (tagList.Any())
            {

                int minSize = tagList.Min(x => x.Count());
                int maxSize = tagList.Max(x => x.Count());
                int dist = maxSize - minSize;
                if (dist == 0) dist = 1;
                TagList = tagList.OrderBy(x=> rnd.Next()).Select(x => new CatalogTagCloudItem()
                                                  {
                                                      Tag = x.First().BookTag,
                                                      Size = x.Count(),
                                                      FontSize =
                                                          (int)
                                                          ((decimal)(x.Count() - minSize) * (decimal)MaxDist /
                                                           (decimal)(dist)) +
                                                          MinFont,
                                                      Link = string.Format("{0}?tags={1}", info.CurrentPage.FullUrl, x.First().TagID)
                                                  });

            }
            else
            {
                TagList = new List<CatalogTagCloudItem>();
            }


        }
    }


    public class CatalogSectionViewModel
    {
        public IEnumerable<CatalogSectionList> FirstRow { get; private set; }
        public IEnumerable<CatalogSectionList> SecondRow { get; private set; }
        public CMSPage CurrentPage { get; private set; }
        public CatalogSectionViewModel(CMSPage page)
        {
            CurrentPage = page;
            var list =
                CMSPage.FullPageTable.Where(x => x.ParentID == page.ID).OrderBy(x => x.OrderNum).Select(
                    x => new CatalogSectionList(x)).Select(
                        (x, index) => new {Index = index, Value = x});

            FirstRow = list.Where(x => x.Index % 2 == 0).Select(x => x.Value).ToList();
            SecondRow = list.Where(x => x.Index % 2 == 1).Select(x => x.Value).ToList();

            if (FirstRow == null)
                FirstRow = new List<CatalogSectionList>();
            if (SecondRow == null)
                SecondRow = new List<CatalogSectionList>();

        }
        public CatalogSectionViewModel(int pageID)
            : this(new DB().CMSPages.FirstOrDefault(x => x.ID == pageID))
        {

        }
    }

    public class CatalogSectionList
    {
        public CatalogSectionList(CMSPage page)
        {
            CurrentPage = page;

            //Заменено на счетчики внутри БД
            //BookCount = BookSaleCatalog.BookCountList.First(x => x.PageID == page.ID).BookCount;
            BookCount = CurrentPage.ActiveCount;
        }
        public CatalogSectionList(int pageID)
            : this(CMSPage.FullPageTable.FirstOrDefault(x => x.ID == pageID))
        {
        }

        public string _sectionList;

        public string SectionList
        {
            get
            {
                if (_sectionList.IsNullOrEmpty())
                {
                    var pagesList =
                        CMSPage.FullPageTable.Where(x => x.ParentID == CurrentPage.ID && x.Visible && x.Type == 1);
                    _sectionList = string.Join(", ", pagesList.Select
                        (x => string.Format("<a href=\"/{0}\">{1}</a>", x.FullUrl, x.PageName)).ToArray())
                    ;
                }
                return _sectionList;
            }
        }

        /*
                private void calculateCount(ref int count, CMSPage page)
                {
                    count += page.BookPageRels.Count;
                    var subList = page.Children.Where(x => x.PageType.TypeName == "Catalog");
                    foreach (var cmsPage in subList)
                    {
                        calculateCount(ref count, cmsPage);
                    }
                }
        */

        public CMSPage CurrentPage { get; private set; }
        public int BookCount { get; private set; }


    }

}