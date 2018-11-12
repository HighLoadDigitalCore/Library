using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Linq;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Xml;
using System.Xml.Serialization;
using ICSharpCode.SharpZipLib.Zip;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;

namespace Sprinter.Models
{
    public delegate void OnOrderFormedDelegate(int current, int total);

    public class YmlExporter
    {
        public OnOrderFormedDelegate OnOrderFormed { get; set; }

        private IQueryable<BookSaleCatalog> _exportQuery;
        public YmlExporter(IQueryable<BookSaleCatalog> query)
        {
            _exportQuery = query;
        }

        public YmlCatalog CreateYmlCatalog()
        {
            var catalog = new YmlCatalog()
            {
                Shop =
                    new Shop(SiteSetting.Get<string>("Yml.Name"), SiteSetting.Get<string>("Yml.Company"),
                             AccessHelper.SiteUrl, _exportQuery.Select(x => x.ID).Distinct().ToList())
            };

            var catList =
                _exportQuery.SelectMany(x => x.BookPageRels).Select(x => x.CMSPage).Where(x => x.URL != "catalog").
                    Distinct().Select(
                        x => new Category() { Id = x.ID, Name = x.PageName, ParentId = x.ParentID.ToString() }).ToList();

            foreach (var category in catList.Where(category => catList.All(x => x.Id != int.Parse(category.ParentId))))
            {
                category.ParentId = null;
            }
            catalog.Shop.Categories = new Collection<Category>(catList.OrderBy(x => x.ParentId).ThenBy(x => x.Id).ToList());

            catalog.Shop.Currency =
                new Collection<Currency>(new[] { new Currency() { Id = "USD", Rate = "CBRF" }, new Currency() { Id = "RUR", Rate = "1.00" } });
            //catalog.Shop.DataSource = _exportQuery;
            catalog.Shop.OnOrderFormed = OnOrderFormed;

            return catalog;
        }

        public string ExportToFile(YmlCatalog catalog, bool zipped = true)
        {

            Stream stream = null;

            var rnd = new Random(DateTime.Now.Millisecond);
            var path = HttpContext.Current.Server.CreateDir("/Temp/YmlExport/");
            string fileName;
            string onlyName;
            do
            {
                onlyName = "export_" + rnd.Next(100000, 999999) + "_" + DateTime.Now.ToString("ddMMyyyy");
                fileName = onlyName + (zipped ? ".zip" : ".yml");
            } while (File.Exists(path + fileName));
            var fs = new FileStream(path + fileName, FileMode.Create, FileAccess.Write, FileShare.None);

            if (!zipped)
                stream = fs;
            else
            {
                var zfs = new ZipOutputStream(fs);
                zfs.SetLevel(9);
                zfs.UseZip64 = UseZip64.Off;
                zfs.IsStreamOwner = true;
                var entry = new ZipEntry(onlyName + ".yml") { DateTime = DateTime.Now };
                zfs.PutNextEntry(entry);
                stream = zfs;
            }






            var settings = new XmlWriterSettings
                {
                    Indent = true
                };
            using (var writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteDocType("yml_catalog", null, "shops.dtd", null);
                var nsSerializer = new XmlSerializerNamespaces();
                nsSerializer.Add("", "");
                var serializer = new XmlSerializer(typeof(YmlCatalog), "");
                serializer.Serialize(writer, catalog, nsSerializer);
            }

            if (zipped)
            {
                ((ZipOutputStream)stream).CloseEntry();
            }
            stream.Close();
            return string.Format("{0}/Temp/YmlExport/{1}", AccessHelper.SiteUrl, fileName);
        }
    }

    [Serializable, XmlRoot("yml_catalog")]
    public class YmlCatalog
    {
        [XmlAttribute(AttributeName = "date")]
        public string Date { get; set; }

        [XmlElement(ElementName = "shop")]
        public Shop Shop { get; set; }

