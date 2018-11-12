using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sprinter.Models.ViewModels
{
    public class CatalogFilterEditModel
    {
        public CMSPage CatalogPage { get; private set; }
        public CatalogFilterEditModel(int? pageID)
        {
            DB db = new DB();
            CatalogPage = db.CMSPages.FirstOrDefault(x => x.ID == pageID);
        }
    }
}