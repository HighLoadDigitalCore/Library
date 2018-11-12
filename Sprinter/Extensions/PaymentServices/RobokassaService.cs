using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Web;
using System.Globalization;
using Sprinter.Extensions.PaymentServices.AuxClasses;
using Sprinter.Models;

namespace Sprinter.Extensions.PaymentServices
{
    public sealed class RobokassaService : IPaymentService
    {
        private string _merchantLogin;
        private string _password1;
        private string _password2;
        private string _paymentUrl;
        private HttpRequest _request;
        private HttpResponse _response;
        private Order _orderForPay;

        public RobokassaService()
        {
            _request = HttpContext.Current.Request;
            _response = HttpContext.Current.Response;
        }

        private RobokassaService(string merchantLogin, string password1, string password2, string paymentUrl)
        {
            _merchantLogin = merchantLogin;
            _password1 = password1;
            _password2 = password2;
            _paymentUrl = paymentUrl;
            _request = HttpContext.Current.Request;
            _response = HttpContext.Current.Response;
        }

        /// <summary>
        /// Обрабатывает оповещение об оплате от Робокассы (Result).
        ///	При успешном результате проверки, состояние заказа изменяется на "Оплачено".
        /// </summary>
        /// <returns>
        /// Результат проверки оповещения: экземпляр заказа -- если оплата произведена, иначе -- null.
        /// </returns>
        public bool OnResult()
        {
            /*
             * OutSum
             *	-полученная сумма. Сумма будет передана в той валюте, которая 
             *	была указана при регистрации магазина. Формат представления 
             *	числа - разделитель точка.
             */
            string outSum = _request["OutSum"];

            /*
             * InvId - номер счета в магазине
             */
            string invId = _request["InvId"];

            /*
             * SignatureValue
             *	- контрольная сумма MD5 - строка представляющая собой 32-разрядное 
             *	число в 16-ричной форме и любом регистре (всего 32 символа 0-9, A-F). 
             *	Формируется по строке, содержащей некоторые параметры, разделенные ':', 
             *	с добавлением sMerchantPass2
             */
            string signatureValue = _request["SignatureValue"];

            string actualSignature = GetPaymentSignature(new[] { outSum, invId, _password2 }, null);

            _response.Clear();

            // Если подписи совпадают...
            if (string.Equals(actualSignature, signatureValue, StringComparison.InvariantCultureIgnoreCase))
            {
                var db = new DB();

                int orderId = Convert.ToInt32(invId);
                var order = db.Orders.FirstOrDefault(x => x.ID == orderId);
                if (order == null)
                {
                    _response.Write("FAIL{0}".FormatWith(orderId));
                    _response.Write("No such order.");
                    _response.End();

                    return false;
                }

                decimal payedTotal = Convert.ToDecimal(outSum, new CultureInfo("en-US"));
                // и совпадают суммы...
                if (order.FinalSumWithDelivery == payedTotal)
                {
                    // то отмечаем заказ как оплаченный...
                    order.StatusID = OrderStatus.GetStatusID("Paid");
                    db.SubmitChanges();

                    // и возвращаем робокассе ответ "OK<номер заказа>"
                    _response.Write("OK{0}".FormatWith(order.ID));
                    _response.End();

                    return true;
                }
                _response.Write("FAIL{0}".FormatWith(order.ID));
                _response.Write("Wrong total amount.");
                _response.End();
            }
            return false;
        }

        public void ProcessPayment()
        {
            ProcessPayment("");
        }

