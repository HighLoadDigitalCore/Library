

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Routing;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;

namespace Sprinter.Models
{

    public class BookAdditionalParams
    {
        public int? Pages { get; set; }
        public int? Year { get; set; }
        public string Format { get; set; }
        public string Series { get; set; }
        public string Details { get; set; }
        public string AdditionalDetails { get; set; }
        public string Stamp { get; set; }
        public string ForLearning { get; set; }
        public string PublishingType { get; set; }
        public string Apps { get; set; }
        public decimal? Weight { get; set; }
        public string Language { get; set; }
        public string ShortDescription { get; set; }
        public string Illustrations { get; set; }
        public string SecondPublisher { get; set; }
        public BookDescriptionCatalog DescriptionCatalog { get; set; }
    }

    public partial class BookDescriptionCatalog
    {
        public int AverageRounded
        {
            get { return (int)Math.Round(Average ?? 0, 0); }
        }



        const decimal DefWeight = (decimal)0.3;
        const decimal PageWeight = (decimal)2;

        public decimal BookWeight
        {
            get
            {
                if (!PageCount.HasValue)
                    return DefWeight;
                return PageCount.Value * PageWeight;
            }
        }

        public string SprinterCode
        {
            get
            {
                if (DataSourceID == 10/*Импортировано из спринтера*/)
                    return ProviderUID.ToInt().ToString("d7");
                return "S" + ID.ToString("d7");
            }
        }


        public string BookCatalogPath
        {
            get
            {
                return BookSaleCatalogs.Any()
                           ? BookSaleCatalogs.First().BookCatalogPath
                           : AccessHelper.IsMasterPage ? "--Не назначено--" : Header;
            }
        }

        public string HeaderWithAuthors
        {
            get { return "{0}{2}{1}".FormatWith(AuthorsByComma, Header, AuthorsByComma.IsFilled() ? " - " : ""); }
        }

        public string SEOAnnotation
        {
            get
            {
                return
                    "<i>В нашем  книжном интернет магазине «Спринтер» Вы можете купить</i> {3}{0} {1}<br>{2}".FormatWith(
                        Header, AuthorsByComma, Annotation, GoodType);
            }
        }

        public string GoodType
        {
            get
            {
                if (BookSaleCatalogs.Any() && BookSaleCatalogs.First().PartnerID <= 16)
                    return "книгу ";
                return "";
            }
        }

        public string AuthorsFIOByComma
        {
            get
            {
                var rx = new Regex(@"[А-ЯA-Z]{1}\.\,?");
                var rxFio = new Regex(@"[А-ЯA-Z]{1}[a-zа-я]+");

                return string.Join(", ", BookAuthorsRels.Select(x => x.Author.FIO)
                                                        .ToList()
                                                        .Select(x => rx.Replace(x, ""))
                                                        .SelectMany(x => x.Split<string>(" ", ","))
                                                        .Where(x => rxFio.IsMatch(x)));
            }
        }

        public void ChangeAuthorsList(List<string> list)
        {
            var _db = new DB();
            var authList = list.Select(x => new { Key = x, Value = _db.Authors.FirstOrDefault(z => z.FIO == x) });
            var deleted =
                _db.BookAuthorsRels.Where(x => x.BookDescriptionID == ID).AsEnumerable().Where(
                    x => !authList.Where(z => z.Value != null).Select(z => z.Value.FIO).Contains(x.Author.FIO));
            _db.BookAuthorsRels.DeleteAllOnSubmit(deleted);
            _db.SubmitChanges();

            var inserted =
                authList.Select(x => x.Key).Except(
                    _db.BookAuthorsRels.Where(x => x.BookDescriptionID == ID).Select(x => x.Author.FIO).AsEnumerable());

            foreach (string a in inserted)
            {
                var exist = _db.Authors.FirstOrDefault(x => x.FIO.ToLower() == a.ToLower());
                if (exist == null)
                {
                    exist = new Author { FIO = a };
                    _db.Authors.InsertOnSubmit(exist);
                }
                _db.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel { Author = exist, BookDescriptionID = ID });
            }
            _db.SubmitChanges();

        }

