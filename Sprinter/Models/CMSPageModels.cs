using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Caching;
using Sprinter.Extensions;
namespace Sprinter.Models
{
    [Serializable]
    public class JsTreeModel
    {
        public string data { get; set; }
        public JsTreeAttribute attr { get; set; }
        public List<JsTreeModel> children { get; set; }
    }

    [Serializable]
    public class JsTreeAttribute
    {

        public string id { get; set; }
        public string href { get; set; }
        public int uid { get; set; }
        public int priority { get; set; }

    }

    [MetadataType(typeof(CMSPageDataAnnotations))]
    public partial class CMSPage
    {
        private static List<CMSPage> _fullPageTable;
        public static List<CMSPage> FullPageTable
        {
            get
            {
                /*if (_fullPageTable == null)*/
                {
                    //сначала кеш

                    var cached = HttpRuntime.Cache.Get("FullPageTable");
                    if (cached != null && cached is List<CMSPage>)
                    {
                        _fullPageTable = cached as List<CMSPage>;
                    }
                    else
                    {


                        _fullPageTable =
                            new DB().getPageList(null).AsEnumerable().Select(
                                x =>
                                new CMSPage()
                                    {
                                        PageName = x.PageName,
                                        FullName = x.FullName,
                                        FullUrl = x.FullURL,
                                        TreeLevel = x.TreeLevel ?? 0,
                                        Type = x.Type ?? 0,
                                        ParentID = x.ParentID,
                                        ID = x.ID ?? 0,
                                        URL = x.URL,
                                        BreadCrumbs = x.BreadCrumbs,
                                        LinkedBreadCrumbs = x.LinkedBreadCrumbs,
                                        ActiveCount = x.ActiveCount ?? 0,
                                        AllCount = x.AllCount ?? 0,
                                        Visible = x.Visible ?? false,
                                        ViewMenu = x.ViewMenu ?? false,
                                        OrderNum = x.OrderNum??0,
                                        OriginalURL = x.OriginalURL
                                    }).ToList();
/*
                        HttpRuntime.Cache.Insert("FullPageTable",
                                                 _fullPageTable,
                                                 new SqlCacheDependency("Sprinter", "Pages"),
                                                 DateTime.Now.AddDays(1D),
                                                 Cache.NoSlidingExpiration);
*/
                        /*HttpRuntime.Cache.Add("FullPageTable", _fullPageTable, null,
                                              Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(2),
                                              CacheItemPriority.High, null);*/
                    }
                }
                return _fullPageTable;
            }
            set
            {
                if (value == null)
                {
                    HttpRuntime.Cache.Remove("FullPageTable");
                }
            }
        }

        public class CMSPageDataAnnotations
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения"), DisplayName("Название раздела"), /*StringLength(100, ErrorMessage = "{0} должен содержать минимум {2} символов.", MinimumLength = 6)*/]
            public string PageName { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения"), DisplayName("URL раздела"), /*StringLength(100, ErrorMessage = "{0} должен содержать минимум {2} символов.", MinimumLength = 6)*/]
            [RegularExpression(@"^([a-zA-Z0-9_\-\.]+)", ErrorMessage = "Поле '{0}' должно содержать только буквы английского алфавита и цифры")]
            public string URL { get; set; }

            [DisplayName("Заголовок страницы")]
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            public string FullName { get; set; }

            [DisplayName("Тайтл")]
            public string Title { get; set; }

            [DisplayName("Ключевые слова")]
            public string Keywords { get; set; }

            [DisplayName("Описание")]
            public string Description { get; set; }

            [DisplayName("Родительский раздел"), Required]
            public string ParentID { get; set; }

            [DisplayName("Отображать на сайте"), DefaultValue(true)]
            public string Visible { get; set; }

            [DisplayName("Отображать в верхнем меню"), DefaultValue(true)]
            public string ViewMenu { get; set; }

            [DisplayName("Тип раздела"), Required]
            public string Type { get; set; }
        }


        public PageTextData TextData
        {
            get
            {
                if (PageType.TypeName == "TextPage")
                {
                    if (PageTextDatas.Any()) return PageTextDatas.First();
                    else
                    {
                        var data = new PageTextData() { CMSPage = this, ShowHeader = true, Text = "" };
                        return data;
                    }
                }
                return null;
            }
        }


        private List<string> _urlPath;
        public List<string> UrlPath
        {
            get
            {
                if (_urlPath == null)
                {
                    _urlPath =
                        FullPageTable.First(x => x.URL == URL).FullUrl.Split(new string[] { "/" },
                                                                                        StringSplitOptions.
                                                                                            RemoveEmptyEntries).ToList();
                }
                return _urlPath;
            }
            set { _urlPath = value; }

        }

