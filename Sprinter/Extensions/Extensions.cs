using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Web.Security;
using System.Xml;
using System.Xml.Linq;
using Sprinter.Extensions.Helpers;
using Sprinter.Extensions.PaymentServices.AuxClasses;
using Sprinter.Models;


namespace Sprinter.Extensions
{
    public enum WordKind
    {
        Man,
        Woman,
        Middle
    }

    public static class Extensions
    {
        private static readonly DateTime MaxDate = new DateTime(0x270f, 12, 0x1f, 0x17, 0x3b, 0x3b, 0x3e7);
        private static readonly DateTime MinDate = new DateTime(0x76c, 1, 1);
        private static readonly Regex WebUrlExpression = new Regex(@"((http|https)://)?([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?", RegexOptions.Singleline | RegexOptions.Compiled);



        public static bool IsFilled(this decimal? val)
        {
            return val.HasValue && val.Value != 0;
        }

        public static IEnumerable<T> Split<T>(this string target, params string[] args)
        {
            return target.Split(args.Any()?args:new[]{";", ".", ","}, StringSplitOptions.RemoveEmptyEntries).Select(x=> x.Trim()).Select(x => x.ConvertFromString<T>());
        }

        public static string FormatWith(this string target, params object[] args)
        {
            Check.Argument.IsNotEmpty(target, "target");
            return string.Format(target, args);
        }

        public static bool IsWebUrl(this string target)
        {
            return (!string.IsNullOrEmpty(target) && WebUrlExpression.IsMatch(target));
        }
        public static bool IsValid(this DateTime target)
        {
            return ((target >= MinDate) && (target <= MaxDate));
        }
        public static RouteValueDictionary ToRouteValues(this HttpRequestBase request)
        {
            var routes = new RouteValueDictionary();
            foreach (string key in request.QueryString.Keys)
            {
                routes.Add(key, request.QueryString[key]);
            }
            return routes;
        }


        public static bool IsArchiveExt(this string fileName)
        {
            return fileName.EndsWith(".rar") || fileName.EndsWith(".zip") || fileName.EndsWith(".gz");
        }
        public static IEnumerable<T> WrapWithEnumerable<T>(this T obj)
        {
            yield return obj;
        }

        public static List<string> GetPropertyNameList(this object data)
        {
            var infos = data.GetType().GetProperties();
            return infos.Where(x => x.CanRead && x.CanWrite).Select(x => x.Name).ToList();
        }

        public static object GetPropertyValue(this object data, string field)
        {
            var infos = data.GetType().GetProperties();
            var prop = infos.FirstOrDefault(x => x.Name == field);
            if (prop == null) return null;
            return prop.GetValue(data, null);
        }

        public static void SetPropertyValueByString(this object data, string name, string field)
        {
            var infos = data.GetType().GetProperties();
            var prop = infos.FirstOrDefault(x => x.Name == name);
            if (prop == null) return;
            prop.SetValue(data, Convert.ChangeType(field, prop.PropertyType), null);
        }

        public static void SetPropertyValue(this object data, string name, object field)
        {
            var infos = data.GetType().GetProperties();
            var prop = infos.FirstOrDefault(x => x.Name == name);
            if (prop == null) return;
            prop.SetValue(data, field, null);

        }

        public static string GetPropertyAttribute<T>(this object data, string propName, string attrName)
        {
            var infos = data.GetType().GetProperties();
            var prop = infos.FirstOrDefault(x => x.Name == propName);
            if (prop == null) return "";

            var attrList = prop.GetCustomAttributes(true);
            foreach (var a in attrList)
            {
                if (a is T)
                    return (a.GetPropertyValue(attrName) ?? "").ToString();

            }
            return "";

        }

        public static User UserEntity(this MembershipUser user)
        {
            DB db = new DB();
            return db.Users.First(x => x.UserId == (Guid)user.ProviderUserKey);
        }

