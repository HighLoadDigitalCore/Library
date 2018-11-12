using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sprinter.Extensions;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class TextPageController : Controller
    {
        private DB db = new DB();
        public ActionResult Index()
        {
            var info = AccessHelper.CurrentPageInfo;

            return PartialView(db.PageTextDatas.FirstOrDefault(x => x.PageID == info.ID));
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Editor(int? pageID)
        {
            var pages =
                db.CMSPages.Where(x => x.PageType.TypeName == "TextPage").OrderBy(x => x.OrderNum).AsEnumerable().ToList();
            pages.Insert(0, new CMSPage() {ID = 0, PageName = "Выберите страницу в списке"});

            ViewBag.TextPages = new SelectList(pages, "ID", "PageName", pageID ?? 0);

            var current = db.CMSPages.FirstOrDefault(x => x.ID == pageID);
            return View(current);
        }


        [HttpPost]
        [AuthorizeMaster]
        [ValidateInput(false)]
        public ActionResult Editor(int? pageID, FormCollection collection)
        {


            var current = db.CMSPages.FirstOrDefault(x => x.ID == pageID);
            if(current==null)
            {
                return RedirectToAction("Editor");
            }

            var pages =
                db.CMSPages.Where(x => x.PageType.TypeName == "TextPage").OrderBy(x => x.OrderNum).AsEnumerable().ToList
                    ();
            pages.Insert(0, new CMSPage() { ID = 0, PageName = "Выберите страницу в списке" });

            ViewBag.TextPages = new SelectList(pages, "ID", "PageName", pageID ?? 0);



            PageTextData data;
            if (current.PageTextDatas.Any())
                data = current.PageTextDatas.First();
            else
            {
                data = new PageTextData(){CMSPage = current};
                db.PageTextDatas.InsertOnSubmit(data);
            }
            data.ShowHeader = (bool) collection.ToValueProvider().GetValue("ShowHeader").ConvertTo(typeof (bool));
            data.Text = (string)collection.ToValueProvider().GetValue("Text").ConvertTo(typeof(string));
            db.SubmitChanges();

            ModelState.AddModelError("", "Данные успешно сохранены");
            return View(current);
        }

        public ActionResult NotFound()
        {

            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return PartialView();

        }

    }
}