        public IEnumerable<CMSPage> GetHalfOfChildren(bool left, int limit = 100)
        {
            var all = FullPageTable.Where(x => x.ParentID == ID).Take(limit).ToList();
            int leftCount = 0;
            int rem = 0;
            leftCount = Math.DivRem(all.Count(), 2, out rem);
            if (rem > 0)
                leftCount++;
            if (left)
                return all.Take(leftCount);
            return all.Skip(leftCount);
            
        }

        private int? _treeLevel;

        public int TreeLevel
        {
            get
            {
                if (!_treeLevel.HasValue)
                {
                    var item = FullPageTable.FirstOrDefault(x => x.ID == ID);
                    if (item == null)
                        return 0;
                    _treeLevel = item.TreeLevel;
                }
                return _treeLevel.Value;
            }
            set { _treeLevel = value; }
        }


        private string _fullUrl;
        public string FullUrl
        {
            get
            {
                if (_fullUrl.IsNullOrEmpty())
                    _fullUrl = string.Format("/{0}", string.Join("/", UrlPath.ToArray()));
                return _fullUrl;
            }
            set { _fullUrl = value; }
        }

        private string _breadCrumbs;
        public string BreadCrumbs
        {
            get
            {
                if (_breadCrumbs == null)
                {
                    var first = FullPageTable.First(x => x.ID == ID);
                    if (first == null)
                        _breadCrumbs = "";
                    else
                        _breadCrumbs = first.BreadCrumbs;
                }

                return _breadCrumbs;
            }
            set { _breadCrumbs = value; }
        }

        private string _linkedBreadCrumbs;
        public string LinkedBreadCrumbs
        {
            get
            {
                if (_linkedBreadCrumbs == null)
                {
                    var first = FullPageTable.First(x => x.ID == ID);
                    if (first == null)
                        _linkedBreadCrumbs = "";
                    else
                    {
                        _linkedBreadCrumbs = first.LinkedBreadCrumbs;
                        _linkedBreadCrumbs =
                            string.Join("&mdash;",
                                        _linkedBreadCrumbs.Split(new[] { "&mdash;" },
                                                                 StringSplitOptions.RemoveEmptyEntries).Select(
                                                                     x =>
                                                                     x.Split(new[] { ";" },
                                                                             StringSplitOptions.RemoveEmptyEntries)).
                                            Select(
                                                x =>
                                                string.Format(x[1],
                                                              "/" +
                                                              FullPageTable.First(z => z.ID == int.Parse(x[0])).FullUrl)));
                    }
                }

                return _linkedBreadCrumbs;
            }
            set { _linkedBreadCrumbs = value; }
        }


        private void FillChildren(CMSPage cmsPage)
        {
            var childs = FullPageTable.Where(x => x.ParentID == cmsPage.ID).ToList();
            if (childs.Any())
                _fullChildrenList.AddRange(childs.Select(x => x.ID));
            foreach (var child in childs)
            {
                FillChildren(child);
            }
        }

        private int? _sectionCount;
        public int SectionCount
        {
            get
            {
                if (!_sectionCount.HasValue)
                {
                    //var countData = BookSaleCatalog.BookCountList.FirstOrDefault(x => x.PageID == ID);
                    //if (countData != null)
                    //   _sectionCount = countData.SectionCount;
                    //return 0;
                    _sectionCount = ActiveCount;
                }
                return _sectionCount.Value;
            }
            set { _sectionCount = value; }
        }

        public string FullPath
        {
            get
            {
                if (URL == "catalog") return "Каталог";
                return string.Format("{0} &mdash; {1}", FullPageTable.First(x => x.URL == "catalog").PageName, BreadCrumbs);
            }
        }
        private List<int> _fullChildrenList;
        public List<int> FullChildrenList
        {
            get
            {
                if (_fullChildrenList == null)
                {
                    _fullChildrenList = new List<int>();
                    FillChildren(this);

                }
                return _fullChildrenList;
            }
            set { _fullChildrenList = value; }
        }

        public IEnumerable<int> ShortChildrenList(int count)
        {
            var list = new List<int>();
            list.Add(ID);
            FillChildrenList(this, ref list, count + 1);
            return list.Take(count + 1);
        }

        private void FillChildrenList(CMSPage cmsPage, ref List<int> list, int count)
        {
            var childs = FullPageTable.Where(x => x.ParentID == cmsPage.ID).ToList();
            if (childs.Any())
                list.AddRange(childs.Select(x => x.ID));
            if (list.Count >= count)
                return;
            foreach (var child in childs)
            {
                FillChildrenList(child, ref list, count);
            }

        }
    }
}