        public void ChangePublisher(string publisher)
        {
            var db = new DB();
            var dbp = db.BookPublishers.FirstOrDefault(x => x.Name == publisher);
            if (dbp == null)
            {
                dbp = new BookPublisher { Name = publisher };
                db.BookPublishers.InsertOnSubmit(dbp);
            }
            var item = db.BookDescriptionCatalogs.First(x => x.ID == ID);
            item.BookPublisher = dbp;
            db.SubmitChanges();

            db.BookPublishers.DeleteAllOnSubmit(db.BookPublishers.Where(x => !x.BookDescriptionCatalogs.Any()));
            db.SubmitChanges();

        }

        private string _authorsByComma;
        public string AuthorsByComma
        {
            get
            {
                if (_authorsByComma.IsNullOrEmpty())
                    _authorsByComma = string.Join(", ", BookAuthorsRels.Select(x => x.Author.FIO));
                return _authorsByComma;
            }
        }

        private BookAdditionalParams _additionalParams;
        public BookAdditionalParams AdditionalParams
        {
            get
            {
                if (_additionalParams == null)
                {
                    _additionalParams = new BookAdditionalParams();
                    foreach (var value in BookDescriptionDataValues)
                    {
                        _additionalParams.SetPropertyValueByString(value.BookDescriptionDataKey.DataKey, value.DataValue);
                    }
                    _additionalParams.DescriptionCatalog = this;
                    _additionalParams.Pages = PageCount;
                    _additionalParams.Year = PublishYear;
                    _additionalParams.Format = BookType;
                }
                return _additionalParams;
            }
        }

        public void SaveParams()
        {
            SaveParams(AdditionalParams);
        }

        public string GetParamDescription(string key)
        {
            var dataKey = new DB().BookDescriptionDataKeys.FirstOrDefault(x => x.DataKey == key);
            if (dataKey != null)
                return dataKey.Description;
            return "";
        }

