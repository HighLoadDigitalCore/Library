using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class MainPageController : Controller
    {
        DB db = new DB();

        [HttpGet]
        public PartialViewResult Index()
        {
            return PartialView();
        }

        [HttpGet]
        public PartialViewResult LowerTabs()
        {
            return PartialView();
        }

        [HttpGet]
        public PartialViewResult UpperTabs()
        {
            var bigs = db.BooksOnMains.Where(x => x.GroupNum == 2).OrderBy(x => x.OrderNum);
            ViewBag.First = bigs.Any() ? bigs.First().BookSaleCatalog : null;
            ViewBag.Second = bigs.Count() > 1 ? bigs.Skip(1).First().BookSaleCatalog : null;
            return PartialView();
        }

        [HttpGet]
        public PartialViewResult BooksBlock(int? type)
        {
            IEnumerable<BookSaleCatalog> list;
            switch (type)
            {
                case 0:
                default:
                    list =
                        db.BookSaleCatalogs.Where(x => x.IsAvailable && x.BookDescriptionCatalog != null && x.BookPageRels.Any()).OrderByDescending(x => x.LastUpdate).Select(x => x.BookDescriptionCatalog).Distinct().
                            Select(x => x.BookSaleCatalogs.First()).Take(6);
                    break;

                case 1:
                case 2:
                case 3:
                    list =
                        db.BooksOnMains.Where(x => x.GroupNum == 1 && x.SubGroupNum == type).OrderBy(x => x.OrderNum).
                            Select(x => x.BookSaleCatalog);
                    break;

            }
            return PartialView(list);

        }

        [HttpGet]
        public PartialViewResult BooksBlockLower(int? type)
        {
            var list =
                db.BooksOnMains.Where(x => x.GroupNum == 3 && x.SubGroupNum == (type ?? 1)).OrderBy(x => x.OrderNum).
                    Select(x => x.BookSaleCatalog);
            return PartialView(list);
        }



        [HttpGet]
        [AuthorizeMaster]
        public ActionResult EditorLists()
        {
            return View();
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult EditTabs(int? tab)
        {
            ViewBag.Header = "Редактирование" + getHeaderGroup(tab);
            var items = db.BooksOnMains.Where(x => x.GroupNum == tab).OrderBy(x => x.SubGroupNum).ThenBy(x => x.OrderNum).AsEnumerable();
            ViewBag.SubGroupList = getSelectList(tab, 0);
            return View(items);
        }


        [HttpPost]
        [AuthorizeMaster]
        public ActionResult EditEntry(int? tab, int entryId, FormCollection collection)
        {
            BooksOnMain entry = db.BooksOnMains.FirstOrDefault(x => x.ID == entryId);
            if (entry == null)
            {
                entry = new BooksOnMain() { GroupNum = tab ?? 1 };
                db.BooksOnMains.InsertOnSubmit(entry);
            }
            UpdateModel(entry, new[] { "SaleCatalogID", "OrderNum", "SubGroupNum" });
            try
            {
                db.SubmitChanges();
            }
            catch (Exception e)
            {
                ModelState.AddModelError("", "Книга с таким идентификатором не найдена");
                return EditEntry(tab, entryId);
            }
            return RedirectToAction("EditTabs", new { tab = tab ?? 1 });
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult EditEntry(int? tab, int entryId)
        {
            ViewBag.Header = (entryId > 0 ? "Редактирование книги для" : "Добавление книги для") + getHeaderGroup(tab);

            var entry = db.BooksOnMains.FirstOrDefault(x => x.ID == entryId);
            if (entry == null)
            {
                entry = new BooksOnMain() { GroupNum = tab ?? 1, SubGroupNum = 1 };
            }
            ViewBag.SubGroupList = getSelectList(tab, entry.SubGroupNum);
            return View(entry);
        }

        private SelectList getSelectList(int? tab, int num)
        {
            SelectList list;
            switch (tab)
            {
                case 1:
                default:

                    list =
                        new SelectList(new[]
                                           {
                                               new {Key = "1", Value = "Бестселлеры"},
                                               new {Key = "2", Value = "Популярные"},
                                               new {Key = "3", Value = "Распродажа"}
                                           }, "Key", "Value", num);
                    break;
                case 3:
                    list =
                        new SelectList(new[]
                                           {
                                               new {Key = "1", Value = "Учебники"},
                                               new {Key = "2", Value = "Развивающие игрушки"},
                                               new {Key = "3", Value = "Спортивные товары"},
                                               new {Key = "4", Value = "Программное обеспечение"}
                                           }, "Key", "Value", num);
                    break;
            }
            return list;
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult DeleteEntry(int? tab, int entryId)
        {
            ViewBag.Header = "Удаление книги из" + getHeaderGroup(tab);
            return View(db.BooksOnMains.FirstOrDefault(x => x.ID == entryId));
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult DeleteEntry(int? tab, int entryId, FormCollection collection)
        {
            db.BooksOnMains.DeleteOnSubmit(db.BooksOnMains.First(x => x.ID == entryId));
            db.SubmitChanges();
            return RedirectToAction("EditTabs", new { tab = tab });
        }

        private string getHeaderGroup(int? tab)
        {
            string header;
            switch (tab)
            {
                default:
                case 1:
                    header = " верхней группы товаров";
                    break;
                case 2:
                    header = " спецпредложений";
                    break;
                case 3:
                    header = " нижней группы товаров";
                    break;

            }
            return header;
        }
    }
}
