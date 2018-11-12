using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Sprinter.Extensions.Helpers;
using Sprinter.Models;

namespace Sprinter.Extensions.PaymentServices
{
    public class YandexNotification
    {
        /// <summary>
        /// Тип уведомления. Фиксированное значение p2p-incoming.
        /// </summary>
        public string notification_type { get; set; }

        /// <summary>
        /// Идентификатор операции в истории счета получателя.
        /// </summary>
        public string operation_id { get; set; }

        /// <summary>
        /// Сумма операции.
        /// </summary>
        public decimal amount { get; set; }

        /// <summary>
        /// Код валюты счета пользователя. Всегда 643 (рубль РФ согласно ISO 4217)
        /// </summary>
        public string currency { get; set; }

        /// <summary>
        /// Дата и время совершения перевода.
        /// </summary>
        public DateTime datetime { get; set; }

        /// <summary>
        /// Номер счета отправителя перевода.
        /// </summary>
        public string sender { get; set; }

        /// <summary>
        /// Перевод защищен кодом протекции.
        /// </summary>
        public bool codepro { get; set; }

        /// <summary>
        /// Метка платежа. Если метки у платежа нет, параметр содержит пустую строку.
        /// </summary>
        public string label { get; set; }

        /// <summary>
        /// SHA-1 hash параметров уведомления.
        /// </summary>
        public string sha1_hash { get; set; }

        /// <summary>
        /// Флаг означает, что уведомление тестовое. По умолчанию параметр отсутствует.
        /// </summary>
        public bool? test_notification { get; set; }
    }
    #region Хуита какая-то

    /*
    public class YandexRequester
    {

        public static string ClientId = SiteSetting.Get<string>("Yandex.ClientID");
        public static string SecretId = SiteSetting.Get<string>("Yandex.ClientID");
        public static string RedirectUri = AccessHelper.SiteUrl + "/order?step=5&yandex=1&type=";

        public static string GetTokenRequestURL(string scope, string request)
        {
            return "https://sp-money.yandex.ru/oauth/authorize?client_id=" + ClientId +
                                                     "&redirect_uri=" + HttpUtility.UrlEncode(RedirectUri + request, Encoding.UTF8) + "&response_type=code&scope=" + scope;

        }
        private string _accessToken;
        public string AccessToken
        {
            get
            {
                return _accessToken;
            }
            set
            {
                _accessToken = value;
            }
        }

        public Dictionary<string, string> TempTokenList
        {
            get
            {
                if (HttpContext.Current.Application["TempTokenList"] == null || !(HttpContext.Current.Application["TempTokenList"] is Dictionary<string, string>))
                    HttpContext.Current.Application["TempTokenList"] = new Dictionary<string, string>();
                return HttpContext.Current.Application["TempTokenList"] as Dictionary<string, string>;
            }
            set { HttpContext.Current.Application["TempTokenList"] = value; }
        }

        public string GetYandexTemporaryToken
        {
            get
            {
                if(TempTokenList.Contains())
            }
            set
            {
                
            }
        }

        public string GetAccessTokenFromTemporaryToken(string temporaryToken, string request)
        {

            var reqPost = System.Net.WebRequest.Create("https://sp-money.yandex.ru/oauth/token");
            reqPost.Method = "POST"; // Устанавливаем метод передачи данных в POST
            reqPost.Timeout = 120000; // Устанавливаем таймаут соединения
            reqPost.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";

            System.Net.ServicePointManager.Expect100Continue = false;
            // Формируем параметры запроса
            string data = "code=" + temporaryToken + "&client_id=" + ClientId +
                          (SecretId.IsFilled()
                               ? ("&client_secret=" +
                                  SecretId)
                               : "") + "&grant_type=authorization_code&redirect_uri="
                          + HttpUtility.UrlEncode(RedirectUri + request, Encoding.UTF8);

            byte[] sentData = Encoding.UTF8.GetBytes(data);
            reqPost.ContentLength = sentData.Length;
            var sendStream = reqPost.GetRequestStream();

            // Выполняем запрос
            sendStream.Write(sentData, 0, sentData.Length);
            sendStream.Close();

            // Получаем результат
            System.Net.WebResponse result = reqPost.GetResponse();

            string resultString = "";

            using (var reader = new StreamReader(result.GetResponseStream()))
            {
                resultString = reader.ReadToEnd();
            }

            // Пытаемся разобрать
            JObject o = JObject.Parse(resultString);
            // и сохранить токен
            _accessToken = (string)o.SelectToken("access_token");

            // Если есть ошибка - возвращаем ее
            return (string)o.SelectToken("error");

        }
        public string GetAccountInfo()
        {
            string resultText = "";

            try
            {
                var tokenResult = GetAccessTokenFromTemporaryToken(code);

                System.Net.WebRequest request = System.Net.WebRequest.Create("https://money.yandex.ru/api/account-info");
                request.Method = "POST"; // Устанавливаем метод передачи данных в POST
                request.Timeout = 120000; // Устанавливаем таймаут соединения
                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                request.Headers.Add("Authorization", "Bearer " + _accessToken);
                request.ContentLength = 0;
                System.Net.WebResponse result = request.GetResponse();


                using (StreamReader reader = new StreamReader(result.GetResponseStream()))
                {
                    resultText = reader.ReadToEnd();
                }
            }
            catch (System.Net.WebException ex)
            {
                resultText = "Ошибка: " + ex.Message;

            }
            return resultText;
        }
    }
*/

