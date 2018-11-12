using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sprinter.Models
{
    [MetadataType(typeof(BookSpecOfferDataAnnotations))]
    public partial class BookSpecOffer
    {
        public class BookSpecOfferDataAnnotations
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Идентификатор товара в каталоге")]
            public string SaleCatalogID { get; set; }

            [DisplayName("Цена продажи спецпредложения")]
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal SpecPrice { get; set; }

            [DisplayName("Условие активации (сумма заказа)")]
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal MinPrice { get; set; }

        }
    }
}