using System;
using System.Linq;
using System.Web;
using System.Web.Caching;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Models
{
    public partial class SiteSetting
    {

        public static T Get<T>(string key)
        {
            if (HttpContext.Current.Cache[key] != null)
            {
                return (T)HttpContext.Current.Cache[key];
            }
            DB db = new DB();
            T obj = default(T);
            var setting = from x in db.SiteSettings
                                                 where x.Setting == key
                                                 select x;
            if (setting.Any())
            {
                try
                {
                    obj = (T)setting.First().oValue;
                }
                catch (Exception)
                {
                    obj = default(T);
                }
                
                HttpContext.Current.Cache.Add(key, obj, null, DateTime.Now.AddDays(1.0), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
            }
            return obj;
        }
        public object oValue
        {
            get
            {
                return Value.ToTypedObject(Type.GetType(this.ObjectType));
            }
        }
        public string TemplateName
        {
            get
            {
                return ObjectType.Replace("System.", "");
            }
        }
    }

}