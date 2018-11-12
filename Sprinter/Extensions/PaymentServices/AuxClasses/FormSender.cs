using System;
using System.Collections.Specialized;
using System.Text;
using System.Web;

namespace Sprinter.Extensions.PaymentServices.AuxClasses
{
    public enum SendMethod
    {
        Get,
        Post
    }
    public class FormSender
    {
        private readonly NameValueCollection _parameters = new NameValueCollection();
        //private const string FORM_MARKUP = "<html><head></head>\t<body onload=\"document.{0}.submit()\">\t\t<form name=\"{0}\" method=\"{1}\" action=\"{2}\">\t\t\t{3}\t\t</form>\t</body></html>";
        //private const string PARAM_MARKUP = "<input name=\"{0}\" type=\"hidden\" value=\"{1}\">";

        public FormSender()
        {
            Url = string.Empty;
            Method = SendMethod.Post;
            FormName = "form1";
        }

        private string GetMethod()
        {
            switch (Method)
            {
                case SendMethod.Get:
                    return "GET";

                case SendMethod.Post:
                    return "POST";
            }
            throw new ApplicationException("Dunno such method!");
        }

        public void Send()
        {
            var context = HttpContext.Current;
            if (context == null)
            {
                throw new NullReferenceException("HttpContext needed");
            }
            var parametersMarkup = new StringBuilder();
            foreach (string key in _parameters.AllKeys)
            {
                parametersMarkup.AppendFormat("<input name=\"{0}\" type=\"hidden\" value=\"{1}\">", HttpUtility.HtmlEncode(key), HttpUtility.HtmlEncode(_parameters[key]));
            }
            var formMarkup = "<html><head></head>\t<body onload=\"document.{0}.submit()\">\t\t<form name=\"{0}\" method=\"{1}\" action=\"{2}\">\t\t\t{3}\t\t</form>\t</body></html>".FormatWith(new object[] { FormName, GetMethod(), Url, parametersMarkup });
            context.Response.Clear();
            context.Response.Write(formMarkup);
            //context.Response.End();
        }

        public string FormName { get; set; }

        public SendMethod Method { get; set; }

        public NameValueCollection Parameters
        {
            get
            {
                return _parameters;
            }
        }

        public string Url { get; set; }
    }
}