        public static string GetStringPostfix(this int value, WordKind kind)
        {
            var rest = value % 10;
            if (kind == WordKind.Man)
            {
                if (value > 10 && value < 20)
                {
                    return "ов";
                }
                else if (rest == 0 || rest >= 5)
                {
                    return "ов";
                }
                else if (rest == 1)
                {
                    return "";
                }
                else return "а";
            }
            return "";
        }

        public static bool IsGuid(this string guid)
        {
            try
            {
                var g = new Guid(guid);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static MvcHtmlString DisplayNameFor<TModel, TProperty>(this HtmlHelper<IEnumerable<TModel>> helper, Expression<Func<TModel, TProperty>> expression)
        {
            var name = ExpressionHelper.GetExpressionText(expression);
            name = helper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(name);
            var metadata = ModelMetadataProviders.Current.GetMetadataForProperty(() => Activator.CreateInstance<TModel>(), typeof(TModel), name);
            return new MvcHtmlString(metadata.DisplayName);
        }

        public static string ReduceSpaces(this string value)
        {
            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex(@"[ ]{2,}", options);
            return regex.Replace(value, @" ");
        }

        public static bool IsMailAdress(this string value)
        {
            Regex regex =
                new Regex(
                    @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$");

            return regex.IsMatch(value);
        }

        public static int? ToIPInt(this string value)
        {
            IPAddress addr = null;
            if (IPAddress.TryParse(value, out addr))
                return BitConverter.ToInt32(addr.GetAddressBytes(), 0);
            return null;
        }

        public static string ToIPString(this int? value)
        {
            if (!value.HasValue) return "";
            return new IPAddress(BitConverter.GetBytes(value.Value)).ToString();
        }

        public static string TruncateToPoint(this string value, int len)
        {
            if (len >= value.Length) return value;
            int index = value.IndexOf(".", len);
            if (index < 0) return value;
            return value.Substring(0, index + 1);
        }

        public static string PageUrl(this HttpRequest request)
        {

            return request.RawUrl.Split(new[] { '?' })[0];
        }

        public static string ToShowStatus(this bool? value)
        {

            return (value ?? false).ToShowStatus();
        }
        public static string ToShowStatus(this bool value)
        {
            return value ? "Отображается" : "Скрыто";
        }
        public static string ToYesNoStatus(this bool value)
        {
            return value ? "Да" : "Нет";
        }

        public static int GetFirstHalf(this int value)
        {
            int rem = value % 2;
            return (int)(value / 2) + rem;
        }

        public static string ClearBorders(this string value)
        {
            string val = value.Trim();
            val = val.Replace("..", ".");
            val = val.Replace("--", "-");
            val = val.Replace("  ", " ");
            if (val.StartsWith(".") || val.StartsWith("-")) val = val.Substring(1).Trim();
            if (val.StartsWith(".") || val.StartsWith("-")) val = val.Substring(1).Trim();
            if (val.EndsWith("-")) val = val.Substring(0, val.Length - 1);
            return val.Replace("- ", "-").Replace(" -", "").Trim();

        }
        public static bool ToBool(this string value)
        {
            bool parsed;
            if (string.IsNullOrEmpty(value)) return false;
            if (value == "1") return true;
            if (value == "0") return false;
            if (value.Contains(",")) value = value.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)[0];
            bool.TryParse(value.ToNiceForm(), out parsed);
            return parsed;
        }

        public static decimal ToDecimal(this string value)
        {
            if (value.IsNullOrEmpty()) return 0;
            decimal parsed;
            decimal.TryParse(value.Replace(".", ",").Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out parsed);
            return parsed;
        }
        public static decimal? ToNullableDecimal(this string value)
        {
            if (value.IsNullOrEmpty()) return null;
            decimal parsed;
            if (decimal.TryParse(value.Replace(".", ",").Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
                return parsed;
            return null;
        }
        public static int ToInt(this string value)
        {
            if (value.IsNullOrEmpty()) return 0;
            int parsed = 0;
            if (int.TryParse(value, out parsed))
                return parsed;
            return 0;
        }

        public static string UnTranslit(this string value, TransliterationType type)
        {
            return Translitter.Back(value, type);
        }
        public static string Translit(this string value, TransliterationType type)
        {
            return Translitter.Front(value, type);
        }
        public static string Translit(this string value)
        {
            return Translitter.Front(value);
        }
        public static string UnTranslit(this string value)
        {
            return Translitter.Back(value);
        }

        public static IEnumerable<XElement> ReadElements(this XmlReader reader, XName elementName)
        {

            while (!reader.EOF)
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {

                            if (reader.Name == elementName)
                            {
                                XElement el = (XElement)XElement.ReadFrom(reader);
                                if (el != null)
                                    yield return el;
                            }
                            else
                                reader.Read();
                        }
                        break;
                    default:
                        {
                            reader.Read();
                        }
                        break;
                }

            }
            /*
                        if (reader.Name == elementName.LocalName && reader.NamespaceURI == elementName.NamespaceName)
                            yield return (XElement)XElement.ReadFrom(reader);
            */

            /*
                        while (reader.ReadToNextSibling(elementName.LocalName, elementName.NamespaceName))
                            yield return (XElement)XElement.ReadFrom(reader);
            */
        }
        public static string ClearHTML(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            return Regex.Replace(value, @"<[^>]*>", String.Empty).Trim();
        }

        public static string CreateDir(this HttpServerUtility server, string path)
        {
            var mapped = server.MapPath(path);
            if (!Directory.Exists(mapped))
            {
                Directory.CreateDirectory(mapped);
            }
            return mapped;
        }

        public static string CreateDir(this HttpServerUtilityBase server, string path)
        {
            var mapped = server.MapPath(path);
            if (!Directory.Exists(mapped))
            {
                Directory.CreateDirectory(mapped);
            }
            return mapped;
        }

        public static MvcHtmlString ActionLinkQuery(this HtmlHelper html, string linkText, string actionName, string controllerName)
        {
            return html.ActionLinkQuery(linkText, actionName, controllerName, new List<string>());
        }

        public static MvcHtmlString ActionLinkQuery(this HtmlHelper html, string linkText, string actionName, string controllerName, IEnumerable<string> queryFilter)
        {
            return html.ActionLinkQuery(linkText, actionName, controllerName, queryFilter, new RouteValueDictionary());
        }

        public static MvcHtmlString ActionLinkQuery(this HtmlHelper html, string linkText, string actionName, string controllerName, IEnumerable<string> queryFilter, object routeValues)
        {
            return html.ActionLinkQuery(linkText, actionName, controllerName, queryFilter, new RouteValueDictionary(routeValues.ToDictionary()), null);
        }

        public static MvcHtmlString ActionLinkQuery(this HtmlHelper html, string linkText, string actionName, string controllerName, IEnumerable<string> queryFilter, RouteValueDictionary routeValues)
        {
            return html.ActionLinkQuery(linkText, actionName, controllerName, queryFilter, routeValues, null);
        }

        public static MvcHtmlString ActionLinkQuery(this HtmlHelper html, string linkText, string actionName, string controllerName, IEnumerable<string> queryFilter, object routeValues, object htmlAttributes)
        {
            return html.ActionLinkQuery(linkText, actionName, controllerName, queryFilter, new RouteValueDictionary(routeValues.ToDictionary()), htmlAttributes);
        }

        public static MvcHtmlString ActionLinkQuery(this HtmlHelper html, string linkText, string actionName, string controllerName, IEnumerable<string> queryFilter, RouteValueDictionary routeValues, object htmlAttributes)
        {
            RouteValueDictionary dictionary = new RouteValueDictionary();
            NameValueCollection query = html.ViewContext.HttpContext.Request.QueryString;
            foreach (string key in query.Keys)
            {
                if (queryFilter.Contains<string>(key))
                {
                    dictionary.Add(key, query[key]);
                }
            }
            foreach (KeyValuePair<string, object> pair in routeValues)
            {
                dictionary.Add(pair.Key, pair.Value);
            }
            return html.ActionLink(linkText, actionName, controllerName, dictionary, (htmlAttributes ?? new object()).ToDictionary());
        }

        public static T ConvertFromString<T>(this string text)
        {
            if (typeof(Enum).IsAssignableFrom(typeof(T)))
            {
                try
                {
                    return (T)Enum.Parse(typeof(T), text);
                }
                catch
                {
                    return default(T);
                }
            }
            return (T)Convert.ChangeType(text, typeof(T));
        }

        public static Bitmap CreateThumbnail(this Bitmap loBMP, int lnWidth, int lnHeight, bool useCalc)
        {
            Bitmap bmpOut = null;
            try
            {
                decimal lnRatio;
                decimal lnTemp;
                int lnNewWidth = 0;
                int lnNewHeight = 0;
                if (useCalc)
                {
                    if ((loBMP.Width < lnWidth) && (loBMP.Height < lnHeight))
                    {
                        return loBMP;
                    }
                    if (loBMP.Width > loBMP.Height)
                    {
                        lnRatio = (decimal)lnWidth / (decimal)loBMP.Width;
                        lnNewWidth = lnWidth;
                        lnTemp = loBMP.Height * lnRatio;
                        lnNewHeight = (int)lnTemp;
                    }
                    else
                    {
                        lnRatio = (decimal)lnHeight / (decimal)loBMP.Height;
                        lnNewHeight = lnHeight;
                        lnTemp = loBMP.Width * lnRatio;
                        lnNewWidth = (int)lnTemp;
                    }
                }
                else
                {
                    lnNewHeight = lnHeight;
                    lnNewWidth = lnWidth;
                }
                bmpOut = new Bitmap(lnNewWidth, lnNewHeight);
                using (Graphics g = Graphics.FromImage(bmpOut))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(loBMP, 0, 0, lnNewWidth, lnNewHeight);
                }
                loBMP.Dispose();
            }
            catch
            {
                return null;
            }
            return bmpOut;
        }

        public static string ForDisplaing(this decimal value)
        {
            if (Math.Truncate(value) != value)
            {
                return value.ToString("f1");
            }
            return value.ToString("f0");
        }

        public static string FormatWith(this string str, params string[] args)
        {
            return string.Format(str, (object[])args);
        }

        public static Color GenerateRandom(this Color color)
        {
            Random rnd = new Random((DateTime.Now.Millisecond * DateTime.Now.Second) * DateTime.Now.Minute);
            return Color.FromArgb(0xff, rnd.Next(1, 0x100), rnd.Next(1, 0x100), rnd.Next(1, 0x100));
        }

        public static bool IsEmpty(this Guid guid)
        {
            return (guid == new Guid());
        }

        public static bool IsEmpty(this Guid? guid)
        {

            return (!guid.HasValue || guid == new Guid());
        }

        public static bool IsFilled(this string str)
        {
            return !str.IsNullOrEmpty();
        }
        public static bool IsNullOrEmpty(this string str)
        {
            return ((str == null) || string.IsNullOrEmpty(str.Trim()));
        }

        public static bool IsWeekEnd(this DateTime date)
        {
            return ((date.DayOfWeek == DayOfWeek.Sunday) || (date.DayOfWeek == DayOfWeek.Saturday));
        }

        public static RedirectToRouteResult RedirectToActionWithQuery(this Controller controller, string actionName, string controllerName, IEnumerable<string> defaultRoutes)
        {
            RouteValueDictionary dictionary = new RouteValueDictionary();
            NameValueCollection query = controller.ControllerContext.HttpContext.Request.QueryString;
            foreach (string key in query.Keys)
            {
                if (defaultRoutes.Contains(key))
                {
                    dictionary.Add(key, query[key]);
                }
            }
            return (typeof(Controller).GetMethod("RedirectToAction", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string), typeof(RouteValueDictionary) }, null).Invoke(controller, new object[] { actionName, controllerName, dictionary }) as RedirectToRouteResult);
        }

