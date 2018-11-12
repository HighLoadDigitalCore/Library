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
    public class PagesController : Controller
    {
        DB db = new DB();

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Index(int? Page)
        {
            var pagedList = new PagedData<CMSPage>(db.CMSPages.OrderBy(x => x.OrderNum), Page ?? 0, 20, "MasterListPaged");
            return View(pagedList);
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Edit(int? ID, int? ParentID)
        {
            CMSPage page = new CMSPage() { ParentID = 0, Visible = true };

            if (!ID.HasValue || ID == 0)
            {
                ViewBag.Header = "Создание нового раздела";
            }
            else
            {
                ViewBag.Header = "Редактирование раздела";
                page = db.CMSPages.FirstOrDefault(x => x.ID == ID);
                if (page == null)
                {
                    RedirectToAction("Index");
                }
            }
            var parents = db.CMSPages.AsEnumerable().ToList();
            parents.Insert(0, new CMSPage() { ID = 0, PageName = "Корневой раздел сайта" });
            ViewBag.Parents = new SelectList(parents, "ID", "PageName", page.ParentID ?? 0);
            ViewBag.Types = new SelectList(db.PageTypes.AsEnumerable(), "ID", "Description");
            return View(page);
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Edit(int? ID, FormCollection collection)
        {
            CMSPage page = new CMSPage();

            if (ID.HasValue && ID > 0)
            {
                page = db.CMSPages.FirstOrDefault(x => x.ID == ID);
                if (page == null)
                    return RedirectToAction("Index");
            }
            else
            {
                page.OrderNum = (db.CMSPages.Count() + 1) * 10;
                db.CMSPages.InsertOnSubmit(page);
            }
            UpdateModel(page);
            if (page.ParentID == 0) page.ParentID = null;
            try
            {
                db.SubmitChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                var parents = db.CMSPages.AsEnumerable().ToList();
                parents.Insert(0, new CMSPage() { ID = 0, PageName = "Корневой раздел сайта" });
                ViewBag.Parents = new SelectList(parents, "ID", "PageName", page.ParentID ?? 0);
                ViewBag.Types = new SelectList(db.PageTypes.AsEnumerable(), "ID", "Description");
                return View(page);


            }
            CMSPage.FullPageTable = null;
            return RedirectToAction("Index");

        }

        [HttpGet]
        [AuthorizeMaster]
        public JsonResult getTreeData()
        {
            var result = new JsonResult();
            JsTreeModel root = new JsTreeModel() { data = AccessHelper.SiteName.ToNiceForm(), attr = new JsTreeAttribute() { id = "x0", href = "#", uid = 0 }, children = new List<JsTreeModel>() };
            fillModel(ref root, null);
            result.Data = root;
            result.JsonRequestBehavior = JsonRequestBehavior.AllowGet;
            result.ContentType = "application/json";
            return result;

        }


        private void fillModel(ref JsTreeModel model, int? id)
        {
            var pages = CMSPage.FullPageTable.Where(x => id == null ? !x.ParentID.HasValue : x.ParentID == id).OrderBy(
                x => x.OrderNum);

            //var pages = db.CMSPages.Where(x => id == null ? !x.ParentID.HasValue : x.ParentID == id).OrderBy(x => x.OrderNum);
            foreach (var cmsPage in pages)
            {
                var child = new JsTreeModel()
                                {
                                    data = cmsPage.PageName,
                                    attr =
                                        new JsTreeAttribute()
                                            {
                                                id = "x" + cmsPage.ID,
                                                href = Url.Action("Edit", "Pages", new { ID = cmsPage.ID }),
                                                uid = cmsPage.ID
                                            },
                                };
                if (model.children == null)
                    model.children = new List<JsTreeModel>();
                model.children.Add(child);
                fillModel(ref child, cmsPage.ID);
            }
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Delete(int? ID)
        {
            var page = db.CMSPages.FirstOrDefault(x => x.ID == ID);
            if (page == null)
                return RedirectToAction("Index");
            return View(page);
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Delete(int? ID, FormCollection collection)
        {
            var page = db.CMSPages.FirstOrDefault(x => x.ID == ID);
            if (page == null)
                return RedirectToAction("Index");

            deleteRecursive(page);
            db.SubmitChanges();
            return RedirectToAction("Index");
        }

        private void deleteRecursive(CMSPage page)
        {
            if (page.Children.Any())
            {
                foreach (var child in page.Children)
                {
                    deleteRecursive(child);
                }
            }
            db.CMSPages.DeleteOnSubmit(page);
        }

        [HttpPost]
        [AuthorizeMaster]
        public ContentResult saveNode(int nodeID, int targetID, string type)
        {
            var currentNode = db.CMSPages.FirstOrDefault(x => x.ID == nodeID);
            var targetNode = db.CMSPages.FirstOrDefault(x => x.ID == targetID);
            if (currentNode == null || (targetNode == null && targetID != 0)) return new ContentResult();
            var targetParent = targetNode == null ? null : (int?)targetNode.ID;
            switch (type)
            {
                //родитель меняется
                case "first":
                    currentNode.ParentID = targetParent;
                    var inLevelNodes = db.CMSPages.Where(x => targetParent == null ? !x.ParentID.HasValue : x.ParentID == targetParent);
                    if (inLevelNodes.Any())
                        currentNode.OrderNum = inLevelNodes.Min(x => x.OrderNum) - 20;
                    break;
                case "last":
                    currentNode.ParentID = targetParent;
                    var inLevelNodesA = db.CMSPages.Where(x => targetParent == null ? !x.ParentID.HasValue : x.ParentID == targetParent);
                    if (inLevelNodesA.Any())
                        currentNode.OrderNum = inLevelNodesA.Max(x => x.OrderNum) + 20;
                    break;
                //родитель не меняется ??
                case "before":
                    targetParent = targetNode == null ? null : (int?)targetNode.ParentID;
                    var prevInOrder =
                        db.CMSPages.Where(x => (targetParent == null ? !x.ParentID.HasValue : x.ParentID == targetParent) && x.OrderNum < targetNode.OrderNum);
                    foreach (var page in prevInOrder)
                    {
                        page.OrderNum -= 40;
                    }
                    currentNode.OrderNum = targetNode.OrderNum - 20;
                    currentNode.ParentID = targetParent;
                    break;
                case "after":
                    targetParent = targetNode == null ? null : (int?)targetNode.ParentID;
                    var nextInOrder =
                        db.CMSPages.Where(x => (targetParent == null ? !x.ParentID.HasValue : x.ParentID == targetParent) && x.OrderNum > targetNode.OrderNum);
                    foreach (var page in nextInOrder)
                    {
                        page.OrderNum += 40;
                    }
                    currentNode.OrderNum = targetNode.OrderNum + 20;
                    currentNode.ParentID = targetParent;
                    break;
            }

            db.SubmitChanges();
            //Обнуляем кеш
            CMSPage.FullPageTable = null;
            return new ContentResult() { Content = "1" };
        }

        public ActionResult Recalc()
        {
            var maxLevel = CMSPage.FullPageTable.Max(x => x.TreeLevel);
            var db = new DB();
            for (int i = maxLevel; i > 0; i--)
            {
                var pages = CMSPage.FullPageTable.Where(x => x.TreeLevel == i && x.Type == 1);
                foreach (CMSPage cmsPage in pages)
                {
                    var dbp = db.CMSPages.First(x => x.ID == cmsPage.ID);
                    var children = CMSPage.FullPageTable.Where(x => x.ParentID == cmsPage.ID && x.Type == 1).ToList();
                    dbp.ActiveCount = db.BookAvailableCounters.First(x => x.ID == cmsPage.ID).BookCount.Value +
                                      (children.Any()
                                           ? children.Sum(
                                               x => x.ActiveCount)
                                           : 0);
                    dbp.AllCount = db.BookCommonCounters.First(x => x.ID == cmsPage.ID).BookCount.Value +
                                   (children.Any()
                                        ? children.Sum(x => x.AllCount)
                                        : 0);
                    db.SubmitChanges();
                }
                
                CMSPage.FullPageTable = null;
            }
            return RedirectToAction("Index");
        }
    }
}
