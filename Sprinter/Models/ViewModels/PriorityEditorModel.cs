using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sprinter.Models.ViewModels
{
    public class PriorityEditorModel
    {
        public int? PageID { get; private set; }
        public List<PartnerPriority> PriorityList { get; private set; }
        public CMSPage Page { get; private set; }
        public PriorityEditorModel(int? pageID)
        {
            PageID = pageID;
            if (PageID.HasValue)
            {
                DB db = new DB();
                Page = db.CMSPages.FirstOrDefault(x => x.ID == PageID);
                var partners = db.Partners.AsEnumerable();
                PriorityList = new List<PartnerPriority>();
                foreach (var partner in partners)
                {
                    var p = partner.PartnerPriorities.FirstOrDefault(x => x.PageID == PageID);
                    if (p == null)
                    {
                        p = new PartnerPriority() { Priority = 0, PageID = PageID.Value, PartnerID = partner.ID, Partner = partner};
                    }
                    PriorityList.Add(p);
                }
                PriorityList = PriorityList.OrderBy(x => x.Priority == 0 ? 10000 : x.Priority).ToList();
            }
        }
    }


}