using System;
using System.Linq;
using System.Web.Mvc;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class SpecsController : Controller
    {
        DB db = new DB();

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Edit(int entryId, FormCollection collection)
        {
            var entry = db.BookSpecOffers.FirstOrDefault(x => x.ID == entryId);
            if (entry == null)
            {
                entry = new BookSpecOffer();
                db.BookSpecOffers.InsertOnSubmit(entry);
            }
            UpdateModel(entry, new[] { "SaleCatalogID", "SpecPrice", "MinPrice" });
            try
            {
                db.SubmitChanges();
            }
            catch (Exception e)
            {
                ModelState.AddModelError("", "Книга с таким идентификатором не найдена");
                return Edit(entryId);
            }
            return RedirectToAction("List");
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Edit(int entryId)
        {

            var entry = db.BookSpecOffers.FirstOrDefault(x => x.ID == entryId) ?? new BookSpecOffer();
            return View(entry);
        }
        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Delete(int entryId)
        {
            ViewBag.Header = "Удаление книги из cписка спецпредложений";
            return View(db.BookSpecOffers.FirstOrDefault(x => x.ID == entryId));
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Delete(int entryId, FormCollection collection)
        {
            db.BookSpecOffers.DeleteOnSubmit(db.BookSpecOffers.First(x => x.ID == entryId));
            db.SubmitChanges();
            return RedirectToAction("List");
        }
        [HttpGet]
        [AuthorizeMaster]
        public ActionResult List()
        {
            ViewBag.Header = "Редактирование списка спецпредложений";
            var items = db.BookSpecOffers.OrderBy(x => x.BookSaleCatalog.BookDescriptionCatalog.Header).AsEnumerable();
            return View(items);
        }

        [HttpGet]
        [AuthorizeMaster]
        public PartialViewResult Preview(int? id)
        {
            var sale = db.BookSaleCatalogs.FirstOrDefault(x => x.ID == (id ?? 0));
            return PartialView(sale);
        }

    }
}
