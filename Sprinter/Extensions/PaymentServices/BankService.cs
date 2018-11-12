using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;

namespace Sprinter.Extensions.PaymentServices
{
    public class BankService : IPaymentService
    {
        public string Name { get { return "Bank"; } }
        public Mode CurrentMode { get { return Mode.Default; } }
        public bool IsCompleted { get { return true; } }
        private Order _orderForPay;
        public void Init(Order orderForPay)
        {
            _orderForPay = orderForPay;
        }

        public void ProcessPayment(string currencyCode = "")
        {
        }

        public string AutoExecuteOnRequest()
        {
            if (_orderForPay != null)
            {

                var mailing =
                    MailingList.Get("PaymentOrder")
                               .To(ShopCart.InitCart().UserMail)
                               .WithReplacement(new MailReplacement("{ORDERNUMBER}", NotificatedOrder.ID.ToString("d10")));
                mailing.MemoryAttachments = new List<KeyValuePair<string, MemoryStream>>()
                    {
                        new KeyValuePair<string, MemoryStream>(
                            string.Format("Квитанция на оплату заказа №{0}.pdf", _orderForPay.ID.ToString("d10")),
                            new MemoryStream(_orderForPay.CreatePdfDoc(null)))
                    };

                string error = mailing.Send();
                return
                    string.Format(
                        "Наш менеджер скоро свяжется с Вами.<br />Спасибо за покупку!<br>Квитанция для оплаты заказа выслана на ваш электронный адрес.<br>Также вы можете скачать квитанцию по этой <a href='{0}'>ссылке</a><br>{1}",
                        _orderForPay.OrderPdfLink, error);

            }
            return "";


        }

        public bool IsMyNotification { get { return false; } }
        public Order NotificatedOrder { get { return (Order)HttpContext.Current.Session["LastOrder"]; } }
    }
}