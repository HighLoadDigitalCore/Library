using System.Web;
using System.Web.Mvc;

namespace Sprinter
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            var errorAttribute = new HandleErrorAttribute();
            errorAttribute.View = "NotFound";
            filters.Add(errorAttribute);
        }
    }
}