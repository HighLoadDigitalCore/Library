using Sprinter.Extensions;
using Sprinter.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sprinter.Controllers
{


    public class FilesController : Controller
    {
        private DB db = new DB();

        [AllowAnonymous]
        [HttpGet]
        public FileResult BookPicture(int bookID, int width)
        {
            var cover = db.BookSaleCatalogs.First(x => x.ID == bookID).BookDescriptionCatalog.BookCover;
            FileStreamResult result;
            if(cover==null)
            {
                FileStream fs = new FileStream(Server.MapPath("/Content/nopic.gif"), FileMode.Open, FileAccess.Read);
                result = new FileStreamResult(fs, "image/gif");
            }
            else
            {
                MemoryStream ms = new MemoryStream(cover.Data.ToArray());
                ms.Seek(0L, SeekOrigin.Begin);
                Bitmap bmpIn = new Bitmap(ms);
                ImageFormat loFormat = bmpIn.RawFormat;

                Bitmap bmpOut = bmpIn.CreateThumbnail(cover.getProperWidth(width), cover.getProperHeight(width), false);
                ms.Close();
                MemoryStream res = new MemoryStream();
                bmpOut.Save(res, loFormat);
                res.Seek(0L, SeekOrigin.Begin);
                result = new FileStreamResult(res,
                                              "image/" +
                                              Path.GetExtension(cover.Name.IsNullOrEmpty() ? ".jpg" : cover.Name).
                                                  Substring(1));

            }
            return result;
        }


        public FileResult Image(string path, int? size, string output, bool? preview)
        {
            bool usePreview = !preview.HasValue ? true : preview.Value;
            MemoryStream ms = new MemoryStream();
            FileStreamResult empty = new FileStreamResult(ms, "image/jpeg");
            if (!size.HasValue)
            {
                size = 60;
            }
            path = HttpContext.Server.MapPath(HttpContext.Request.ApplicationPath) + @"\" + path;
            if (!System.IO.File.Exists(path))
            {
                return empty;
            }
            string previewPath = Path.GetDirectoryName(path) + @"\preview";
            string previewFile = "{0}_{1}{2}".FormatWith(new string[] { Path.GetFileNameWithoutExtension(path), size.ToString(), Path.GetExtension(path) });
            string existFile = Path.Combine(previewPath, previewFile);
            if (usePreview)
            {
                if (!Directory.Exists(previewPath))
                {
                    Directory.CreateDirectory(previewPath);
                }
                if (System.IO.File.Exists(existFile))
                {
                    return new FileStreamResult(new FileStream(existFile, FileMode.Open, FileAccess.Read), "image/" + Path.GetExtension(path).Substring(1));
                }
            }
            Bitmap bmpIn = new Bitmap(path);
            ImageFormat loFormat = bmpIn.RawFormat;
            Bitmap bmpOut = bmpIn.CreateThumbnail(size.Value, size.Value, true);
            bmpIn.Dispose();
            if (bmpOut == null)
            {
                return empty;
            }
            if (usePreview)
            {
                if (!HttpContext.User.Identity.IsAuthenticated)
                {
                    bmpOut.Dispose();
                    return empty;
                }
                try
                {
                    string outPath = string.IsNullOrEmpty(output) ? existFile : Server.MapPath(output);
                    if (!System.IO.File.Exists(outPath))
                    {
                        bmpOut.Save(outPath, loFormat);
                    }
                }
                catch
                {
                    bmpOut.Dispose();
                    return empty;
                }
            }
            bmpOut.Save(ms, loFormat);
            ms.Seek(0L, SeekOrigin.Begin);
            FileStreamResult result = new FileStreamResult(ms, "image/" + Path.GetExtension(path).Substring(1));
            bmpOut.Dispose();
            return result;
        }


    }
}

