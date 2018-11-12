using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Sprinter.Extensions;

namespace Sprinter.Models.ViewModels
{
    public interface IAdditionalFilter
    {
        IQueryable<BookSaleCatalog> Filter(IQueryable<BookSaleCatalog> primarySource);
        RouteValueDictionary Routes { get; }
    }

    public class MasterSearchViewModel
    {
        public MasterSearchData SearchData { get; set; }
        public IQueryable<BookTag> SearchedTagList
        {
            get { return FinalList.SelectMany(x => x.BookDescriptionCatalog.BookTagRels).Select(x => x.BookTag).Distinct().OrderBy(x=> x.Tag); }
        }
        public bool HasCheckColumn { get; set; }
        public int? Page { get; set; }
        public IAdditionalFilter AdditionalFilterModel { get; set; }
        public RouteValueDictionary CommonRoutes
        {
            get
            {
                var list = new RouteValueDictionary(SearchData.CreateRoutes());
                if (AdditionalFilterModel != null)
                {
                    var subList = AdditionalFilterModel.Routes;
                    foreach (var pair in subList)
                    {
                        list.Add(pair.Key, pair.Value);
                    }
                }
                list.Add("page", Page ?? 0);
                return list;
            }
        }

        private IQueryable<BookSaleCatalog> _finalList;
        public IQueryable<BookSaleCatalog> FinalList
        {
            get
            {
                if (_finalList == null)
                {
                    var primaryDS = SearchData.Search();
                    if (AdditionalFilterModel != null)
                    {
                        primaryDS = AdditionalFilterModel.Filter(primaryDS);
                    }
                    _finalList = primaryDS;
                }
                return _finalList;
            }
        }

        private PagedData<BookSaleCatalog> _pagedCatalog;
        public PagedData<BookSaleCatalog> PagedCatalog
        {
            get
            {
                if (_pagedCatalog == null)
                    _pagedCatalog = new PagedData<BookSaleCatalog>(FinalList, Page ?? 0, SearchData.ResultCount,
                                                                   CommonRoutes, SearchData.SearchedCount);
                return _pagedCatalog;
            }
        }
    }
}