using System.Web.Routing;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Security;

namespace Sprinter.Extensions
{
    public class AuthorizeClient : AuthorizeAttribute
    {

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {

            return AccessHelper.IsAuthClient;
            //return base.AuthorizeCore(httpContext);
        }



        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var result = new ContentResult();
            result.Content = "<script type=\"text/javascript\">$().ready(function(){$('#auth-modal-content').modal({ containerCss: { width: 400, height: 250 } });});</script>";

            filterContext.Result = result;
        }

    }

    public class AuthorizeMaster : AuthorizeAttribute
    {

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {

            return AccessHelper.IsMaster;
            //return base.AuthorizeCore(httpContext);
        }



        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            UrlHelper helper = new UrlHelper(filterContext.RequestContext);
            RouteValueDictionary routeValues = new RouteValueDictionary();
            routeValues.Add("ReturnUrl",  HttpUtility.UrlPathEncode(
                                                               filterContext.HttpContext.Request.Url.PathAndQuery));
                                              


            string url = UrlHelper.GenerateUrl(
                "Master",
                "LogOn",
                "Account",
                routeValues,
                helper.RouteCollection,
                filterContext.RequestContext,
                true
                );


            filterContext.Result = new RedirectResult(url);

          
            //base.HandleUnauthorizedRequest(filterContext);
        }

    }
}

