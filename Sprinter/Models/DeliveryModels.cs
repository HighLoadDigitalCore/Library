using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sprinter.Extensions;

namespace Sprinter.Models
{
    [MetadataType(typeof(OrderDeliveryZoneIntervalDataAnnotations))]
    public partial class OrderDeliveryZoneInterval
    {
        public class OrderDeliveryZoneIntervalDataAnnotations
        {
            [DisplayName("Нижняя граница (кг или км)")]
            [Required]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal MinInterval { get; set; }


            [DisplayName("Верхняя граница (кг или км)")]
            [Required]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal MaxInterval { get; set; }


            [DisplayName("Стоимость")]
            [Required]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal Cost { get; set; }


            [DisplayName("Предел веса")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal WeightLimit { get; set; }


            [DisplayName("Цена за единицу при превышении веса")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal OverWeightCost { get; set; }

            [DisplayName("Единица рассчета при превышении веса (кг)")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal OverWeightUnit { get; set; }
        }
    }

    [MetadataType(typeof(OrderDeliveryZoneDataAnnotations))]
    public partial class OrderDeliveryZone
    {
        public class OrderDeliveryZoneDataAnnotations
        {
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Название тарифного плана")]
            public string Name { get; set; }


            [DisplayName("Тарифный план рассчитывается по весу товара")]
            public bool IsWeightZone { get; set; }

            [DisplayName("Ограничения по весу")]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal WeightThreshold { get; set; }

            [DisplayName("Тарифный план при превышении лимита")]
            public string AlternativeZone { get; set; }

        }

        public SelectList AlternativeZoneList
        {
            get
            {
                DB db = new DB();
                var list = db.OrderDeliveryZones.Where(x => x.ID != ID).Select(x => new { Name = x.Name, ID = x.ID }).ToList();
                list.Insert(0, new { Name = "Не определено", ID = 0 });
                return new SelectList(list, "ID", "Name", AlternativeZone ?? 0);
            }
        }
    }

    public partial class OrderDeliveryRegion
    {
        private decimal? _orderDeliveryCost;
        public decimal OrderDeliveryCost
        {
            get
            {
                if (_orderDeliveryCost == null || !_orderDeliveryCost.HasValue)
                {
                    var cart = ShopCart.InitCart();
                    if (cart.SelectedProvider.DiscountThreshold.IsFilled())
                    {
                        if (cart.TotalSum >= cart.SelectedProvider.DiscountThreshold)
                        {
                            _orderDeliveryCost = 0;
                            return _orderDeliveryCost.Value;
                        }
                    }

                    var weight = ShopCart.InitCart().SummaryBookWeight;

                    decimal cost = Price;
                    if (WeightZoneID != null && WeightZoneID > 0)
                    {
                        var zone = OrderDeliveryWeightZone;
                        if (weight > OrderDeliveryWeightZone.WeightThreshold)
                        {
                            if (OrderDeliveryWeightZone.AlternativeZone != null &&
                                OrderDeliveryWeightZone.AlternativeOrderDeliveryZone.WeightThreshold > weight)
                            {
                                zone = OrderDeliveryWeightZone.AlternativeOrderDeliveryZone;
                            }
                            else
                            {
                                zone = null;
                            }
                        }
                        if (zone == null)
                        {
                            _orderDeliveryCost = 0;
                            return _orderDeliveryCost.Value;
                        }
                        var interval =
                            OrderDeliveryWeightZone.OrderDeliveryZoneIntervals.FirstOrDefault(
                                x => x.MinInterval <= weight && x.MaxInterval > weight) ??
                            OrderDeliveryWeightZone.OrderDeliveryZoneIntervals.OrderByDescending(x => x.MaxInterval)
                                                   .
                                                    First();
                        cost += interval.Cost;
                        if (interval.WeightLimit.HasValue && interval.OverWeightCost.HasValue &&
                            interval.OverWeightUnit.HasValue && weight > interval.WeightLimit)
                        {
                            decimal additionalUnits = (weight - interval.WeightLimit.Value) /
                                                      interval.OverWeightUnit.Value;
                            cost += interval.OverWeightCost.Value * Math.Ceiling(additionalUnits);
                        }
                    }
                    if (DistanceZoneID != null && DistanceZoneID > 0)
                    {
                        var zone = OrderDeliveryDistanceZone;
                        if (weight > OrderDeliveryDistanceZone.WeightThreshold)
                        {
                            if (OrderDeliveryDistanceZone.AlternativeZone != null &&
                                OrderDeliveryDistanceZone.AlternativeOrderDeliveryZone.WeightThreshold > weight)
                            {
                                zone = OrderDeliveryDistanceZone.AlternativeOrderDeliveryZone;
                            }
                            else
                            {
                                zone = null;
                            }
                        }
                        if (zone == null)
                        {
                            _orderDeliveryCost = 0;
                            return _orderDeliveryCost.Value;
                        }
                        var dist =
                            zone.OrderDeliveryZoneIntervals.FirstOrDefault(
                                x => x.MinInterval <= RegionDistance && x.MaxInterval > RegionDistance) ??
                            zone.OrderDeliveryZoneIntervals.OrderByDescending(x => x.MaxInterval).
                                 First();

                        cost += dist.Cost;
                        if (dist.WeightLimit.HasValue && dist.OverWeightCost.HasValue &&
                            dist.OverWeightUnit.HasValue && weight > dist.WeightLimit)
                        {
                            decimal additionalUnits = (weight - dist.WeightLimit.Value) / dist.OverWeightUnit.Value;
                            cost += dist.OverWeightCost.Value * Math.Ceiling(additionalUnits);
                        }
                    }
                    _orderDeliveryCost = cost;

                }
                return _orderDeliveryCost.Value;
            }
        }



        public string Delivery
        {
            get
            {
                if (DeliveryTime < 0) return "";
                if (DeliveryTime == 0) return "Доставка в день заказа";
                if (DeliveryTime == 1) return "Доставка на следующий день";
                return "Доставка — от {0} {1}".FormatWith(DeliveryTime.ToString(), OrderDeliveryProvider.GetDayWordForm(DeliveryTime));

            }
        }
    }

    public partial class OrderDeliveryProvider
    {
        public decimal MinPrice
        {
            get
            {
                if (OrderDeliveryRegions.Any())
                {
                    decimal price = 0;
                    OrderDeliveryZoneInterval minInterval = null;
                    if (OrderDeliveryRegions.Any(x => x.DistanceZoneID != null))
                    {
                        minInterval =
                            OrderDeliveryRegions.First(x => x.DistanceZoneID != null).OrderDeliveryDistanceZone.
                                OrderDeliveryZoneIntervals.OrderBy(x => x.MinInterval).First();
                        price += minInterval.Cost;
                    }

                    if (OrderDeliveryRegions.Any(x => x.WeightZoneID != null))
                    {
                        minInterval =
                            OrderDeliveryRegions.First(x => x.WeightZoneID != null).OrderDeliveryWeightZone.
                                OrderDeliveryZoneIntervals.OrderBy(x => x.MinInterval).First();
                        price += minInterval.Cost;
                    }
                    price += OrderDeliveryRegions.AsEnumerable().Min(x => x.Price);
                    return price;
                }
                return 0;
            }
        }
        public bool HasPriceRange
        {
            get
            {
                if (OrderDeliveryRegions.Any(x => x.DistanceZoneID != null))
                    return true;

                if (OrderDeliveryRegions.Select(x => x.WeightZoneID).Distinct().Count() > 1)
                    return true;

                return OrderDeliveryRegions.Select(x => x.Price).Distinct().Count() > 1;
            }
        }
        public static string GetDayWordForm(int count)
        {
            if (count % 10 == 1 && (count < 10 || count > 20)) return "дня";
            return "дней";
        }

        public string DeliveryAverage
        {
            get
            {
                if (OrderDeliveryRegions.Any())
                {
                    var min = OrderDeliveryRegions.Min(x => x.DeliveryTime);
                    if (min < 0) return "";
                    if (min == 0) return "Доставка в день заказа";
                    if (min == 1) return "Доставка на следующий день";
                    return "Доставка — от {0} {1}".FormatWith(min.ToString(), GetDayWordForm(min));
                }
                return "";

            }
        }

        public SelectList RegionList
        {
            get
            {
                var list =
                    OrderDeliveryRegions.OrderBy(x => x.ImportID).Select(x => new { Key = x.ID, Value = x.Name }).ToList();
                if (list.Count > 1)
                {
                    list.Insert(0, new { Key = 0, Value = string.Format("--Выберите {0}--", SelectListText) });
                }
                else
                {
                    ShopCart.InitCart().SetField("DeliveryRegion", list.First().Key);
                }
                return new SelectList(list, "Key", "Value", ShopCart.InitCart().GetField<int>("DeliveryRegion"));
            }
        }

        public string SelectListText
        {
            get
            {
                if (ListType.IsNullOrEmpty())
                    return "";
                var arr = ListType.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (arr.Length < 2) return "";
                return arr[0];
            }
        }
        public string ListText
        {
            get
            {
                if (ListType.IsNullOrEmpty())
                    return "";
                var arr = ListType.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (arr.Length < 2) return "";
                return arr[1];
            }
        }

        public SelectList RegionListWithoutDefault
        {
            get
            {
                var list =
                    OrderDeliveryRegions.OrderBy(x => x.ImportID).Select(x => new { Key = x.ID, Value = x.Name }).ToList();
                return new SelectList(list, "Key", "Value", ShopCart.InitCart().GetField<int>("DeliveryRegion"));
            }
        }

        public SelectList TimeList
        {
            get
            {
                string selected = ShopCart.InitCart().GetField<string>("DeliveryTime");
                if (selected.IsNullOrEmpty())
                {
                    selected = "10.00 - 21.00 (пн-пт)";
                    ShopCart.InitCart().SetField("DeliveryTime", selected);
                }
                var list = new List<string> { "10.00 - 21.00 (пн-пт)", "10.00 - 16.00 (сб)" };
                return new SelectList(list, selected);
            }
        }
    }

    [MetadataType(typeof(OrderDeliveryRegionDataAnnotations))]
    public partial class OrderDeliveryRegion
    {
        public SelectList DistanceZoneList
        {
            get
            {
                var items =
                    new DB().OrderDeliveryZones.Where(x => !x.IsWeightZone).Select(x => new { Key = x.ID.ToString(), Value = x.Name })
                        .ToList();
                items.Insert(0, new { Key = "0", Value = "--Нет зоны--" });
                return new SelectList(items, "Key", "Value", DistanceZoneID);
            }
        }

        public SelectList WeightZoneList
        {
            get
            {
                var items =
                    new DB().OrderDeliveryZones.Where(x => x.IsWeightZone).Select(x => new { Key = x.ID.ToString(), Value = x.Name })
                        .ToList();
                items.Insert(0, new { Key = "0", Value = "--Нет зоны--" });
                return new SelectList(items, "Key", "Value", WeightZoneID);
            }
        }

        public class OrderDeliveryRegionDataAnnotations
        {

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Название региона")]
            public string Name { get; set; }

            [DisplayName("Цена доставки (базовая)")]
            [Required(ErrorMessage = "Поле '{0}' обязательно для заполнения", AllowEmptyStrings = false)]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal Price { get; set; }

            [DisplayName("Срок доставки, дни (-1 = не отображать, 0 = в день заказа)")]
            [Required(ErrorMessage = "Необходимо указать сроки доставки")]
            public int DeliveryTime { get; set; }


            [DisplayName("Расстояние (для тарифов по расстоянию)")]
            [Required(ErrorMessage = "Поле '{0}' обязательно для заполнения", AllowEmptyStrings = false)]
            [RegularExpression(@"\d+([\.,]{1}\d{1,2})?", ErrorMessage = "Поле '{0}' должно содержать число")]
            public decimal RegionDistance { get; set; }

            [DisplayName("Тарифный план по расстоянию")]
            public string DistanceZoneID { get; set; }

            [DisplayName("Тарифный план по весу")]
            public string WeightZoneID { get; set; }

            [DisplayName("Идентификатор в системе Sprinter")]
            public string ImportID { get; set; }
        }

    }

}