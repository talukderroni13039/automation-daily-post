using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DailyPost.BackgroundWorker
{
    public class EmailInfo
    {
        public long id { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Body { get; set; }
        public string Subject { get; set; }
        public string UserToken { get; set; }
        public string EmailTemplate { get; set; }
        public string Locale { get; set; }
        public List<string> Files { get; set; }
        public int EmailCounter { get; set; }
        public short IsEmailSent { get; set; }
        public string UpdateDispatchDT { get; set; }
        public object RefObject { get; set; }
        public bool IsCanceled { get; set; }
        public bool IsError { get; set; }
        public string Error { get; set; }
    }
}