        public void SaveParams(BookAdditionalParams bookParams)
        {
            var forSkip = new[] { "Pages", "Year", "Format" };
            var names = bookParams.GetPropertyNameList();
            var db = new DB();
            var book = db.BookDescriptionCatalogs.First(x => x.ID == ID);
            foreach (var name in names)
            {
                if (forSkip.Contains(name)) continue;
                var value = bookParams.GetPropertyValue(name);
                if (value != null && value.ToString().IsFilled())
                {
                    var prop = book.BookDescriptionDataValues.FirstOrDefault(x => x.BookDescriptionDataKey.DataKey == name);
                    if (prop == null)
                    {
                        var key = db.BookDescriptionDataKeys.First(x => x.DataKey == name);
                        prop = new BookDescriptionDataValue()
                            {
                                BookDescriptionCatalog = book,
                                BookDescriptionDataKey = key,
                                DataValue = value.ToString()
                            };
                        db.BookDescriptionDataValues.InsertOnSubmit(prop);
                    }
                    else
                    {
                        prop.DataValue = value.ToString();
                    }
                }
                else
                {
                    var prop = book.BookDescriptionDataValues.FirstOrDefault(x => x.BookDescriptionDataKey.DataKey == name);
                    if (prop != null)
                        db.BookDescriptionDataValues.DeleteOnSubmit(prop);
                }
            }

            book.PublishYear = bookParams.Year;
            book.PageCount = bookParams.Pages;
            book.BookType = bookParams.Format;

            db.SubmitChanges();
        }
    }

    [MetadataType(typeof(BooksOnMainAnnotation))]
    public partial class BooksOnMain
    {
        public class BooksOnMainAnnotation
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [Range(1, int.MaxValue, ErrorMessage = "Поле '{0}' должно быть больше нуля")]
            [DisplayName("Номер в списке")]
            public int OrderNum { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [Range(1, int.MaxValue, ErrorMessage = "Поле '{0}' должно быть больше нуля")]
            [DisplayName("ID товара в каталоге")]
            public int SaleCatalogID { get; set; }
        }
    }

    public partial class BookTag
    {
        public string TagFilterURL
        {
            get
            {
                var info = AccessHelper.CurrentPageInfo;
                if (info == null)
                    return "#";
                return string.Format("{0}?tags={1}", info.CurrentPage.FullUrl, ID);
            }
        }
    }

    public partial class TagComplexFilterList
    {
        public string TagList
        {
            get { return string.Join(", ", TagComplexFilterItems.Select(x => x.BookTag.Tag)); }
        }

        public SelectList DropDownItems
        {
            get
            {

                var selected = TagComplexFilterItems.FirstOrDefault(x => SelectedItemsFromRequest.Contains(x.TagID));
                var list =
                    TagComplexFilterItems.OrderBy(x => x.BookTag.Tag).Select(
                        x => new KeyValuePair<int, string>(x.TagID, x.BookTag.Tag)).ToList();

                if (!DefaultValue.IsNullOrEmpty())
                    list.Insert(0, new KeyValuePair<int, string>(0, DefaultValue));

                return new SelectList(list, "Key", "Value", selected != null ? selected.TagID : 0);

            }
        }



        public static List<int> SelectedItemsFromRequest
        {
            get
            {
                var list = new List<int>();
                if (!HttpContext.Current.Request["tags"].IsNullOrEmpty())
                {
                    list =
                        HttpContext.Current.Request["tags"].Split(new string[] { "," },
                                                                  StringSplitOptions.RemoveEmptyEntries).Select(
                                                                      x => x.ToInt()).ToList();
                }
                return list;
            }
        }
    }

    [MetadataType(typeof(TagSimpleFilterAnnotation))]
    public partial class TagSimpleFilter
    {
        private string _tagList;
        public string TagList
        {
            get
            {
                if (_tagList.IsNullOrEmpty())
                {
                    _tagList = string.Join(", ", TagSimpleFilterItems.Select(x => x.BookTag.Tag));
                }
                return _tagList;
            }
            set { _tagList = value; }
        }

        public class TagSimpleFilterAnnotation
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Название")]
            public string Name { get; set; }

            [DisplayName("Отображать на сайте")]
            public bool Visible { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Список тегов")]
            public string TagList { get; set; }
        }

    }

    [MetadataType(typeof(TagComplexFilterAnnotation))]
    public partial class TagComplexFilter
    {
        public class TagComplexFilterAnnotation
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Название")]
            public string Name { get; set; }

            [DisplayName("Отображать на сайте")]
            public bool Visible { get; set; }
        }


        public List<TagComplexFilterList> FilterList
        {
            get
            {
                int counter = 10 - TagComplexFilterLists.Count;
                var list = new List<TagComplexFilterList>();
                list.AddRange(TagComplexFilterLists);
                for (int i = 0; i < counter; i++)
                {
                    list.Add(new TagComplexFilterList() { ComplexFilterID = ID });
                }
                return list;
            }
        }
    }

    public partial class BookPublisher
    {
        public decimal GetMargin(int partnerID)
        {
            if (BookPublisherMargins.Any(x => x.PartnerID == partnerID))
                return BookPublisherMargins.First(x => x.PartnerID == partnerID).Margin ?? 0;
            return 0;
        }

        public decimal GetDiscount(int partnerID)
        {
            if (BookPublisherMargins.Any(x => x.PartnerID == partnerID))
                return BookPublisherMargins.First(x => x.PartnerID == partnerID).Discount ?? 0;
            return 0;
        }
    }


    [MetadataType(typeof(PartnerAnnotation))]
    public partial class Partner
    {
        public class PartnerAnnotation
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Название")]
            public decimal Description { get; set; }            
            
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Наценка")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal Margin { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Скидка от поставщика")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal Discount { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Приоритет поставщика")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public int SalePriority { get; set; }

            [DisplayName("Продавать товары этого поставщика")]
            public bool Enabled { get; set; }


        }



        private IQueryable<BookPublisher> _publisherList;
        public IQueryable<BookPublisher> PublisherList
        {
            get
            {
                if (_publisherList == null)
                {
                    DB db = new DB();
                    _publisherList = db.BookPublishers.Where(
                        x => x.BookDescriptionCatalogs.SelectMany(z => z.BookSaleCatalogs).Any(c => c.PartnerID == ID)).OrderBy(x => x.Name);
                }
                return _publisherList;
            }
        }

        public int PublisherCount
        {
            get { return PublisherList.Count(); }
        }
    }

    public partial class BookCover
    {
        public int getProperWidth(int width)
        {
            if (Width < width) return Width;
            return width;
        }
        public int getProperHeight(int width)
        {
            if (Width <= width)
                return Height;

            return (int)((((decimal)width / (decimal)Width)) * Height);
        }
    }



    public partial class BookSaleCatalog
    {
        public class BookCountData
        {
            public int PageID { get; set; }
            public int ParentID { get; set; }
            public string Url { get; set; }
            public string FullUrl { get; set; }
            public int BookCount { get; set; }
            public int SectionCount { get; set; }
        }

        public string SprinterCode
        {
            get { return BookDescriptionCatalog.SprinterCode; }
        }


        public static List<BookCountData> _bookCountList;
        public static List<BookCountData> BookCountList
        {
            get
            {
                return new List<BookCountData>();
                /*if (_bookCountList == null)*/
                {
                    var bcl = HttpRuntime.Cache.Get("BookCountList");
                    if (bcl != null && bcl is List<BookCountData>)
                        _bookCountList = bcl as List<BookCountData>;
                    else
                    {
                        DB db = new DB();
                        _bookCountList = db.getSectionList(1).Select(
                            x =>
                            new BookCountData()
                                {
                                    PageID = x.ID ?? 0,
                                    ParentID = x.ParentID ?? 0,
                                    FullUrl = x.FullURL,
                                    Url = x.URL,
                                    BookCount = x.BookTotalCount ?? 0,
                                    SectionCount = x.SectionCount ?? 0
                                }).ToList();

                        HttpRuntime.Cache.Insert("BookCountList",
                                                 _bookCountList,
                                                 new SqlCacheDependency("Sprinter", "BookPageRels"),
                                                 DateTime.Now.AddDays(1D),
                                                 Cache.NoSlidingExpiration);
                        /*HttpRuntime.Cache.Add("BookCountList", _bookCountList, null,
                                         Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(20),
                                         CacheItemPriority.NotRemovable, null);*/
                    }

                }
                return _bookCountList;
            }
        }

        public static readonly Expression<Func<BookSaleCatalog, decimal>> TradingPriceExpr =
            x => x.Partner == null
                     ? 0
                     : ((x.PriceOverride.HasValue && x.PriceOverride > 0)
                            ? x.PriceOverride.Value
                            : x.PartnerPrice * (100 +
                                              (x.Margin > 0
                                                   ? x.Margin
                                                   : (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                                      x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                                          z => z.PartnerID == x.PartnerID) &&
                                                      x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                                          z => z.PartnerID == x.PartnerID).Margin.HasValue
                                                          ? (x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins
                                                                .First(
                                                                    z => z.PartnerID == x.PartnerID).Margin.Value)
                                                          : (x.Partner.Margin)))

                                              - (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                                 x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                                     z => z.PartnerID == x.PartnerID) &&
                                                 x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                                     z => z.PartnerID == x.PartnerID).Discount.HasValue
                                                     ? x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First
                                                           (
                                                               z => z.PartnerID == x.PartnerID).Discount.Value
                                                     : x.Partner.Discount)) / 100);

        public static readonly Expression<Func<BookSaleCatalog, decimal>> OriginalTradingPriceExpr =
            x => x.Partner == null
                     ? 0
                     : (x.PartnerPrice * (100 +
                                        (x.Margin > 0
                                             ? x.Margin
                                             : (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                                x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                                    z => z.PartnerID == x.PartnerID) &&
                                                x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                                    z => z.PartnerID == x.PartnerID).Margin.HasValue
                                                    ? (x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First
                                                          (
                                                              z => z.PartnerID == x.PartnerID).Margin.Value)
                                                    : (x.Partner.Margin)))

                                        - (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                           x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                               z => z.PartnerID == x.PartnerID) &&
                                           x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                               z => z.PartnerID == x.PartnerID).Discount.HasValue
                                               ? x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                                   z => z.PartnerID == x.PartnerID).Discount.Value
                                               : x.Partner.Discount)) / 100);
        /*
                public static readonly Expression<Func<BookSaleCatalog, decimal>> TradingPriceExpr =
                    x => x.PartnerPrice * (100 + Math.Max(x.Partner.Margin, x.Margin) - x.Partner.Discount) / 100;
        */

        /*
                public static readonly Expression<Func<BookSaleCatalog, bool>> IsOverPricedExpr =
                    x => (x.PartnerPrice * (100 + Math.Max(x.Partner.Margin, x.Margin) - x.Partner.Discount) / 100) >
                         (x.BookDescriptionCatalog.BookPrices.Any()
                              ? x.BookDescriptionCatalog.BookPrices.Min(y => y.Price)
                              : decimal.MaxValue);
        */

        public static readonly Expression<Func<BookSaleCatalog, bool>> IsOverPricedExpr =
            x =>
            (x.PartnerPrice *
             (100 +
              (x.Margin > 0
                   ? x.Margin
                   : (x.BookDescriptionCatalog.PublisherID.HasValue &&
                      x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                          z => z.PartnerID == x.PartnerID)
                          ? (x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                              z => z.PartnerID == x.PartnerID).Margin)
                          : (x.Partner.Margin)))
              - (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                    x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                        z => z.PartnerID == x.PartnerID) &&
                                    x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                        z => z.PartnerID == x.PartnerID).Discount.HasValue
                                        ? x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                            z => z.PartnerID == x.PartnerID).Discount.Value
                                        : x.Partner.Discount)) / 100) >
            (x.BookDescriptionCatalog.BookPrices.Any()
                 ? x.BookDescriptionCatalog.BookPrices.Min(y => y.Price)
                 : decimal.MaxValue);

        public static readonly Expression<Func<BookSaleCatalog, bool>> IsMaxOverPricedExpr =
            x => x.PartnerPrice * (100 +
                                 (x.Margin > 0
                                      ? x.Margin
                                      : (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                         x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                             z => z.PartnerID == x.PartnerID)
                                             ? (x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                                 z => z.PartnerID == x.PartnerID).Margin)
                                             : (x.Partner.Margin)))

                                 - (x.BookDescriptionCatalog.PublisherID.HasValue &&
                                    x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.Any(
                                        z => z.PartnerID == x.PartnerID) &&
                                    x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                        z => z.PartnerID == x.PartnerID).Discount.HasValue
                                        ? x.BookDescriptionCatalog.BookPublisher.BookPublisherMargins.First(
                                            z => z.PartnerID == x.PartnerID).Discount.Value
                                        : x.Partner.Discount)) / 100 >
                 (x.BookDescriptionCatalog.BookPrices.Any()
                      ? x.BookDescriptionCatalog.BookPrices.Max(y => y.Price)
                      : decimal.MaxValue);

        /*
                public static readonly Expression<Func<BookSaleCatalog, bool>> IsMaxOverPricedExpr =
                    x => x.PartnerPrice * (100 + Math.Max(x.Partner.Margin, x.Margin) - x.Partner.Discount) / 100 >
                         (x.BookDescriptionCatalog.BookPrices.Any()
                              ? x.BookDescriptionCatalog.BookPrices.Max(y => y.Price)
                              : decimal.MaxValue);

        */

        private static readonly Func<BookSaleCatalog, decimal> TradingPriceCompile = TradingPriceExpr.Compile();
        private static readonly Func<BookSaleCatalog, decimal> OriginalTradingPriceCompile = OriginalTradingPriceExpr.Compile();

        public string BookIdentifier
        {
            get
            {
                if (!BookDescriptionCatalog.ISBN.IsNullOrEmpty()) return BookDescriptionCatalog.ISBN;
                if (BookDescriptionCatalog.EAN > 0) return BookDescriptionCatalog.EAN.ToString();
                return BookDescriptionCatalog.ProviderUID;
            }
        }

        public decimal OriginalTradingPrice
        {
            get
            {
                return OriginalTradingPriceCompile(this);
            }
        }
        public decimal TradingPrice
        {
            get
            {
                return TradingPriceCompile(this);
            }
        }


        public decimal MaxCompetitorPrice
        {
            get
            {
                var prices = BookDescriptionCatalog.BookPrices;
                if (prices.Any())
                    return prices.Max(x => x.Price);
                return decimal.MaxValue;
            }
        }

        public decimal MinCompetitorPrice
        {
            get
            {
                var prices = BookDescriptionCatalog.BookPrices;
                if (prices.Any())
                    return prices.Min(x => x.Price);
                return decimal.MaxValue;
            }
        }

        public bool IsMaxOverPriced
        {
            get { return TradingPrice > MaxCompetitorPrice; }
        }


        public bool IsOverPriced
        {
            get
            {
                var expr = IsOverPricedExpr.Compile();
                return expr(this);
            }
        }

        public string PriceForClient
        {
            get
            {
                if (IsMaxOverPriced) return "";
                var tradingPrice = TradingPrice;
                if (Math.Floor(tradingPrice) != TradingPrice)
                    return Math.Round(tradingPrice, 1).ToString("f1");
                return ((int)tradingPrice).ToString();
            }
        }

        public string BookCatalogClientLinkedPath
        {
            get
            {
                CMSPage page = null;
                var info = AccessHelper.CurrentPageInfo;
                if (AccessHelper.IsMasterPage)
                {
                    if (BookPageRels.Any())
                        page = BookPageRels.First().CMSPage;
                }
                else if (info.CurrentPage.URL == "catalog")
                {
                    page = BookPageRels.Any() ? BookPageRels.First().CMSPage : info.CurrentPage;
                }
                else
                    page = info.CurrentPage;

                if (page == null) return BookDescriptionCatalog.Header;

                return string.Format("{0} &mdash; {1}", page.LinkedBreadCrumbs, BookDescriptionCatalog.Header);
            }
        }
        public string BookCatalogPath
        {
            get
            {
                CMSPage page = null;
                var info = AccessHelper.CurrentPageInfo;
                if (AccessHelper.IsMasterPage)
                {
                    if (BookPageRels.Any())
                        page = BookPageRels.First().CMSPage;
                }
                else if (info.CurrentPage.URL == "catalog")
                {
                    page = BookPageRels.Any() ? BookPageRels.First().CMSPage : info.CurrentPage;
                }
                else
                    page = info.CurrentPage;

                if (page == null) return AccessHelper.IsMasterPage ? "--Не назначено--" : BookDescriptionCatalog.Header;

                if (AccessHelper.IsMasterPage)
                    return page.BreadCrumbs;
                return string.Format("{0} &mdash; {1}", page.BreadCrumbs, BookDescriptionCatalog.Header);
            }
        }

        private string _url;
        public string URL
        {
            get
            {
                CommonPageInfo info = AccessHelper.CurrentPageInfo;
                if (info!=null)
                {
                    
                    if (info.TagFilter != null && info.TagFilter.Any())
                    {
                        if (_url.IsNullOrEmpty())
                            _url = string.Format("{0}?book={1}", BookPageRels.First().CMSPage.FullUrl, ID);
                    }
                    else
                    {
                        if (_url.IsNullOrEmpty())
                        {
                            if (info.CurrentPage != null && info.CurrentPage.PageType.TypeName == "Catalog")
                            {
                                _url = string.Format("{0}?book={1}", info.CurrentPage.FullUrl, ID);
                            }
                            else
                            {
                                if (BookPageRels.Any())
                                    _url = string.Format("{0}?book={1}", BookPageRels.First().CMSPage.FullUrl, ID);
                                else
                                {
                                    var page = CMSPage.FullPageTable.FirstOrDefault(
                                        x =>
                                        x.Type == 1 && x.URL != "catalog");

                                    _url = string.Format("{0}?book={1}", page.FullUrl, ID);
                                }

                            }
                        }
                    }
                }
                else
                {
                    _url = string.Format("{0}?book={1}", BookPageRels.First().CMSPage.FullUrl, ID);
                }
                return _url;
            }
        }

        public string GetPreview(int width)
        {
            return "/Master/Files/BookPicture/bookID/{0}/width/{1}".FormatWith(ID.ToString(), width.ToString());
        }
        public string ThumbURL
        {
            get { return "/Master/Files/BookPicture/bookID/{0}/width/118".FormatWith(ID.ToString()); }
        }

        public string CoverURL
        {
            get
            {
                return "/Master/Files/BookPicture/bookID/{0}/width/236".FormatWith(ID.ToString());
            }
        }

        public string CategoryName
        {
            get { return BookPageRels.Any() ? BookPageRels.First().CMSPage.PageName : "--Категория не назначена--"; }
        }

        public decimal PartnerPriceWithDiscount
        {
            get
            {
                if (Partner == null) return 0;
                return PartnerPrice * (100 - Partner.Discount) / 100;
            }
        }

        public decimal TradingMargin
        {
            get
            {
                if (Partner == null)
                    return 0;
                if (Margin > 0) return Margin;
                var publisherMargin = BookDescriptionCatalog.BookPublisher.GetMargin(PartnerID);
                if (publisherMargin > 0)
                    return publisherMargin;
                return Partner.Margin;
            }
        }

        public bool CanBeSold
        {
            get { return IsAvailable && TradingPrice > 0 && !IsMaxOverPriced; }
        }
    }

    public class CMSPageComparer : IEqualityComparer<CMSPage>
    {
        public bool Equals(CMSPage x, CMSPage y)
        {
            return x.ID == y.ID;
        }

        public int GetHashCode(CMSPage obj)
        {
            return obj.ID.GetHashCode();
        }
    }


}