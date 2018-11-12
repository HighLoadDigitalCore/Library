using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;

namespace Sprinter.Models
{

    public class ThreadStartData
    {
        public HttpContext Context { get; set; }
        public int ProviderID { get; set; }
        public string URL { get; set; }
        public bool IsBook { get; set; }
    }

    [Serializable]
    public class ParseringInfo
    {
        public static ParseringInfo Create(string key)
        {
            if (HttpContext.Current.Application[key] == null)
                HttpContext.Current.Application[key] = new ParseringInfo();
            return HttpContext.Current.Application[key] as ParseringInfo;

        }
        public static ParseringInfo Reset(string key)
        {
            HttpContext.Current.Application[key] = null;
            return Create(key);
        }
        protected ParseringInfo()
        {
            Messages = new List<KeyValuePair<string, bool>>();
        }

        protected List<KeyValuePair<string, bool>> Messages { get; set; }
        public string MessageList
        {
            get { return string.Join("<br>", Messages.Select(x => x.Key).ToArray()); }
        }

        public string ErrorList
        {
            get { return string.Join("<br>", Messages.Where(x => x.Value).Select(x => x.Key).ToArray()); }
        }

        public void AddMessage(string message, bool error = false)
        {
            if (Messages.Count >= 100)
                Messages.RemoveRange(0, 10);
            Messages.Add(new KeyValuePair<string, bool>(message, error));
        }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        [ScriptIgnore]
        public BookDescriptionProvider Provider { get; set; }

        [ScriptIgnore]
        public string ParseURL { get; set; }

        public int Deleted { get; set; }
        public int Dirs { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Errors { get; set; }
        public int Prepared { get; set; }
        public int Total { get { return Created + Updated + Errors; } }

        public bool Break { get; set; }

        
        protected List<string> pagesProcessed = new List<string>();
        protected List<string> booksProcessed = new List<string>();

        public List<string> getProcessedList(bool isBook)
        {
            if (isBook) return booksProcessed;
            return pagesProcessed;
        }

        public void AddProcessedItem(string url, bool isBook)
        {
            if (isBook)
                lock (booksProcessed)
                {
                    booksProcessed.Add(url);
                }

            else
                lock (pagesProcessed)
                {
                    pagesProcessed.Add(url);
                }
        }
        public bool IsItemProcessed(string url, bool isBook)
        {

            if (isBook)
                lock (booksProcessed)
                {
                    return booksProcessed.Contains(url);
                }
            else
                lock (pagesProcessed)
                {
                    return pagesProcessed.Contains(url);
                }
        }
    }
}