        public YmlCatalog()
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            /*
                        Namespaces = new XmlSerializerNamespaces();
                        Namespaces.Add("", "");
            */
        }

        /*
                [XmlNamespaceDeclarations]
                public XmlSerializerNamespaces Namespaces { get; set; }
        */
    }

    [Serializable]
    public class Shop
    {
        private DB _db;
        private List<int> DataSource { get; set; }

        [XmlIgnore]
        public OnOrderFormedDelegate OnOrderFormed { get; set; }

        [XmlElement(ElementName = "name", Order = 1)]
        public string Name { get; set; }

        [XmlElement(ElementName = "company", Order = 2)]
        public string Company { get; set; }

        [XmlElement(ElementName = "url", Order = 3)]
        public string Url { get; set; }

        [XmlArray(ElementName = "currencies", Order = 4)]
        [XmlArrayItem(ElementName = "currency")]
        public Collection<Currency> Currency { get; set; }

        [XmlArray(ElementName = "categories", Order = 5)]
        [XmlArrayItem(ElementName = "category")]
        public Collection<Category> Categories { get; set; }

        private const int POTION_AMOUNT = 250;

        private Collection<Offer> _offers;
        [XmlArray(ElementName = "offers", Order = 10)]
        [XmlArrayItem(ElementName = "offer")]
        public Collection<Offer> Offers
        {
            get
            {
                if (_offers == null)
                {
                    _db = new DB();
                    var dlo = new DataLoadOptions();
                    dlo.LoadWith<BookSaleCatalog>(x => x.BookDescriptionCatalog);
                    //dlo.LoadWith<BookDescriptionCatalog>(x => x.BookAuthorsRels);
                    //dlo.LoadWith<BookDescriptionCatalog>(x => x.AuthorsByComma);
                    dlo.LoadWith<BookAuthorsRel>(x => x.Author);
                    dlo.LoadWith<BookSaleCatalog>(x => x.BookPageRels);
                    dlo.LoadWith<BookPageRel>(x => x.CMSPage);
                    dlo.LoadWith<BookDescriptionCatalog>(x => x.BookPublisher);
                    dlo.LoadWith<BookPublisher>(x => x.BookPublisherMargins);
                    _db.LoadOptions = dlo;


                    int startOffset = 0;
                    _offers = new Collection<Offer>();
                    int current = 0;
                    IEnumerable<int> potion;
                    do
                    {
                        potion = DataSource.Skip(startOffset).Take(POTION_AMOUNT).ToList();

                        if (!potion.Any())
                            break;

                        var sales = _db.BookSaleCatalogs.Where(x => potion.Contains(x.ID)).ToList();
                        var bids = sales.Select(x => x.DescriptionID).ToList();
                        var authors =
                            _db.BookAuthorsRels.Where(x => bids.Contains(x.BookDescriptionID)).ToList().GroupBy(
                                x => x.BookDescriptionID).ToDictionary(x => x.Key, y => y);
                        foreach (var sale in sales)
                        {
                            _offers.Add(new Offer(sale,
                                                  authors.ContainsKey(sale.DescriptionID)
                                                      ? authors[sale.DescriptionID].Select(x => x.Author)
                                                      : new List<Author>()));
                            current++;
                            if (OnOrderFormed != null)
                            {
                                OnOrderFormed(current, DataSource.Count);
                            }
                        }

                        startOffset += POTION_AMOUNT;

                    } while (true);
                }
                return _offers;
            }
        }


        public Shop(string name, string company, string url, List<int> ds)
        {
            Name = name;
            Company = company;
            Url = url;
            DataSource = ds;
        }

        public Shop()
        {

        }

    }

    /*    [Serializable]
        [XmlRoot(ElementName = "categories")]
        public class Categories
        {
            [XmlElement(ElementName = "category")]
            private Collection<Category> CategoryList { get; set; }
            public Categories()
            {
                CategoryList = new Collection<Category>();
            }
        }*/

    [Serializable]
    public class Currency
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "rate")]
        public string Rate { get; set; }

        public Currency()
        {
            Rate = "1";
            Id = "RUR";
        }
    }

    [XmlRoot(ElementName = "category")]
    public class Category
    {
        [XmlAttribute(AttributeName = "id")]
        public int Id { get; set; }

        [XmlAttribute(AttributeName = "parentId")]
        public string ParentId { get; set; }

        [XmlText]
        public string Name { get; set; }

        public Category()
        {
            Id = 0;
        }
    }

    [XmlRoot(ElementName = "offer")]
    public class Offer
    {
        [XmlAttribute(AttributeName = "id")]
        public long ID { get; set; }

        [XmlAttribute(AttributeName = "available")]
        public bool Available { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlElement(ElementName = "url", Order = 1)]
        public string Url { get; set; }

        [XmlElement(ElementName = "price", Order = 2)]
        public decimal Price { get; set; }

        [XmlElement(ElementName = "name", Order = 3)]
        public string Name { get; set; }

        [XmlElement(ElementName = "currencyId", Order = 4)]
        public string Currency { get; set; }

        [XmlElement(ElementName = "categoryId", Order = 5)]
        public Collection<int> CategoryID { get; set; }

        [XmlElement(ElementName = "picture", Order = 6)]
        public string Picture { get; set; }

        [XmlElement(ElementName = "publisher", Order = 7)]
        public string Publisher { get; set; }

        [XmlElement(ElementName = "author", Order = 8)]
        public string Author { get; set; }

        [XmlElement(ElementName = "year", Order = 9)]
        public string Year { get; set; }

        [XmlElement(ElementName = "ISBN", Order = 10)]
        public string ISBN { get; set; }

        [XmlElement(ElementName = "binding", Order = 11)]
        public string Binding { get; set; }

        [XmlElement(ElementName = "page_extent", Order = 12)]
        public string PageExtent { get; set; }

        /*
                [XmlElement(ElementName = "delivery")]
                public bool Delivery { get; set; }
        */

        /*
                [XmlArray(ElementName = "orderingTime"), XmlArrayItem(ElementName = "ordering")]
                public Collection<string> OrderingTime { get; set; }
        */


        /*
                [XmlElement(ElementName = "vendor")]
                public string Vendor { get; set; }
        */

        [XmlElement(ElementName = "description", Order = 13)]
        public string Description { get; set; }

        public Offer(BookSaleCatalog sale, IEnumerable<Author> authors)
        {
            ID = sale.ID;
            Price = sale.TradingPrice;
            Available = sale.IsAvailable;
            Type = "book";
            Url = AccessHelper.SiteUrl + sale.URL;
            Currency = "RUR";
            CategoryID = new Collection<int>(sale.BookPageRels.Select(x => x.PageID).ToList());
            Picture = AccessHelper.SiteUrl + sale.CoverURL;


            Name = sale.BookDescriptionCatalog.Header.ClearHTML();
            Description = sale.BookDescriptionCatalog.Annotation.ClearHTML();

            if (sale.BookDescriptionCatalog.PublisherID.HasValue)
                Publisher = sale.BookDescriptionCatalog.BookPublisher.Name;
            if (sale.BookDescriptionCatalog.PublishYear != null)
                Year = sale.BookDescriptionCatalog.PublishYear.Value.ToString();
            if (sale.BookDescriptionCatalog.ISBN.IsFilled())
                ISBN = sale.BookDescriptionCatalog.ISBN;
            if (sale.BookDescriptionCatalog.PageCount != null)
                PageExtent = sale.BookDescriptionCatalog.PageCount.Value.ToString();
            if (sale.BookDescriptionCatalog.BookType.IsFilled())
                Binding = sale.BookDescriptionCatalog.BookType;
            Author = string.Join(", ", authors.Select(x => x.FIO));
            //sale.BookDescriptionCatalog.AuthorsByComma;
        }

        public Offer()
        {
            Price = 0;
        }
    }
}