        /// <summary>
        /// Выполняет отправку информации о платеже в Робокассу. Первый шаг.
        /// </summary>
        /// <param name="currencyCode"> </param>
        public void ProcessPayment(string currencyCode)
        {
            var form =
                new FormSender
                {
                    FormName = "paymentForm",
                    Method = SendMethod.Post,
                    Url = _paymentUrl
                };

            /*
             * MrchLogin - login магазина в обменном пункте(обязательный параметр)
             */
            form.Parameters.Add("MrchLogin", _merchantLogin);

            /*
             * nOutSum - требуемая к получению сумма. Сумма должна быть указана в той валюте, 
             * которая была указана при регистрации магазина. Если параметр не указан 
             * (пустая строка), то пользователю предоставляется возможность ввести 
             * сумму самостоятельно. Формат представления числа - разделитель точка.
             */
            string outSum = _orderForPay.FinalSumWithDelivery.ToString("F", new CultureInfo("en-US"));
            form.Parameters.Add("OutSum", outSum);

            /* Desc - описание покупки, можно использовать только символы английского или 
             * русского алфавита, цифры и знаки препинания. Максимальная длина 100 символов.
             */
            string description = _orderForPay.BriefDescription.Substring(0, Math.Min(_orderForPay.BriefDescription.Length, 100));
            form.Parameters.Add("Desc", description);

            /*
             * nInvId - номер счета в магазине (должен быть уникальным для магазина). 
             * Может принимать значения от 1 до 2147483647 (2^31-1). 
             */
            string invId = _orderForPay.ID.ToString(CultureInfo.InvariantCulture);
            form.Parameters.Add("InvId", invId);

            if (!currencyCode.IsNullOrEmpty())
            {
                form.Parameters.Add("IncCurrLabel", currencyCode);
            }

            if (!_orderForPay.User.MembershipData.Email.IsNullOrEmpty())
            {
                form.Parameters.Add("Email", _orderForPay.User.MembershipData.Email);
            }

            /*
             * sSignatureValue - контрольная сумма MD5(обязательный параметр) - строка 
             * представляющая собой 32-разрядное число в 16-ричной форме и любом регистре 
             * (всего 32 символа 0-9, A-F). Формируется по строке, содержащей некоторые 
             * параметры, разделенные ':', с добавлением sMerchantPass1 - (устанавливается 
             * через интерфейс администрирования) т.е. 
             * sMerchantLogin:nOutSum:nInvId:sMerchantPass1[:пользовательские параметры, в отсортированном порядке]
             * К примеру если переданы параметры shpb=xxx и shpa=yyy то подпись формируется 
             * из строки ...:sMerchantPass1:shpa=yyy:shpb=xxx
             */
            string signature = GetPaymentSignature(new[] { _merchantLogin, outSum, invId, _password1 }, null);
            form.Parameters.Add("SignatureValue", signature);

            form.Send();
        }

        /// <summary>
        /// Выполняет проверку полученных от Робокассы даных об успешном платеже.
        ///	При успешной проверке и если заказ ещё не отмечен как оплаченный,
        ///	состояние заказа изменяется на "Оплачено".
        /// </summary>
        /// <returns>
        ///	При успешной проверке возвращает экземпляр заказа, иначе -- null.
        /// </returns>
        public bool OnSuccess()
        {
            /* OutSum
             *	-полученная сумма. Сумма будет передана в той валюте, 
             *	которая была указана при регистрации магазина. Формат 
             *	представления числа - Разделитель точка.
             */
            string outSum = _request["OutSum"];

            /* InvId 
             *	- номер счета в магазине
             */
            string invId = _request["InvId"];

            /* SignatureValue
             *	- контрольная сумма MD5 - строка представляющая собой 
             *	32-разрядное число в 16-ричной форме и любом регистре 
             *	(всего 32 символа 0-9, A-F). Формируется по строке, 
             *	содержащей некоторые параметры, разделенные ':', с 
             *	добавлением sMerchantPass1 (указывается при регистрации) 
             *	т.е. nOutSum:nInvId:sMerchantPass1[:пользовательские 
             *	параметры, в отсортированном порядке].
             *	К примеру если при инициализации операции были переданы 
             *	пользовательские параметры shpb=xxx и shpa=yyy то подпись 
             *	формируется из строки ...:sMerchantPass1:shpa=yyy:shpb=xxx
             */
            string signatureValue = _request["SignatureValue"];

            string actualSignature = GetPaymentSignature(new[] { outSum, invId, _password1 }, null);

            // Если подписи совпадают...
            if (string.Equals(actualSignature, signatureValue, StringComparison.InvariantCultureIgnoreCase))
            {
                var db = new DB();
                var order = db.Orders.FirstOrDefault(x => x.ID == Convert.ToInt32(invId));

                // если заказа с таким номером не существует
                if (order == null)
                {
                    return false;
                }

                decimal payedTotal = Convert.ToDecimal(outSum, new CultureInfo("en-US"));

                // если не совпадают суммы
                if (order.FinalSumWithDelivery != payedTotal)
                {
                    return false;
                }

                // всё ОК
                // Если обработчик OnResult почему-то не отработал и заказ ещё не отмечен как оплаченный
                if (order.StatusID != OrderStatus.GetStatusID("Paid"))
                {
                    // то делаем это
                    order.StatusID = OrderStatus.GetStatusID("Paid");
                    db.SubmitChanges();
                }
                return true;
            }
            return false;
        }

