using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sprinter.Models.ViewModels
{
    public class CatalogExportFilterModel
    {
        public string PageListPlain { get; set; }
        public string PartnerListPlain { get; set; }
        public IEnumerable<Partner> Partners { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool AvailableOnly { get; set; }
        public bool UseZip { get; set; }

        public CatalogExportFilterModel()
        {
            PageListPlain = PartnerListPlain = "";
            MinPrice = MaxPrice = null;
            Partners = new DB().Partners.OrderBy(x=> x.SalePriority).ToList();
            AvailableOnly = true;
            UseZip = true;
        }

    }
}