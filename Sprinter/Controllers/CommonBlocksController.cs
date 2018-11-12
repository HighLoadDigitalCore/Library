using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sprinter.Extensions;
using Sprinter.Models;
using Sprinter.Models.ViewModels;

namespace Sprinter.Controllers
{
    public class CommonBlocksController : Controller
    {
        private DB db = new DB();
        private static HeaderViewModel _headerModel;
        public static HeaderViewModel HeaderModel
        {
            get { return _headerModel ?? (_headerModel = new HeaderViewModel()); }
        }

        public ActionResult CountersBlock()
        {
            return PartialView(new CountersBlockModel());
        }

        public ActionResult ShopCartRight()
        {
            return PartialView(new ShopCartRight());
        }

        public ActionResult OrderSteps()
        {
            return PartialView(new OrderSteps());
        }

        [HttpGet]
        public ActionResult Search()
        {
            return PartialView(new CommonSearch());
        }

        [HttpPost]
        public ActionResult Search(CommonSearch search, FormCollection collection)
        {
            if (search.SearchQuery.IsNullOrEmpty())
                return PartialView(search);
            Response.Redirect(search.ToString());
            return PartialView(search);
        }

        public ActionResult Header()
        {
            return PartialView(HeaderModel);
        }

        public ActionResult LeftColumn()
        {
            CMSPage page = db.CMSPages.FirstOrDefault(x => x.URL == "catalog" && x.PageType.TypeName == "Catalog");
            return PartialView(page);
        }
        public ActionResult Footer()
        {
            return PartialView(HeaderModel);
        }

    }
}
