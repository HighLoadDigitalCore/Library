﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class SettingsController : Controller
    {
        private DB db = new DB();

        [HttpGet, AuthorizeMaster]
        public ActionResult Index()
        {
            var list = (from x in db.SiteSettings
                        orderby x.OrderNum
                        select x).AsEnumerable();
            foreach (var setting in list)
            {
                ViewData.Add(setting.Setting, setting.oValue);
            }
            return View(list);
        }

        [HttpPost, AuthorizeMaster,ValidateInput(false)]
        public ActionResult Index(FormCollection collection)
        {
            IValueProvider provider = collection.ToValueProvider();
            foreach (string key in collection.AllKeys)
            {
                var setting = (from x in db.SiteSettings
                               where x.Setting == key
                               select x).First();
                try
                {
                    setting.Value = provider.GetValue(key).ConvertTo(Type.GetType(setting.ObjectType)).ToString();

                }
                catch (Exception)
                {

                    setting.Value = "";
                }
            }
            ModelState.AddModelError("", "Данные успешно сохранены");
            db.SubmitChanges();
            return Index();
        }
    }
}