        public Mode CurrentMode
        {
            get
            {
                var modeValue = _request.QueryString["robo"];
                if (!string.IsNullOrEmpty(modeValue))
                {
                    switch (modeValue.ToLower())
                    {
                        case "check":
                            return Mode.Result;

                        case "success":
                            return Mode.Success;

                        case "fail":
                            return Mode.Fail;
                    }
                }
                return Mode.Default;
            }
        }

        public bool IsCompleted { get { return CurrentMode == Mode.Success; } }

        public string Name { get { return "Robo"; } }
        public void Init(Order orderForPay)
        {

            _merchantLogin = SiteSetting.Get<string>("RobokassaLogin");
            _password1 = SiteSetting.Get<string>("RobokassaPass1");
            _password2 = SiteSetting.Get<string>("RobokassaPass2");
            _paymentUrl = SiteSetting.Get<string>("RobokassaURL");
            _orderForPay = orderForPay;
        }


        /// <summary>
        /// Болванка метода обрабатывающего сообщение об отказе от оплаты.
        /// Пока ничего не делает.
        /// </summary>
        public void OnFail()
        {
        }

        public string AutoExecuteOnRequest()
        {
            string succesText =
                "Оплата заказа <b>{0}</b> произведена успешно.".FormatWith(_orderForPay.ID.ToString("d10"));
            string errorText =
                "Произошла ошибка при оплате заказа <b>{0}</b>. Пожалуйста, обратитесь в техподдержку.".FormatWith(
                    _orderForPay.ID.ToString("d10"));
            switch (CurrentMode)
            {
                case Mode.Result:
                    return OnResult() ? succesText : errorText;
                case Mode.Success:
                    return OnSuccess() ? succesText : errorText;
                case Mode.Fail:
                    return errorText;
                case Mode.Default:
                    ProcessPayment();
                    return "Производится перенаправление в систему оплаты.";
            }
            return "";
        }

        public bool IsMyNotification { get { return CurrentMode != Mode.Default; } }
        public Order NotificatedOrder
        {
            get
            {
                if (_orderForPay == null)
                {
                    switch (CurrentMode)
                    {
                        case Mode.Success:
                        case Mode.Result:
                            {
                                var db = new DB();
                                _orderForPay = db.Orders.FirstOrDefault(x => x.ID == int.Parse(_request["InvId"]));
                            }
                            break;

                    }
                }
                return _orderForPay;
            }
        }

        /// <summary>
        /// Вычисление контрольной суммы сообщения Робокассы
        /// </summary>
        /// <param name="requiredParams">Обязательные параметры</param>
        /// <param name="customParams">Пользовательские параметры</param>
        /// <returns>Контрольная сумма в MD5 в нижнем регистре.</returns>
        private static string GetPaymentSignature(string[] requiredParams, Dictionary<string, string> customParams)
        {
            Check.Argument.IsNotNull(requiredParams, "requiredParams");
            Check.Argument.IsNotEmpty(requiredParams, "requiredParams");

            string signSource = string.Join(":", requiredParams);

            if (customParams != null)
            {
                foreach (var param in customParams)
                {
                    signSource += ":{0}={1}".FormatWith(param.Key, param.Value);
                }
            }

            var md5 = new MD5CryptoServiceProvider();
            var buf = md5.ComputeHash(Encoding.ASCII.GetBytes(signSource));
            var result = buf.Select(b => "{0:x2}".FormatWith(b)).Aggregate((acc, el) => acc + el);
            return result;
        }
    }
}
