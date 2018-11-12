using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sprinter.Extensions;

namespace Sprinter.Models
{
    public class CabinetPageSelector
    {
        public static string CurrentAction
        {
            get
            {
                var view = HttpContext.Current.Request.QueryString["view"];
                if(view.IsNullOrEmpty())
                    view = "common";
                if (view == "orders" && HttpContext.Current.Request.QueryString["id"].IsFilled())
                    view = "details";
                return view.ToNiceForm();
            }
        }
    }
}