        public static byte[] ToByteArray(this HttpPostedFileBase file)
        {
            byte[] buffer = new byte[0x8000];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while (true)
                {
                    read = file.InputStream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        return ms.ToArray();
                    }
                    ms.Write(buffer, 0, read);

                }
                /*
                                bool CS$4$0001;
                                goto Label_0045;
                            Label_0015:
                                read = file.InputStream.Read(buffer, 0, buffer.Length);
                                if (read <= 0)
                                {
                                    return ms.ToArray();
                                }
                                ms.Write(buffer, 0, read);
                            Label_0045:
                                CS$4$0001 = true;
                                goto Label_0015;
                */
            }
        }

        public static IDictionary<string, object> ToDictionary(this object obj)
        {
            BindingFlags attr = BindingFlags.Public | BindingFlags.Instance;
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach (PropertyInfo property in obj.GetType().GetProperties(attr))
            {
                if (property.CanRead)
                {
                    dict.Add(property.Name, property.GetValue(obj, null));
                }
            }
            return dict;
        }

        public static string ToHtml(this decimal value)
        {
            return value.ToString("f2").Replace(",", ".");
        }

        public static string ToNiceForm(this string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "";
            }
            if (str.Length == 1)
            {
                return str.ToUpper();
            }
            return (str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower());
        }

        public static string GeneratePassword(this Random rnd, int len = 6)
        {
            string pattern = "0123456789abcdefghijklmnopqrstuvwxyz";
            string pass = "";
            if (len == 0) len = 6;
            for (int i = 0; i < len; i++)
            {
                pass += pattern.Substring(rnd.Next(0, pattern.Length), 1);
            }
            return pass;
        }

        public static object ToNonAnonymousList<T>(this List<T> list, Type t)
        {
            object l = Activator.CreateInstance(typeof(List<>).MakeGenericType(new[] { t }));
            MethodInfo addMethod = l.GetType().GetMethod("Add");
            foreach (T item in list)
            {
                addMethod.Invoke(l, new[] { item.ToType(t) });
            }
            return l;
        }

        public static object ToType<T>(this object obj, T type)
        {
            object tmp = Activator.CreateInstance(Type.GetType(type.ToString()));
            foreach (PropertyInfo pi in obj.GetType().GetProperties())
            {
                try
                {
                    tmp.GetType().GetProperty(pi.Name).SetValue(tmp, pi.GetValue(obj, null), null);
                }
                catch
                {
                }
            }
            return tmp;
        }

        public static T ToTypedValue<T>(this object obj)
        {
            Type t = typeof(T);
            object tmp = Activator.CreateInstance(t);
            try
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof (Nullable<>))
                {

                    if (obj == null)
                    {
                        return (T) (object) null;
                    }
                    else
                    {
                        var nullableConverter = new NullableConverter(t);
                        t = nullableConverter.UnderlyingType;
                    }
                }
                tmp = Convert.ChangeType(obj, t);
            }
            catch(Exception e)
            {
                
            }
            
            return (T)tmp;
        }

        public static object ToTypedObject(this string text, Type type)
        {
            if (typeof(Enum).IsAssignableFrom(type))
            {
                try
                {
                    return Enum.Parse(type, text);
                }
                catch
                {
                    return null;
                }
            }
            try
            {
                return Convert.ChangeType(text, type);
            }
            catch
            {
                return null;
            }
        }

        public static DateTime WeekStarted(this DateTime date)
        {
            if (date.DayOfWeek != DayOfWeek.Monday)
            {
                DateTime current = date;
                while (current.DayOfWeek != DayOfWeek.Monday)
                {
                    current = current.AddDays(-1.0);
                }
                return current.Date;
            }
            return date.Date;
        }
    }
}