    #endregion
    public class YandexService : IPaymentService
    {
        private const string PAYMENT_FORM =
            "<iframe allowtransparency=\"true\" src=\"https://money.yandex.ru/embed/shop.xml?uid={0}&amp;writer=seller&amp;targets=%d0%a1%d0%bf%d1%80%d0%b8%d0%bd%d1%82%d0%b5%d1%80.%d1%80%d1%83+-+%d0%9e%d0%bf%d0%bb%d0%b0%d1%82%d0%b0+%d0%b7%d0%b0%d0%ba%d0%b0%d0%b7%d0%b0+%e2%84%96{1}&amp;default-sum={2}&amp;button-text=01&amp;hint=&amp;fio=on&amp;mail=on\" frameborder=\"0\" height=\"180\" scrolling=\"no\" width=\"450\"></iframe>";

        private HttpRequest _request;
        private HttpResponse _responce;
        private Order _paymentOrder;
        public YandexService()
        {
            _request = HttpContext.Current.Request;
            _responce = HttpContext.Current.Response;
        }
        public string Name { get { return "Yandex"; } }
        public Mode CurrentMode
        {
            get
            {
                var modeValue = _request.QueryString["yandex"];
                if (!string.IsNullOrEmpty(modeValue))
                {
                    switch (modeValue.ToLower())
                    {
                        case "1":
                            return Mode.Result;
                    }
                }
                return Mode.Default;
            }
        }
        public bool IsCompleted { get { return CurrentMode == Mode.Result; } }
        public void Init(Order orderForPay)
        {
            _paymentOrder = orderForPay;
        }

        public void ProcessPayment(string currencyCode = "")
        {
        }

        private YandexNotification _notification;
        public YandexNotification Notification
        {
            get
            {
                if (_notification == null)
                {
                    var notification = new YandexNotification();
                    var paramKeys = notification.GetType().GetFields().Select(x => x.Name);
                    var paramList =
                        paramKeys.Select(x => new { Key = x, Value = _request.Params[x] }).Where(x => x.Value.IsFilled()).
                            ToList();
                    foreach (var param in paramList)
                    {
                        notification.SetPropertyValueByString(param.Key, param.Value);
                    }
                    _notification = notification;
                }
                return _notification;
            }
        }

        public bool OnResult()
        {
            var paramKeys = Notification.GetType().GetFields().Select(x => x.Name);
            var paramList =
                paramKeys.Select(x => new { Key = x, Value = _request.Params[x] }).Where(x => x.Value.IsFilled()).
                    ToList();

            string joined = string.Join("&", paramList.Where(x => x.Key != "sha1_hash").Select(x => x.Value));
            joined += "&" + SiteSetting.Get<string>("Yandex.Secret");
            byte[] buffer = Encoding.UTF8.GetBytes(joined);
            var cryptoTransformSha1 = new SHA1CryptoServiceProvider();
            var hash = BitConverter.ToString(cryptoTransformSha1.ComputeHash(buffer)).Replace("-", "");
            if (hash != Notification.sha1_hash)
            {
                _responce.StatusCode = 500;
                return false;
            }

            return true;
        }

        public string AutoExecuteOnRequest()
        {
            switch (CurrentMode)
            {
                case Mode.Default:
                    return PAYMENT_FORM.FormatWith(SiteSetting.Get<string>("Yandex.Account"),
                                                   _paymentOrder.ID.ToString("d10"),
                        _paymentOrder.FinalSumWithDelivery/*(0.1)*/.ToString("f2").Replace(",", "."));
                case Mode.Result:
                    var paramKeys = Notification.GetType().GetFields().Select(x => x.Name);
                    var paramList =
                        paramKeys.Select(x => new { Key = x, Value = _request.Params[x] }).Where(x => x.Value.IsFilled()).
                            ToList();

                    string joined = string.Join("&", paramList.Select(x => x.Value));
                    //Logger.WriteToLog(joined);

                    return OnResult()
                               ? "Ошибка при совершении платежа. Обратитесь в техподдержку."
                               : "Ваш платеж успешно завершен";

            }
            return "";
        }

        public bool IsMyNotification { get { return CurrentMode != Mode.Default; } }
        public Order NotificatedOrder
        {
            get { return null; }
        }
    }
}