using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sprinter.Models.ViewModels
{
    public class MarginEditorModel
    {
        public int Type { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
        [DisplayName("Наценка")]
        [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
        public decimal? Margin { get; set; }
        public string IDs { get; set; }

        public SelectList TypeList { get; private set; }

        public IEnumerable<BookTag> Tags { get; private set; } 

        public MarginEditorModel(int? Type)
        {
            this.Type = Type ?? 1;

            if (!Margin.HasValue)
                Margin = 0;

            var list = new List<KeyValuePair<int, string>>();
            list.Add(new KeyValuePair<int, string>(1, "По разделам"));
            list.Add(new KeyValuePair<int, string>(2, "По тегам"));
            TypeList = new SelectList(list, "Key", "Value", Type);

            if(this.Type == 2)
            {
                DB db = new DB();
                Tags = db.BookTags.OrderBy(x => x.Tag);
            }
        }
    }
}