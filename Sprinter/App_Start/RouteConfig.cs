using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

namespace Sprinter
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{*botdetect}", new { botdetect = @"(.*)BotDetectCaptcha\.ashx" });
            routes.IgnoreRoute("{*ckfinder}", new { ckfinder = @"(.*)ckfinder(.*)" });




            routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );



            routes.MapRoute(
                name: "Master",
                url: "Master/{controller}/{action}/{id}",
                defaults: new { controller = "MasterHome", action = "Index", id = UrlParameter.Optional }
                );


            routes.MapRoute(
                name: "MasterListPaged",
                url: "Master/{controller}/{action}/Page/{page}",
                defaults: new { controller = "MasterHome", action = "Index", page = UrlParameter.Optional });

            routes.MapRoute(
                name: "ImgPreview",
                url: "Master/{controller}/{action}/bookID/{bookID}/width/{width}",
                defaults: new { controller = "FilesController", action = "BookPicture" });


            routes.MapRoute(
                name: "DefaultOld",
                url: "books/{url}.html",
                defaults: new
                              {
                                  controller = "Selector",
                                  action = "OldPage"
                              }
                );

            routes.MapRoute(
                name: "DefaultOldCategoryL1",
                url: "catalog/{c1}/",
                defaults: new
                              {
                                  controller = "Selector",
                                  action = "OldPageCatL1"
                              },
                              constraints: new { c1 = @"\d{1,}" }
                );

            routes.MapRoute(
                name: "DefaultOldCategoryL2",
                url: "catalog/{c1}/{c2}/",
                defaults: new
                    {
                        controller = "Selector",
                        action = "OldPageCatL2"
                    },
                constraints: new {c1 = @"\d{1,}" /*, c2 = @"\d{1,}"*/}
                );

            routes.MapRoute(
                name: "Default",
                url: "{url1}/{url2}/{url3}/{url4}/{url5}/{url6}/{url7}/{url8}/{url9}",
                defaults: new
                              {
                                  controller = "Selector",
                                  action = "Index",
                                  url1 = UrlParameter.Optional,
                                  url2 = UrlParameter.Optional,
                                  url3 = UrlParameter.Optional,
                                  url4 = UrlParameter.Optional,
                                  url5 = UrlParameter.Optional,
                                  url6 = UrlParameter.Optional,
                                  url7 = UrlParameter.Optional,
                                  url8 = UrlParameter.Optional,
                                  url9 = UrlParameter.Optional
                              }
                );




        }
    }
}