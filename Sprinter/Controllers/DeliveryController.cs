using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sprinter.Models;
using Sprinter.Extensions;
namespace Sprinter.Controllers
{
    public class DeliveryController : Controller
    {
        private DB db = new DB();
        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Index()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            list.AddRange(db.OrderDeliveryProviders.Select(
                  x =>
                  new KeyValuePair<string, string>(Url.Action("Regions", new { pid = x.ID }),
                                                   string.Format("{0} --> {1}", x.OrderDeliveryGroup.GroupName, x.Name))));
            return View(list);
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Regions(int? pid)
        {
            if (!pid.HasValue) return RedirectToAction("Index");
            ViewBag.Provider = db.OrderDeliveryProviders.FirstOrDefault(x => x.ID == pid);
            var regions = db.OrderDeliveryRegions.Where(x => x.DeliveryProviderID == pid).OrderBy(x => x.ImportID).ThenBy(x=> x.Name);
            return View(regions);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Regions(int? pid, FormCollection collection)
        {
            if (!pid.HasValue) return RedirectToAction("Index");
            var provider = db.OrderDeliveryProviders.FirstOrDefault(x => x.ID == pid);
            if (provider == null) return RedirectToAction("Index");
            provider.DiscountThreshold = collection["DiscountThreshold"].ToDecimal();
            db.SubmitChanges();
            ModelState.AddModelError("", "Данные успешно сохранены.");
            ViewBag.Provider = provider;
            var regions = db.OrderDeliveryRegions.Where(x => x.DeliveryProviderID == pid).OrderBy(x => x.ImportID).ThenBy(x=> x.Name);
            return View(regions);
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Edit(int pid, int? rid)
        {
            var region = new OrderDeliveryRegion();
            region.RegionDistance = 0;
            if(rid > 0)
            {
                region = db.OrderDeliveryRegions.FirstOrDefault(x => x.ID == rid);
            }
            return View(region);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Edit(int pid, int? rid, FormCollection collection)
        {
            var region = new OrderDeliveryRegion(); 
            if(rid > 0)
            {
                region = db.OrderDeliveryRegions.FirstOrDefault(x => x.ID == rid);
            }
            else
            {
                db.OrderDeliveryRegions.InsertOnSubmit(region);
            }
            UpdateModel(region);
            if (region.DistanceZoneID == 0)
                region.DistanceZoneID = null;
            if (region.WeightZoneID == 0)
                region.WeightZoneID = null;
            region.DeliveryProviderID = pid;
            db.SubmitChanges();
            return RedirectToAction("Regions", new {pid = pid});
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Delete(int pid, int? rid, FormCollection collection)
        {
            var r = db.OrderDeliveryRegions.FirstOrDefault(x => x.ID == (rid ?? 0));
            if (r != null)
            {
                db.OrderDeliveryRegions.DeleteOnSubmit(r);
                db.SubmitChanges();
            }
            return RedirectToAction("Regions", new { pid = pid });
        }
        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Delete(int pid, int? rid)
        {
            var r = db.OrderDeliveryRegions.FirstOrDefault(x => x.ID == (rid ?? 0));
            if (r == null)
                return RedirectToAction("Regions", new {pid = pid});
            return View(r);
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult ZoneList()
        {
            return View(db.OrderDeliveryZones.OrderBy(x=> x.Name));
        }


        [AuthorizeMaster]
        [HttpPost]
        public ActionResult DeleteZone(int id, FormCollection collection)
        {
            var r = db.OrderDeliveryZones.FirstOrDefault(x => x.ID == id);
            if (r != null)
            {
                db.OrderDeliveryZones.DeleteOnSubmit(r);
                db.SubmitChanges();
            }
            return RedirectToAction("ZoneList");
        }
        [AuthorizeMaster]
        [HttpGet]
        public ActionResult DeleteZone(int id)
        {
            var r = db.OrderDeliveryZones.FirstOrDefault(x => x.ID == id);
            if (r == null)
                return RedirectToAction("ZoneList");
            return View(r);
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult EditZone(int id)
        {
            var z = db.OrderDeliveryZones.FirstOrDefault(x => x.ID == id);
            if (z == null)
                z = new OrderDeliveryZone();
            return View(z);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult EditZone(int id, FormCollection collection)
        {
            var z = db.OrderDeliveryZones.FirstOrDefault(x => x.ID == id);
            if (z == null)
            {
                z = new OrderDeliveryZone();
                db.OrderDeliveryZones.InsertOnSubmit(z);
            }
            UpdateModel(z);
            if (z.AlternativeZone == 0)
                z.AlternativeZone = null;
            db.SubmitChanges();
            return RedirectToAction("ZoneList");
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult ZoneIntervalsList(int zid)
        {
            var intervals = db.OrderDeliveryZoneIntervals.Where(x => x.ZoneID == zid);
            return View(intervals);
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult EditZoneInterval(int zid, int id)
        {
            var interval = db.OrderDeliveryZoneIntervals.FirstOrDefault(x => x.ID == id);
            if(interval==null) interval = new OrderDeliveryZoneInterval();
            return View(interval);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult EditZoneInterval(int zid, int id, FormCollection collection)
        {
            var interval = db.OrderDeliveryZoneIntervals.FirstOrDefault(x => x.ID == id);
            if(interval==null)
            {
                interval = new OrderDeliveryZoneInterval {ZoneID = zid};
                db.OrderDeliveryZoneIntervals.InsertOnSubmit(interval);
            }
            UpdateModel(interval);
            db.SubmitChanges();
            return RedirectToAction("ZoneIntervalsList", new {zid = zid});
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult DeleteZoneInterval(int id, int zid, FormCollection collection)
        {
            var r = db.OrderDeliveryZoneIntervals.FirstOrDefault(x => x.ID == id);
            if (r != null)
            {
                db.OrderDeliveryZoneIntervals.DeleteOnSubmit(r);
                db.SubmitChanges();
            }
            return RedirectToAction("ZoneIntervalsList", new{zid = zid});
        }
        [AuthorizeMaster]
        [HttpGet]
        public ActionResult DeleteZoneInterval(int id, int zid)
        {
            var r = db.OrderDeliveryZoneIntervals.FirstOrDefault(x => x.ID == id);
            if (r == null)
                return RedirectToAction("ZoneIntervalsList");
            return View(r);
        }


    }
}
