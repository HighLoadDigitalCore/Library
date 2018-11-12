using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;
using Sprinter.Extensions;

namespace Sprinter.Controllers
{
    public class ParserController : Controller
    {
        DB db = new DB();




        [HttpGet]
        [AuthorizeMaster]
        public ActionResult YMLImport()
        {

            return View(db.BookDescriptionProviders.Where(x => x.IsPriceProvider));
        }


        public ContentResult startYMLAsync(int arg)
        {
            ParseringInfo info = ParseringInfo.Create("YML");
            if (info.StartDate.HasValue)
                info = ParseringInfo.Reset("YML");
            info.Provider = db.BookDescriptionProviders.First(x => x.ID == arg);
            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));

            var workingThread = new Thread(ThreadFuncYML);
            workingThread.Start(System.Web.HttpContext.Current);
            return new ContentResult();

        }

        private static void ThreadFuncYML(object context)
        {
            var HttpContext = context as HttpContext;
            System.Web.HttpContext.Current = HttpContext;
            ParseringInfo info = ParseringInfo.Create("YML");
            var path = HttpContext.Current.Server.CreateDir("/Temp/YML/");

            WebUnzipper unzipper = new WebUnzipper(path, "YML", "");
            bool success = unzipper.GetFile();

            if (!success) return;


            string ymlPath = unzipper.ResultFileName;
            //ymlPath = @"D:\Sites\Sprinter\Sprinter\Sprinter\Temp\YML\partner_YML_availability_2.xml";

            DB db = new DB();
            Uri relative = new Uri(ymlPath);
            using (XmlReader reader = XmlReader.Create(relative.ToString(),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, IgnoreComments = true }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("yml_catalog");

                reader.ReadToDescendant("offer");

                reader.ReadElements("offer").AsParallel().WithDegreeOfParallelism(10).Select(x => ProcessYMLRecord(x, HttpContext)).Count();

            }

            info.EndDate = DateTime.Now;

            var provider = db.BookDescriptionProviders.FirstOrDefault(x => x.ID == info.Provider.ID);
            provider.LastUpdateDate = DateTime.Now;
            db.SubmitChanges();



            info.AddMessage(string.Format("Окончание обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            info.AddMessage(string.Format("Длительность - {0} минут", info.EndDate.Value.Subtract(info.StartDate.Value).TotalMinutes));
            System.IO.File.Delete(ymlPath);
        }


        public static bool ProcessYMLRecord(XElement offer, HttpContext context)
        {
            System.Web.HttpContext.Current = context;
            DB db = new DB();
            ParseringInfo info = ParseringInfo.Create("YML");
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                info.AddMessage("Обработка прервана.");
                var p = db.BookDescriptionProviders.FirstOrDefault(x => x.ID == info.Provider.ID);
                p.LastUpdateDate = DateTime.Now;
                db.SubmitChanges();

            }

            if ((string)offer.Attribute("type") == "book")
            {
                var isbnNode = offer.Element("ISBN");
                if (isbnNode == null) return true;

                string ISBN = isbnNode.Value.Trim();
                string sYear = "";
                if (offer.Element("year") != null)
                    sYear = offer.Element("year").Value.Trim();

                var isbnList = ISBN.Split(new string[] { ";", ",", " " }, StringSplitOptions.RemoveEmptyEntries).ToList();

                foreach (var isbn in isbnList)
                {
                    try
                    {

                        bool isNew = false;
                        var normalIsbn = EAN13.NormalizeIsbn(isbn);
                        if (normalIsbn.Replace("-", "").Length < 7) continue; //гавно какое-то а не ISBN

                        /*                  info.Updated++;
                                          return true;*/
                        /*
                                                info.AddMessage("Обработка книги с ISBN = " + normalIsbn);
                        */

                        if (normalIsbn.IsNullOrEmpty()) continue;
                        if (normalIsbn.Length > 13)
                            normalIsbn = normalIsbn.Substring(0, 13);

                        var ean = long.Parse(EAN13.IsbnToEan13(normalIsbn));
                        if (ean == 0) continue;
                        BookDescriptionCatalog book;

                        book =
                            db.BookDescriptionCatalogs.FirstOrDefault(
                                x =>
                                (x.EAN == ean) && x.Header.ToLower() == offer.Element("name").Value.Trim().ToLower());


                        if (book == null)
                        {
                            continue;

                            //Книги, которых нет в каталоге пропускаем
                            /*                            isNew = true;
                                                        book = new BookDescriptionCatalog()
                                                        {
                                                            DataSourceID = info.Provider.ID,
                                                            ProviderUID = (string)offer.Attribute("id"),
                                                            ISBN = normalIsbn,
                                                            EAN = ean,
                                                            PublishYear = (sYear.IsNullOrEmpty() ? (int?)null : int.Parse(sYear))
                                                        };
                                                        db.BookDescriptionCatalogs.InsertOnSubmit(book);
                                                        book.Header = offer.Element("name").Value.Trim();*/


                        }

                        book.ISBN = EAN13.NormalizeIsbn(book.ISBN);

                        var puplisherNode = offer.Element("publisher");
                        if (puplisherNode != null && book.BookPublisher == null)
                        {
                            string publisher = puplisherNode.Value.Trim();
                            var dbPub = db.BookPublishers.FirstOrDefault(x => x.Name == publisher);
                            if (dbPub == null)
                            {
                                dbPub = new BookPublisher() { Name = publisher };
                                db.BookPublishers.InsertOnSubmit(dbPub);
                            }
                            book.BookPublisher = dbPub;
                        }

                        if (sYear.ToInt() > 0 && !book.PublishYear.HasValue)
                            book.PublishYear = sYear.ToInt();

                        var descrNode = offer.Element("description");
                        if (descrNode != null && (book.Annotation.IsNullOrEmpty() || book.Annotation.Length < descrNode.Value.Trim().Length))
                            book.Annotation = descrNode.Value.Trim();

                        var authNode = offer.Element("author");
                        if (authNode != null && !book.BookAuthorsRels.Any())
                        {
                            var authors = authNode.Value.Split(new string[] { ",", ";" },
                                                               StringSplitOptions.
                                                                   RemoveEmptyEntries);

                            foreach (var author in authors)
                            {
                                if (author.Length >= 2 &&
                                    author[0].ToString().ToUpper() == author[0].ToString())
                                {
                                    var dbAuthor = db.Authors.FirstOrDefault(x => x.FIO == author);
                                    if (dbAuthor == null)
                                    {
                                        dbAuthor = new Author { FIO = author };
                                        db.Authors.InsertOnSubmit(dbAuthor);

                                    }
                                    db.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel
                                    {
                                        Author = dbAuthor,
                                        BookDescriptionCatalog = book
                                    });
                                }
                            }
                        }


                        var pageNode = offer.Element("page_extent");
                        if (pageNode != null && !book.PageCount.HasValue)
                        {
                            int count = 0;
                            if (int.TryParse(pageNode.Value, out count))
                                book.PageCount = count;
                        }

                        if (info.Provider.LoadCoversOnDemand &&
                            !((book.CoverID.HasValue && (book.DataSourceID == info.Provider.ID || book.DataSourceID == 10))))
                        {
                            try
                            {
                                var coverNode = offer.Element("picture_big") ?? offer.Element("picture");
                                if (coverNode != null)
                                {
                                    string coverURL = coverNode.Value.Trim();
                                    WebClient client = new WebClient();
                                    byte[] imgData = client.DownloadData(coverURL);
                                    MemoryStream ms = new MemoryStream(imgData);
                                    Image bitmap = Image.FromStream(ms);

                                    if (!book.CoverID.HasValue)
                                    {
                                        BookCover cover = new BookCover
                                        {
                                            Data = imgData,
                                            Name = coverURL,
                                            Height = bitmap.Height,
                                            Width = bitmap.Width
                                        };
                                        db.BookCovers.InsertOnSubmit(cover);
                                        book.BookCover = cover;

                                    }
                                    else if (book.BookCover.Width < bitmap.Width)
                                    {
                                        book.BookCover.Data = imgData;
                                        book.BookCover.Name = coverURL;
                                        book.BookCover.Width = bitmap.Width;
                                        book.BookCover.Height = bitmap.Height;
                                    }
                                    bitmap.Dispose();
                                }
                            }
                            catch (Exception e)
                            {
                                info.AddMessage(e.Message);
                                //info.AddMessage(e.StackTrace);
                            }
                        }

                        string link = offer.Element("url").Value;

                        if (isNew)
                        {
                            info.Created++;
                            db.BookPrices.InsertOnSubmit(new BookPrice()
                            {
                                BookDescriptionCatalog = book,
                                ProviderID = info.Provider.ID,
                                Price =
                                    ImportData.ParsePrice(
                                        (string)offer.Element("price")),
                                Link = link
                            });
                        }
                        else
                        {

                            var dbPrice =
                                db.BookPrices.FirstOrDefault(
                                    x => x.DescriptionID == book.ID && x.ProviderID == info.Provider.ID);
                            if (dbPrice == null)
                            {
                                dbPrice = new BookPrice()
                                {
                                    BookDescriptionCatalog = book,
                                    ProviderID = info.Provider.ID,
                                    Price = ImportData.ParsePrice((string)offer.Element("price")),
                                    Link = link
                                };
                                db.BookPrices.InsertOnSubmit(dbPrice);
                                info.Created++;
                            }
                            else
                            {
                                info.Updated++;
                                dbPrice.Price = ImportData.ParsePrice((string)offer.Element("price"));
                                dbPrice.Link = link;
                            }
                        }

                        db.SubmitChanges();
                    }
                    catch (Exception e)
                    {
                        info.AddMessage(e.Message);
                        info.AddMessage(offer.ToString());
                        info.AddMessage(e.StackTrace);
                        info.Errors++;
                    }
                }

            }
            return true;

        }


        public ContentResult stopYMLAsync(int arg)
        {
            ParseringInfo info = ParseringInfo.Create("YML");
            info.Break = true;
            return new ContentResult();
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Index()
        {
            return View();
        }

        #region URAIT
        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Urait()
        {
            return View(ParseringInfo.Create("urait"));
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Urait(FormCollection collection)
        {
            ParseringInfo info = ParseringInfo.Create("urait");
            if (info.StartDate.HasValue && !info.EndDate.HasValue)
            {
                info.Break = true;
                return View(info);
            }
            if (info.EndDate.HasValue)
                info = ParseringInfo.Reset("urait");

            info.Provider = db.BookDescriptionProviders.First(x => x.ProviderName == "Urait");
            info.StartDate = DateTime.Now;
            info.AddMessage(string.Format("Запуск обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));


            var workingThread = new Thread(ThreadFunc);
            workingThread.Start(System.Web.HttpContext.Current);
            return View(info);
        }

        private static void ThreadFunc(object context)
        {
            System.Web.HttpContext.Current = context as HttpContext;
            var db = new DB();
            int providerID = db.BookDescriptionProviders.First(x => x.ProviderName == "Urait").ID;
            ProcessPage(new ThreadStartData() { Context = context as HttpContext, IsBook = false, ProviderID = providerID, URL = "/" });
            ParseringInfo info = ParseringInfo.Create("urait");
            info.EndDate = DateTime.Now;
            info.AddMessage(string.Format("Окончание обработки в {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm")));
            info.AddMessage(string.Format("Длительность - {0} минут", info.EndDate.Value.Subtract(info.StartDate.Value).TotalMinutes));
        }


        [HttpGet]
        [AuthorizeMaster]
        public JsonResult loadInfo(string name)
        {
            return new JsonResult() { JsonRequestBehavior = JsonRequestBehavior.AllowGet, Data = ParseringInfo.Create(name) };
        }



        private static bool ProcessPage(ThreadStartData data)
        {

            System.Web.HttpContext.Current = data.Context;
            ParseringInfo info = ParseringInfo.Create("urait");

            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                return true;
            }
            if (info.IsItemProcessed(data.URL, false)) return true;
            info.AddProcessedItem(data.URL, false);
            WebClient client = new WebClient();

            //            var path = HttpContext.Server.CreateDir("/Temp/Urait/");
            try
            {

                var html = client.DownloadString("http://urait-book.ru" + data.URL);
                Regex linkExpr =
                    new Regex(@"""(((http|https|ftp)://([\w-\d]+\.)+[\w-\d]+){0,1}(/[\w~,;\-\./?%&+#=]*))""",
                              RegexOptions.Multiline);


                var matches = linkExpr.Matches(html);
                var books = new List<string>();
                var pages = new List<string>();

                foreach (Match match in matches)
                {
                    var link = match.Captures[0].Value.Substring(1, match.Captures[0].Value.Length - 2);
                    if (link.Contains("catalog"))
                    {
                        if (link.EndsWith(".htm"))
                            books.Add(link);
                        else
                        {
                            Regex pagerEx = new Regex("PAGEN");
                            var pagerMathes = pagerEx.Matches(link);
                            if (pagerMathes.Count <= 1)
                            {
                                pagerEx = new Regex("SECTION_ID");
                                pagerMathes = pagerEx.Matches(link);
                                if (pagerMathes.Count <= 1)
                                {
                                    pagerEx = new Regex("mask=");
                                    pagerMathes = pagerEx.Matches(link);
                                    if (pagerMathes.Count <= 1)
                                    {
                                        pagerEx = new Regex("sort=");
                                        pagerMathes = pagerEx.Matches(link);
                                        if (pagerMathes.Count <= 1)
                                        {
                                            if (!link.Contains("#comments") && !link.Contains("basket.php") && !link.Contains("by=") && !link.Contains("page=") && !link.Contains("/catalog/?PAGEN_1") && !link.Contains("style.css"))
                                                pages.Add(link);
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
                info.AddMessage("Обработана страница каталога " + data.URL);
                info.Dirs++;

                var newBooks = books.Where(x => !info.IsItemProcessed(x, true)).ToList();
                var newPages = pages.Where(x => !info.IsItemProcessed(x, false)).ToList();

                newBooks.AsParallel().WithDegreeOfParallelism(15).Select(x => StartPageThread(new ThreadStartData() { Context = data.Context, URL = x, ProviderID = data.ProviderID, IsBook = true })).Count();
                newPages.AsParallel().WithDegreeOfParallelism(3).Select(x => StartPageThread(new ThreadStartData() { Context = data.Context, URL = x, ProviderID = data.ProviderID, IsBook = false })).Count();


                return true;

            }
            catch (Exception e)
            {
                info.AddMessage(e.Message);
                return false;
            }

        }


        public static bool StartPageThread(object data)
        {
            var typedData = data as ThreadStartData;
            System.Web.HttpContext.Current = typedData.Context;
            if (typedData.IsBook)
                return ProcessBook(typedData);
            else return ProcessPage(typedData);
        }

        private static bool ProcessBook(ThreadStartData data)
        {

            System.Web.HttpContext.Current = data.Context;
            ParseringInfo info = ParseringInfo.Create("urait");
            if (info.Break)
            {
                info.EndDate = DateTime.Now;
                return true;
            }

            if (info.IsItemProcessed(data.URL, true)) return true;
            info.AddProcessedItem(data.URL, true);
            WebClient client = new WebClient();

            Regex headEx = new Regex(@"<h1>(.+?)</h1>");
            Regex authorEx = new Regex(@"<div><span>Автор:</span>(.+?)</div>");
            Regex yearEx = new Regex(@"<div><span>Год выпуска:</span>(.+?)</div>");
            Regex pagesEx = new Regex(@"<div><span>Число страниц:</span>(.+?)</div>");
            Regex publisherEx = new Regex(@"<div><span>Издательство:</span>(.+?)</div>");
            Regex isbnEx = new Regex(@"<div><span>ISBN:</span>(.+?)</div>");
            Regex eanEx = new Regex(@"<div><span>EAN:</span>(.+?)</div>");
            Regex codeEx = new Regex(@"<div><span>Код:</span>(.+?)</div>");
            Regex bookTypeEx = new Regex(@"<div><span>Переплет:</span>(.+?)</div>");
            Regex annoEx = new Regex(@"<h2>Аннотация</h2>(\W)+p>(.+?)</p><h2>");
            Regex authorDescrEx = new Regex(@"<h2>Об авторе</h2>(\W)+p>(.+?)</p></div>");
            Regex pathEx = new Regex(@"Каталог книг</a>(\W)+td>([\W\w.^()]+?)</tr>");
            Regex pathTitleEx = new Regex(@"title=""(.+?)""");
            Regex coverPicEx = new Regex(@"<img[^>]+class=""pic""[^>]+src=""(.+?)""(.+?)/>");
            try
            {

                string html = client.DownloadString("http://urait-book.ru" + data.URL).Replace("\r", "").Replace("\n", "").Replace("\t", "");
                string code = codeEx.Match(html).Groups[1].Captures[0].Value.ClearHTML();
                string isbn = "";
                long iISBN = 0;
                var isbnMatch = isbnEx.Match(html);
                if (isbnMatch.Groups.Count > 1)
                {
                    isbn = EAN13.NormalizeIsbn(isbnMatch.Groups[1].Captures[0].Value.ClearHTML().Trim());
                    iISBN = long.Parse(EAN13.IsbnToEan13(isbn));
                }
                long iEan = 0;
                var eanMatch = eanEx.Match(html);
                if (eanMatch.Groups.Count > 1)
                {
                    long.TryParse(eanMatch.Groups[1].Captures[0].Value.ClearHTML(), out iEan);
                }
                DB db = new DB();
                var book = db.BookDescriptionCatalogs.FirstOrDefault(x => x.EAN == (iISBN > 0 ? iISBN : iEan));
                bool isNew = false;
                if (book == null)
                {

                    //если книга не создана в импорте, то нахуй
                    return true;


                    info.Created++;
                    isNew = true;
                    book = new BookDescriptionCatalog() { DataSourceID = data.ProviderID, ProviderUID = code, ISBN = isbn, EAN = (iISBN > 0 ? iISBN : iEan) };
                    db.BookDescriptionCatalogs.InsertOnSubmit(book);
                }
                else
                {
                    info.Updated++;
                }


                book.Header = headEx.Match(html).Groups[1].Captures[0].Value.Trim();
                var annoMatch = annoEx.Match(html);
                if (annoMatch.Groups.Count > 1)
                {
                    string annotation = annoMatch.Groups[2].Captures[0].Value.Trim();
                    if (book.Annotation.IsNullOrEmpty() || book.Annotation.Length < annotation.Length)
                        book.Annotation = annotation;
                }

                var authors =
                    authorEx.Match(html).Groups[1].Captures[0].Value.Replace("и др.", "").Replace("и др", "").Replace(
                        "сост.", "").Replace("Сост.", "").Replace("Отв.", "").Replace("отв.", "").Replace(
                            "Составитель", "").Replace("составитель", "")
                        .Replace("Под ", "").Replace("под ", "").Replace("гл.", "").Replace("Гл.", "").Replace("ред.",
                                                                                            "").Replace("Ред.", "").
                        Replace(
                            "Иллюстрации", "").Replace(
                            "П/р", "").Replace("- от", "").Replace("Редактор", "").Replace("редактор", "").Replace("редакцией", "").Replace("пер.", "").Replace("Пер.", "").Replace("Пер.", "").Replace("редакцией", "").Replace("с ан", "").Replace("с франц.", "").Replace("под/ред", "").
                        Split(new string[] { ",", " и " }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.ClearBorders()).Where(x => !x.IsNullOrEmpty()).
                        ToList();

                if (authors.Count == 1)
                {
                    var dbAuthor = db.Authors.FirstOrDefault(x => x.FIO == authors[0]);
                    if (dbAuthor == null)
                    {
                        dbAuthor = new Author { FIO = authors[0] };
                        var authorDescrMatch = authorDescrEx.Match(html);
                        if (authorDescrMatch.Groups.Count > 1)
                        {
                            string about = authorDescrMatch.Groups[2].Captures[0].Value.Trim();
                            if (dbAuthor.About.IsNullOrEmpty() || dbAuthor.About.Length < about.Length)
                                dbAuthor.About = about;
                        }
                        db.Authors.InsertOnSubmit(dbAuthor);

                    }
                    if (isNew)
                    {
                        db.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel { Author = dbAuthor, BookDescriptionCatalog = book });
                    }
                }
                else
                {
                    if (isNew)
                    {
                        foreach (string author in authors)
                        {
                            var dbAuthorEntity = db.Authors.FirstOrDefault(x => x.FIO == author);
                            if (dbAuthorEntity == null)
                            {
                                dbAuthorEntity = new Author { FIO = author };
                                db.Authors.InsertOnSubmit(dbAuthorEntity);
                            }
                            db.BookAuthorsRels.InsertOnSubmit(new BookAuthorsRel
                                                                  {
                                                                      Author = dbAuthorEntity,
                                                                      BookDescriptionCatalog = book
                                                                  });
                        }
                    }
                }

                if (isNew)
                {
                    string publisher = publisherEx.Match(html).Groups[1].Captures[0].Value.Trim();
                    var dbPub = db.BookPublishers.FirstOrDefault(x => x.Name == publisher);
                    if (dbPub == null)
                    {
                        dbPub = new BookPublisher() { Name = publisher };
                        db.BookPublishers.InsertOnSubmit(dbPub);
                    }
                    book.BookPublisher = dbPub;

                    string pathHtml = pathEx.Match(html).Groups[2].Captures[0].Value;
                    var titles = pathTitleEx.Matches(pathHtml);
                    string origPath = string.Join("/", titles.Cast<Match>().Select(x => x.Groups[1].Captures[0].Value.Trim()).ToArray());
                    book.OriginalSectionPath = origPath;
                }
                int year = 0;
                int.TryParse(yearEx.Match(html).Groups[1].Captures[0].Value.ClearHTML(), out  year);
                if (year > 0)
                    book.PublishYear = year;
                int count = 0;
                int.TryParse(pagesEx.Match(html).Groups[1].Captures[0].Value.ClearHTML(), out count);
                book.PageCount = count;
                book.BookType = bookTypeEx.Match(html).Groups[1].Captures[0].Value.ClearHTML();

                var coverMatch = coverPicEx.Match(html);
                if (coverMatch.Groups.Count > 1)
                {
                    string coverURL = coverMatch.Groups[1].Captures[0].Value.Trim();

                    if (!coverURL.Contains("nopic.gif") && (isNew || !book.CoverID.HasValue))
                    {
                        byte[] imgData = client.DownloadData("http://urait-book.ru" + coverURL);
                        MemoryStream ms = new MemoryStream(imgData);
                        Image bitmap = Image.FromStream(ms);
                        BookCover cover = new BookCover
                                              {
                                                  Data = imgData,
                                                  Name = coverURL,
                                                  Height = bitmap.Height,
                                                  Width = bitmap.Width
                                              };

                        bitmap.Dispose();
                        db.BookCovers.InsertOnSubmit(cover);
                        book.BookCover = cover;

                    }
                }
                info.AddMessage("Обработана страница книги " + data.URL);
                db.SubmitChanges();

            }
            catch (Exception e)
            {
                info.Errors++;
                info.AddMessage(e.Message);
                info.AddMessage(e.StackTrace);
                return false;
            }
            return true;
        }
        #endregion
    }
}
