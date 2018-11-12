using System.Collections.Generic;
using Sprinter.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Security;

namespace Sprinter.Extensions.Helpers
{


    public static class AccessHelper
    {

        public static Dictionary<string, object> Repository { get; set; }
        public static void AddToRepository(string key, object value)
        {
            if (Repository.ContainsKey(key))
                Repository[key] = value;
            else Repository.Add(key, value);
        }
        static AccessHelper()
        {
            Repository = new Dictionary<string, object>();
        }

        public static CommonPageInfo CurrentPageInfo
        {
            get
            {
                if (Repository.ContainsKey("CommonInfo"))
                    return (CommonPageInfo)Repository["CommonInfo"];
                var info = CommonPageInfo.InitFromQueryParams();
                Repository.Add("CommonInfo", info);
                return info;
            }
            set
            {
                if (Repository.ContainsKey("CommonInfo"))
                    Repository["CommonInfo"] = value;
                else Repository.Add("CommonInfo", value);
            }
        }

        private static string _siteUrl;
        public static string SiteUrl
        {
            get
            {
                if (_siteUrl.IsNullOrEmpty())
                    try
                    {
                        _siteUrl = HttpContext.Current.Request.Url.Scheme + "://" + HttpContext.Current.Request.Url.Host;
                    }
                    catch
                    {
                        _siteUrl = "http://sprinter.ru";
                    }
                return _siteUrl;
            }
        }
        public static string SiteName
        {
            get
            {
                try
                {
                    return HttpContext.Current.Request.Url.Host;
                }
                catch
                {
                    return "sprinter.ru";
                }
            }
        }
        public static string getStartUserController(string userName)
        {
            return MasterRoles.Any(role => Roles.IsUserInRole(userName, role)) ? "MasterHome" : "Home";
        }

        public static bool HasAccess(string controller)
        {
            return true;
            /*
                        string[] userRoles = Roles.GetRolesForUser(HttpContext.Current.User.Identity.Name);
                        if (userRoles.Contains<string>("GrandAdmin") || userRoles.Contains<string>("Director"))
                        {
                            return true;
                        }
                        DB db = new DB();
                        return (from x in db.eControllers.First<eController>(x => (x.Controller == controller)).xRolesInControllers select x.cRole.RoleName).ToList<string>().Intersect<string>(userRoles).Any<string>();
            */
        }

        public static string CurrentRole
        {
            get
            {
                return Roles.GetRolesForUser(HttpContext.Current.User.Identity.Name).First<string>();
            }
        }

        public static string CurrentRoleName
        {
            get
            {
                return "";
                /*
                                DB db = new DB();
                                return db.cRoles.First<cRole>(x => (x.RoleName == Roles.GetRolesForUser(HttpContext.Current.User.Identity.Name).First<string>())).Description;
                */
            }
        }

        public static Guid CurrentUserKey
        {
            get
            {
                return (Guid)Membership.GetUser().ProviderUserKey;
            }
        }

        public static bool IsMasterPage
        {
            get { return HttpContext.Current.Request.RawUrl.Contains("/Master"); }
        }

        public static string NoMail
        {
            get { return "nomail@sprinter.ru"; }
        }

        public static bool IsMaster
        {
            get { return MasterRoles.Any(Roles.IsUserInRole); }
        }
        private static List<string> _masterRoles;
        public static List<string> MasterRoles
        {
            get { return _masterRoles ?? (_masterRoles = new List<string> { "Administrator" }); }
        }

        public static bool IsAuthClient
        {
            get { return HttpContext.Current.User.Identity.IsAuthenticated && Roles.IsUserInRole("Client"); }
        }
    }
}

