using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Routing;
using Sprinter.Extensions;
using Sprinter.Models;
using Sprinter.Models.ViewModels;

namespace Sprinter.Controllers
{
    public class CatalogController : Controller
    {
        readonly DB _db = new DB();

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult CommentList(int? page)
        {
            return
                View(new PagedData<BookComment>(
                         _db.BookComments.Where(x => !x.Approved).OrderByDescending(x => x.Date), page ?? 0, 50));
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult MarginPublisherEdit(int? partner, int? page)
        {

            var dp = _db.Partners.FirstOrDefault(x => x.ID == partner);
            if (dp == null || !dp.PublisherList.Any()) return RedirectToAction("PartnerList");
            ViewBag.Publishers = new SelectList(dp.PublisherList, "ID", "Name");
            var dict = new RouteValueDictionary { { "partner", partner } };
            var paged = new PagedData<BookPublisher>(dp.PublisherList, page ?? 0, 50, dict);
            return View(paged);
        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult MarginPublisherEdit(int? partner, int? page, FormCollection collection)
        {
            if (!partner.HasValue) return RedirectToAction("PartnerList");
            var keys = collection.AllKeys.Where(x => x.StartsWith("Margin_")).Select(x => x.Replace("Margin_", ""));
            foreach (var key in keys)
            {
                var margin = _db.BookPublisherMargins.FirstOrDefault(x => x.PartnerID == partner && x.PublisherID == key.ToInt());
                var marginValue = collection["Margin_" + key].ToDecimal();
                var discountValue = collection["Discount_" + key].ToDecimal();
                if (marginValue > 0 || discountValue > 0)
                {
                    if (margin == null)
                    {
                        margin = new BookPublisherMargin { PartnerID = partner.Value, PublisherID = key.ToInt() };
                        _db.BookPublisherMargins.InsertOnSubmit(margin);
                    }
                    margin.Margin = marginValue;
                    margin.Discount = discountValue;
                }
                else if (margin != null)
                {
                    _db.BookPublisherMargins.DeleteOnSubmit(margin);
                }
            }
            _db.SubmitChanges();
            ModelState.AddModelError("", "Данные успешно сохранены.");
            return MarginPublisherEdit(partner, page);
        }

        [HttpGet]
        [AuthorizeMaster]
        public ActionResult Editor(int? id, int? bookID, int? type, string ReturnURL)
        {
            if (!id.HasValue)
                return Redirect(ReturnURL);
            return View(new CatalogEditor(id.Value, type, bookID));
        }

        [HttpPost]
        [AuthorizeMaster]
        public ContentResult SaveField(string field, int id, string value)
        {
            var result = new ContentResult();
            var sale = _db.BookSaleCatalogs.FirstOrDefault(x => x.ID == id);
            if (sale == null || (value.IsFilled() && !value.ToTypedValue<decimal?>().HasValue))
                result.Content = "";
            else
            {
                sale.SetPropertyValue(field, value.ToTypedValue<decimal?>());
                result.Content =
                    (sale.PriceOverride.HasValue ? sale.PriceOverride.Value : sale.TradingPrice).ToString("f2");
                _db.SubmitChanges();
            }
            return result;

        }

        [HttpPost]
        [AuthorizeMaster]
        public ActionResult Editor(int? id, int? bookID, int? type, string ReturnURL, FormCollection collection, HttpPostedFileBase coverFile)
        {
            if (!id.HasValue)
                return Redirect(ReturnURL);

            var sid = collection["ID"].ToInt();
            var saleItem = sid > 0 ? _db.BookSaleCatalogs.FirstOrDefault(x => x.ID == sid) : new BookSaleCatalog();
            if (saleItem == null)
                return Redirect(ReturnURL);

            if (type.HasValue)
            {
                saleItem = new BookSaleCatalog();
                _db.BookSaleCatalogs.InsertOnSubmit(saleItem);
            }

            BookDescriptionCatalog description = saleItem.BookDescriptionCatalog;

            if (type == 1 && bookID.HasValue)
            {

                description = _db.BookDescriptionCatalogs.FirstOrDefault(x => x.ID == bookID);
            }
            else if (type == 2)
            {
                description = new BookDescriptionCatalog();
                description.ProviderUID = "";
                var partner = _db.Partners.First(x => x.ID == collection["Partner"].ToInt());
                description.DataSourceID =
                    _db.BookDescriptionProviders.First(x => x.ProviderName == partner.Name).ID;
                _db.BookDescriptionCatalogs.InsertOnSubmit(description);
            }
            if (description == null)
                return Redirect(ReturnURL);

            if (sid == 0 || type.HasValue)
            {
                saleItem.BookDescriptionCatalog = description;
                saleItem.LastUpdate = DateTime.Now;
                _db.BookSaleCatalogs.InsertOnSubmit(saleItem);
            }

            if (coverFile != null && coverFile.ContentLength > 0)
            {
                var buffer = new byte[coverFile.InputStream.Length];
                coverFile.InputStream.Read(buffer, 0, buffer.Length);
                var ms = new MemoryStream(buffer);
                try
                {
                    var bmp = new Bitmap(ms);
                    BookCover cover;
                    if (description.CoverID.HasValue)
                        cover = description.BookCover;
                    else
                    {
                        cover = new BookCover();
                        description.BookCover = cover;
                        _db.BookCovers.InsertOnSubmit(cover);
                    }
                    cover.Name = "";
                    cover.Width = bmp.Width;
                    cover.Height = bmp.Height;
                    cover.Data = buffer;

                }
                catch (Exception e)
                {
                    ModelState.AddModelError("", e.Message);
                }
            }
            saleItem.PartnerID = collection["Partner"].ToInt();
            saleItem.PartnerUID = collection["PartnerUID"];
            saleItem.PartnerPrice = collection["PartnerPrice"].ToDecimal();
            int section = collection["Section"].ToInt();
            var saleList = saleItem.BookDescriptionCatalog.BookSaleCatalogs.ToList();
            if (section > 0)
            {
                foreach (var sale in saleList)
                {
                    if (sale.BookPageRels.Any())
                        sale.BookPageRels.First().PageID = section;
                    else
                        _db.BookPageRels.InsertOnSubmit(new BookPageRel { BookSaleCatalog = sale, PageID = section });
                }
            }
            else
            {
                foreach (var sale in saleList)
                {
                    if (sale.BookPageRels.Any())
                        _db.BookPageRels.DeleteAllOnSubmit(sale.BookPageRels);
                }
            }

            var priceOverride = collection["PriceOverride"].ToDecimal();
            if (priceOverride > 0)
                saleItem.PriceOverride = priceOverride;
            else saleItem.PriceOverride = null;

            var marginOverride = collection["Margin"].ToDecimal();
            saleItem.Margin = marginOverride;

            saleItem.IsAvailable = collection["IsAvailable"].ToBool();
            saleItem.IsNew = collection["IsNew"].ToBool();
            saleItem.IsSpec = collection["IsSpec"].ToBool();
            saleItem.IsTop = collection["IsTop"].ToBool();


            saleItem.BookDescriptionCatalog.Header = collection["Header"];
            saleItem.BookDescriptionCatalog.ISBN = collection["ISBN"];
            saleItem.BookDescriptionCatalog.Annotation = collection["Annotation"];
            saleItem.BookDescriptionCatalog.BookType = collection["BookType"];
            saleItem.BookDescriptionCatalog.PublishYear = ImportData.ParseYear(collection["PublishYear"]);
            saleItem.BookDescriptionCatalog.PageCount = (int?)ImportData.ParseInt(collection["PageCount"]);
            var ean = EAN13.IsbnToEan13(EAN13.ClearIsbn(collection["ISBN"]));
            saleItem.BookDescriptionCatalog.EAN = long.Parse(ean);


            _db.SubmitChanges();

            saleItem.BookDescriptionCatalog.ChangeAuthorsList(ImportData.CreateAuthorsList(collection["Authors"]));
            saleItem.BookDescriptionCatalog.ChangePublisher(collection["Publisher"]);

            return Redirect(Url.Action("Editor", new { ID = saleItem.ID }));


        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult FilterEdit(int? id)
        {
            return View(_db.CMSPages.FirstOrDefault(x => x.ID == id));
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult CatalogSearchForm()
        {
            var sd = MasterSearchData.InitFromQuery(Request.QueryString.ToString());
            return View(sd);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult CatalogSearchForm(FormCollection collection)
        {
            var sd = MasterSearchData.InitFromQuery(Request.QueryString.ToString());
            UpdateModel(sd);
            return
                Redirect(Url.Action(sd.RedirectAction, "Catalog",
                                    sd.CreateRoutes(Request.QueryString.ToString())));

        }


        [AuthorizeMaster]
        [HttpGet]
        public ActionResult TagEdit(int? page)
        {
            var msd = MasterSearchData.InitFromQuery(Request.QueryString.ToString());
            msd.RedirectAction = "TagEdit";

            var vm = new MasterSearchViewModel
                {
                    AdditionalFilterModel = null,
                    HasCheckColumn = true,
                    SearchData = msd,
                    Page = page ?? 0
                };

            vm.SearchData.MainFilter = vm;

            return View(vm);

        }


        #region Обработка событий создания и редактирования фильтров каталога
        [AuthorizeMaster]
        [HttpGet]
        public ActionResult SimpleFilterEdit(int? ID, int page)
        {
            ID = (ID ?? 0);
            TagSimpleFilter filter = (ID == -1 ? new TagSimpleFilter { ID = -1 } : _db.TagSimpleFilters.FirstOrDefault(x => x.PageID == page)) ??
                                     new TagSimpleFilter();

            ViewBag.ExistFilters = new SelectList(_db.TagSimpleFilters.OrderBy(x => x.Name), "ID", "Name");
            return View(filter);
        }
        [AuthorizeMaster]
        [HttpPost]
        public ActionResult SimpleFilterEdit(int? ID, int page, int? Exist, FormCollection collection)
        {
            if (ID == -1)
            {
                if (Exist.HasValue)
                {
                    var exFilter = _db.TagSimpleFilters.FirstOrDefault(x => x.ID == Exist);
                    if (exFilter == null)
                        return RedirectToAction("FilterEdit", new { ID = page });
                    var cmsPage = _db.CMSPages.FirstOrDefault(x => x.ID == page);
                    var newFilter = new TagSimpleFilter
                        {
                            Name =
                                string.Format("{0} на странице {1}", exFilter.Name,
                                              cmsPage.PageName)
                        ,
                            Visible = exFilter.Visible,
                            PageID = page,

                        };
                    _db.TagSimpleFilters.InsertOnSubmit(newFilter);

                    foreach (var list in exFilter.TagSimpleFilterItems)
                    {
                        var newList = new TagSimpleFilterItem
                            {
                                TagID = list.TagID,
                                TagSimpleFilter = newFilter
                            };
                        _db.TagSimpleFilterItems.InsertOnSubmit(newList);
                    }
                }
            }
            else
            {
                TagSimpleFilter filter;
                if (ID == 0)
                {
                    filter = new TagSimpleFilter { PageID = page };
                    _db.TagSimpleFilters.InsertOnSubmit(filter);
                }
                else
                {
                    filter = _db.TagSimpleFilters.FirstOrDefault(x => x.ID == ID);
                    if (filter != null) _db.TagSimpleFilterItems.DeleteAllOnSubmit(filter.TagSimpleFilterItems);
                }
                UpdateModel(filter);

                _db.SubmitChanges();

                _db.TagSimpleFilterItems.InsertAllOnSubmit(
                    filter.TagList.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(
                        x => _db.BookTags.FirstOrDefault(y => y.Tag.ToLower().Trim() == x.ToLower().Trim())).AsEnumerable
                        ().Where(x => x != null).Select(
                            x => new TagSimpleFilterItem { BookTag = x, TagSimpleFilter = filter }));

            }
            _db.SubmitChanges();
            return RedirectToAction("FilterEdit", new { ID = page });
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult ComplexFilterEdit(int? ID, int page)
        {
            ID = (ID ?? 0);
            TagComplexFilter filter = (ID == -1 ? new TagComplexFilter { ID = -1 } : _db.TagComplexFilters.FirstOrDefault(x => x.PageID == page)) ??
                                      new TagComplexFilter();

            ViewBag.ExistFilters = new SelectList(_db.TagComplexFilters.OrderBy(x => x.Name), "ID", "Name");
            return View(filter);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult ComplexFilterEdit(int? ID, int page, int? Exist, FormCollection collection)
        {
            if (ID == -1)
            {
                if (Exist.HasValue)
                {
                    var exFilter = _db.TagComplexFilters.FirstOrDefault(x => x.ID == Exist);
                    if (exFilter == null)
                        return RedirectToAction("FilterEdit", new { ID = page });
                    var cmsPage = _db.CMSPages.FirstOrDefault(x => x.ID == page);
                    var newFilter = new TagComplexFilter
                        {
                            Name =
                                string.Format("{0} на странице {1}", exFilter.Name,
                                              cmsPage.PageName)
                                            ,
                            Visible = exFilter.Visible,
                            PageID = page,

                        };
                    _db.TagComplexFilters.InsertOnSubmit(newFilter);

                    foreach (var list in exFilter.TagComplexFilterLists)
                    {
                        var newList = new TagComplexFilterList
                            {
                                TagComplexFilter = newFilter,
                                DefaultValue = list.DefaultValue,
                                ItemHeader = list.ItemHeader
                            };
                        _db.TagComplexFilterLists.InsertOnSubmit(newList);

                        _db.TagComplexFilterItems.InsertAllOnSubmit(
                            list.TagComplexFilterItems.AsEnumerable().Select(
                                x => new TagComplexFilterItem { TagID = x.TagID, TagComplexFilterList = newList }));
                    }
                }
            }
            else
            {
                TagComplexFilter filter;
                if (ID == 0)
                {
                    filter = new TagComplexFilter { PageID = page };
                    _db.TagComplexFilters.InsertOnSubmit(filter);
                }
                else
                {
                    filter = _db.TagComplexFilters.FirstOrDefault(x => x.ID == ID);
                }
                UpdateModel(filter);

                for (int i = 0; i < 10; i++)
                {
                    var listID = (int)collection.GetValue("ListID_" + i).ConvertTo(typeof(int));
                    var itemHeader = collection.GetValue("ItemHeader_" + i).AttemptedValue;
                    var defaultValue = collection.GetValue("DefaultValue_" + i).AttemptedValue;
                    var tagList = collection.GetValue("TagList_" + i).AttemptedValue;

                    if (listID > 0)
                    {
                        var dbList = _db.TagComplexFilterLists.FirstOrDefault(x => x.ID == listID);
                        if (dbList != null)
                        {
                            if (itemHeader.IsNullOrEmpty() && defaultValue.IsNullOrEmpty() && tagList.IsNullOrEmpty())
                            {
                                _db.TagComplexFilterLists.DeleteOnSubmit(dbList);
                            }
                            else
                            {
                                dbList.ItemHeader = itemHeader;
                                dbList.DefaultValue = defaultValue;

                                _db.TagComplexFilterItems.DeleteAllOnSubmit(dbList.TagComplexFilterItems);
                                _db.SubmitChanges();

                                var tags =
                                    tagList.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(
                                        x => x.Trim());

                                _db.TagComplexFilterItems.InsertAllOnSubmit(
                                    tags.Select(
                                        x => _db.BookTags.FirstOrDefault(y => y.Tag.ToLower().Trim() == x.ToLower())).AsEnumerable().
                                        Where(x => x != null).Select(
                                            x => new TagComplexFilterItem { BookTag = x, FilterListID = dbList.ID }));

                            }


                        }
                    }
                    else
                    {
                        if (!itemHeader.IsNullOrEmpty() && !defaultValue.IsNullOrEmpty() && !tagList.IsNullOrEmpty())
                        {
                            var dbList = new TagComplexFilterList
                                {
                                    DefaultValue = defaultValue,
                                    ItemHeader = itemHeader,
                                    TagComplexFilter = filter
                                };

                            _db.TagComplexFilterLists.InsertOnSubmit(dbList);
                            var tags =
                                tagList.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(
                                    x => x.Trim());

                            _db.TagComplexFilterItems.InsertAllOnSubmit(
                                tags.Select(
                                    x => _db.BookTags.FirstOrDefault(y => y.Tag.ToLower().Trim() == x.ToLower())).AsEnumerable().
                                    Where(x => x != null).Select(
                                        x => new TagComplexFilterItem { BookTag = x, TagComplexFilterList = dbList }));
                        }
                    }
                }

            }
            _db.SubmitChanges();
            return RedirectToAction("FilterEdit", new { ID = page });
        }

        #endregion

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Index(int? ViewMode, string SectionPath, int? page)
        {
            var msd = MasterSearchData.InitFromQuery(Request.QueryString.ToString());
            msd.RedirectAction = "Index";

            var data = new CatalogDistributionFilter(ViewMode, SectionPath, msd.Search());

            var vm = new MasterSearchViewModel
                {
                    AdditionalFilterModel = data,
                    HasCheckColumn = true,
                    SearchData = msd,
                    Page = page ?? 0
                };
            vm.SearchData.MainFilter = vm;

            return View(vm);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Index(int? ViewMode, string SectionPath, int? page, FormCollection collection)
        {
            string qs = Request.QueryString.ToString();
            var msm = MasterSearchData.InitFromQuery(qs);
            var routes = msm.CreateRoutes();
            routes.Add("ViewMode", ViewMode);
            routes.Add("SectionPath", SectionPath);
            routes.Add("page", page);
            return RedirectToAction("Index", routes);

        }

        [HttpGet]
        [AuthorizeMaster]
        public JsonResult TagList(string term)
        {
            var result = new JsonResult { JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            if (!term.IsNullOrEmpty())
            {
                var data =
                    _db.BookTags.Where(x => x.Tag.ToLower().StartsWith(term.ToLower())).OrderBy(x => x.Tag).Select(
                        x => x.Tag).Take(10).ToList();

                result.Data = data;
            }
            else
            {
                result.Data = new List<string> { "" };
            }
            return result;

        }

        [HttpPost]
        [AuthorizeMaster]
        public ContentResult SaveTag(string items, string tag, int arg)
        {
            var dbTag = _db.BookTags.FirstOrDefault(x => x.Tag.ToLower() == tag.ToLower());
            var ids =
                items.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToInt()).Distinct().ToList();
            if (arg == 0)//удаление
            {
                if (dbTag != null)
                {
                    var rels = _db.BookTagRels.Where(x => x.TagID == dbTag.ID && ids.Contains(x.DescriptionID));
                    _db.BookTagRels.DeleteAllOnSubmit(rels);
                    _db.SubmitChanges();
                }
                return new ContentResult { Content = "Тег успешно удален." };
            }
            if (dbTag == null)
            {
                dbTag = new BookTag { Tag = tag };
                _db.BookTags.InsertOnSubmit(dbTag);
                _db.BookTagRels.InsertAllOnSubmit(
                    ids.Select(x => new BookTagRel { BookTag = dbTag, DescriptionID = x }));
            }
            else
            {
                foreach (int id in ids)
                {
                    var dbRel = _db.BookTagRels.FirstOrDefault(x => x.TagID == dbTag.ID && x.DescriptionID == id);
                    if (dbRel == null)
                    {
                        _db.BookTagRels.InsertOnSubmit(new BookTagRel { TagID = dbTag.ID, DescriptionID = id });
                    }
                }
            }
            _db.SubmitChanges();
            return new ContentResult { Content = "Тег успешно добавлен." };
        }


        [HttpPost]
        [AuthorizeMaster]
        public ContentResult DeleteData(string items)
        {
            var bookIDs =
                items.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim()));

            foreach (var bookID in bookIDs)
            {
                var saleCat = _db.BookSaleCatalogs.FirstOrDefault(x => x.ID == bookID);
                if (saleCat != null)
                {
                    if (saleCat.ShopCartItems.Any() || (saleCat.BookDescriptionCatalog != null && saleCat.BookDescriptionCatalog.OrderedBooks.Any()))
                    {
                        saleCat.IsAvailable = false;
                    }
                    else
                    {
                        _db.BookSaleCatalogs.DeleteOnSubmit(saleCat);
                    }
                }
            }
            _db.SubmitChanges();

            return new ContentResult();


        }

        [HttpPost]
        [AuthorizeMaster]
        public ContentResult SaveData(string sections, string items)
        {
            var bookIDs =
                items.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim())).ToList();
            var pageIDs =
                sections.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(
                    x => int.Parse(x.Trim())).ToList();


            foreach (var bookID in bookIDs)
            {
                int id = bookID;
                var saleList = _db.BookSaleCatalogs.First(x => x.ID == id).BookDescriptionCatalog.BookSaleCatalogs.ToList();
                foreach (var sale in saleList)
                {
                    _db.BookPageRels.DeleteAllOnSubmit(_db.BookPageRels.Where(x => x.SaleCatalogID == sale.ID));
                }

            }
            _db.SubmitChanges();

            foreach (var bookID in bookIDs)
            {
                int id = bookID;
                var saleList = _db.BookSaleCatalogs.First(x => x.ID == id).BookDescriptionCatalog.BookSaleCatalogs.ToList();
                foreach (var sale in saleList)
                {
                    _db.BookPageRels.InsertAllOnSubmit(
                        pageIDs.Select(x => new BookPageRel { PageID = x, SaleCatalogID = sale.ID }).ToList());

                }

            }
            _db.SubmitChanges();

            return new ContentResult();
        }

        [AuthorizeMaster]
        [HttpGet]
        public JsonResult GetTreeData()
        {
            JsTreeModel model;
            var result = new JsonResult();
            var cached = HttpRuntime.Cache.Get("TreeData");
            if (cached is JsTreeModel)
            {
                model = cached as JsTreeModel;
            }
            else
            {

                var rootPage = _db.CMSPages.First(x => x.URL == "catalog" && x.PageType.TypeName == "Catalog");

                var root = new JsTreeModel
                    {
                        data = rootPage.PageName,
                        attr = new JsTreeAttribute { id = "x" + rootPage.ID, href = "#", uid = rootPage.ID },
                        children = new List<JsTreeModel>()
                    };
                FillModel(ref root, rootPage.ID);

                HttpRuntime.Cache.Insert("TreeData",
                         root,
                         new SqlCacheDependency("Sprinter", "Pages"),
                         DateTime.Now.AddDays(1D),
                         Cache.NoSlidingExpiration);

                model = root;
            }
            result.Data = model;
            result.JsonRequestBehavior = JsonRequestBehavior.AllowGet;
            result.ContentType = "application/json";
            return result;
        }

        private void FillModel(ref JsTreeModel model, int? id)
        {

            var pages = CMSPage.FullPageTable.Where(x => id == null ? !x.ParentID.HasValue : x.ParentID == id && x.Type == 1).OrderBy(x => x.OrderNum);
            foreach (var cmsPage in pages)
            {
                var child = new JsTreeModel
                    {
                        data = cmsPage.PageName,
                        attr =
                            new JsTreeAttribute
                                {
                                    id = "x" + cmsPage.ID,
                                    href = Url.Action("Edit", "Pages", new { ID = cmsPage.ID }),
                                    uid = cmsPage.ID,
                                    priority = _db.PartnerPriorities.Count(x => x.PageID == cmsPage.ID)
                                },

                    };
                if (model.children == null)
                    model.children = new List<JsTreeModel>();
                model.children.Add(child);
                FillModel(ref child, cmsPage.ID);
            }
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult PartnerList()
        {
            return View(_db.Partners.OrderBy(x => x.SalePriority));
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult PartnerEdit(int? ID)
        {
            var partner = _db.Partners.FirstOrDefault(x => x.ID == ID) ?? new Partner();
            //return RedirectToAction("PartnerList");
            return View(partner);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult PartnerEdit(int? ID, FormCollection collection)
        {
            bool isNew = false;
            var partner = _db.Partners.FirstOrDefault(x => x.ID == ID);
            if (partner == null)
            {
                isNew = true;
                partner = new Partner();
                _db.Partners.InsertOnSubmit(partner);
                //return RedirectToAction("PartnerList");
            }
            
            UpdateModel(partner);
            if (isNew)
            {
                partner.Name = partner.Description;
            }
            _db.SubmitChanges();

            return RedirectToAction("PartnerList");
        }


        [AuthorizeMaster]
        [HttpGet]
        public ActionResult MarginEdit(int? Type)
        {
            return View(new MarginEditorModel(Type));
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult MarginEdit(int Type, decimal Margin, string IDs, FormCollection collection)
        {
            var idList =
                IDs.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

            IQueryable<BookSaleCatalog> books = Type == 1 ? _db.BookSaleCatalogs.Where(x => x.BookPageRels.Any(y => idList.Contains(y.PageID))) : _db.BookSaleCatalogs.Where(x => x.BookDescriptionCatalog.BookTagRels.Any(y => idList.Contains(y.TagID)));

            foreach (var book in books)
            {
                book.Margin = Margin;
            }

            _db.SubmitChanges();

            ViewBag.Message = "Данные успешно сохранены.";

            return View(new MarginEditorModel(Type));
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult PriceCheck(int? page, bool? overpriced, int? concurrentId)
        {
            var msvm = new MasterSearchViewModel();
            string qs = Request.QueryString.ToString();
            msvm.SearchData = MasterSearchData.InitFromQuery(qs);
            msvm.SearchData.MainFilter = msvm;
            msvm.SearchData.RedirectAction = "PriceCheck";
            msvm.HasCheckColumn = true;
            msvm.AdditionalFilterModel = new CatalogOverPriceFilter(overpriced ?? true);

            var routes = msvm.CommonRoutes;
            routes["overpriced"] = !(overpriced ?? true);
            ViewBag.Link =
                "<a href=\"{0}\">{1}</a>".FormatWith(Url.Action("PriceCheck", routes),
                                                     (overpriced ?? true)
                                                         ? "Смотреть товары, с ценой больше минимальной цены конкурентов"
                                                         : "Смотреть товары, с ценой больше максимальной цены конкурентов");



            ViewBag.ValueType =
                new SelectList(new[] {new {Key = "0", Value = "процентов"}, new {Key = "1", Value = "рублей"}}, "Key",
                               "Value", "0");

            var concList = _db.BookDescriptionProviders.Where(x => x.IsPriceProvider).OrderBy(x => x.ProviderName).ToList();
            concList.Insert(0, new BookDescriptionProvider() { ProviderName = "Максимальной цены конкурентов", ID = -2 });
            concList.Insert(1, new BookDescriptionProvider(){ProviderName = "Минимальной цены конкурентов", ID = -1});
            ViewBag.ConcurentList = new SelectList(concList, "ID", "ProviderName", "-2");
            return View(msvm);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ContentResult SaveMargin(string items, decimal margin)
        {
            var itemList = items.Split<int>();
            foreach (var dItem in itemList.Select(item => _db.BookSaleCatalogs.FirstOrDefault(x => x.ID == item)).Where(dItem => dItem != null))
            {
                dItem.Margin = margin;
            }
            _db.SubmitChanges();
            return new ContentResult { Content = "Данные успешно сохранены." };
        }

        [AuthorizeMaster]
        [HttpPost]
        public ContentResult OverrideMargin(string items, decimal @override, int type, int target)
        {
            if (@override == 0)
            {
                  return new ContentResult { Content = "Необходимо указать значение, отличное от нуля." };
            }
            int count = 0;
            var itemList = items.Split<int>();
            foreach (var item in itemList)
            {
                var dbItem = _db.BookSaleCatalogs.FirstOrDefault(x => x.ID == item);
                if (dbItem != null)
                {
                    decimal? targetPrice = null;
                    if (dbItem.BookDescriptionCatalog.BookPrices.Any())
                    {
                        if (target == -2)
                        {
                            targetPrice = dbItem.BookDescriptionCatalog.BookPrices.Max(x => x.Price);
                        }
                        else if (target == -1)
                        {
                            targetPrice = dbItem.BookDescriptionCatalog.BookPrices.Min(x => x.Price);
                        }
                        else
                        {
                            var requiredPrice =
                                dbItem.BookDescriptionCatalog.BookPrices.FirstOrDefault(x => x.ProviderID == target);
                            if (requiredPrice != null)
                                targetPrice = requiredPrice.Price;
                        }
                    }
                    if (targetPrice!=null)
                    {
                        if (type == 0)//Процент
                        {
                            targetPrice = targetPrice + (targetPrice * @override / 100);
                        }
                        else if (type == 1)
                        {
                            targetPrice = targetPrice + @override;
                        }
                        dbItem.PriceOverride = targetPrice;
                        count++;
                    }

                }

            }
            _db.SubmitChanges();
            return new ContentResult
                {
                    Content = "Данные успешно сохранены. Цены переопределены для " + count + " записей каталога."
                };
        }


        [AuthorizeMaster]
        [HttpGet]
        public ActionResult PriorityEdit(int? PageID)
        {
            var pem = new PriorityEditorModel(PageID);
            return View(pem);
        }
        [AuthorizeMaster]
        [HttpPost]
        public ActionResult PriorityEdit(int PageID, FormCollection collection)
        {
            var keys = collection.AllKeys;
            int val;

            var incorrect = keys.Select(x => int.TryParse(collection[x], out val)).Count(x => !x);
            if (incorrect > 0)
            {
                ModelState.AddModelError("", "Все приоритеты должны представлять собой целое число.");
                return View("PriorityEdit", new PriorityEditorModel(PageID));
            }

            foreach (var key in keys)
            {
                var p = _db.PartnerPriorities.FirstOrDefault(x => x.PageID == PageID && x.PartnerID == key.ToInt());
                if (collection[key].ToInt() > 0)
                {

                    if (p == null)
                    {
                        p = new PartnerPriority { PageID = PageID, PartnerID = key.ToInt() };
                        _db.PartnerPriorities.InsertOnSubmit(p);
                    }
                    p.Priority = collection[key].ToInt();
                }
                else if (p != null)
                {
                    _db.PartnerPriorities.DeleteOnSubmit(p);
                }
            }
            _db.SubmitChanges();
            ModelState.AddModelError("", "Все приоритеты успешно сохранены.");
            return View("PriorityEdit", new PriorityEditorModel(PageID));
        }

        [AuthorizeMaster]
        [HttpGet]
        public ContentResult TagItemList(int bid)
        {
            var result = new ContentResult();
            var d = _db.BookDescriptionCatalogs.FirstOrDefault(x => x.ID == bid);
            if (d != null)
            {
                result.Content = string.Join("",
                                d.BookTagRels.Select(
                                    x =>
                                    " <a class=\"tagl\" href=\"/\">{0}</a>".FormatWith(x.BookTag.Tag)));
            }
            return result;

        }

        [AuthorizeMaster]
        [HttpPost]
        public ContentResult PublisherJoin(int source, int target)
        {
            _db.ExecuteCommand("update BookDescriptionCatalog set PublisherID = " + target + " where PublisherID = " +
                               source);
            var sp = _db.BookPublishers.FirstOrDefault(x => x.ID == source);
            if (sp != null)
            {
                _db.BookPublishers.DeleteOnSubmit(sp);
            }
            _db.SubmitChanges();
            return new ContentResult() { Content = "1" };
        }

        public ContentResult SaveComments(string action, string list)
        {
            var ids = list.Split<int>();
            var dbList = _db.BookComments.Where(x => ids.Contains(x.ID));
            if (action == "approve")
            {
                foreach (var comment in dbList)
                {
                    comment.Approved = true;
                }
            }
            else
            {
                _db.BookComments.DeleteAllOnSubmit(dbList);
            }
            _db.SubmitChanges();
            return new ContentResult() { Content = "" };
        }
    }

}
