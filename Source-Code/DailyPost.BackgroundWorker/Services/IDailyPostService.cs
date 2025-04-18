using Microsoft.Extensions.DependencyInjection;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DailyPost.BackgroundWorker.Services
{
    public interface IDailyPostService
    {
      Task<string> TakeScreenShot(IWebDriver driver);
      Task<bool> SendEmail(EmailInfo emailInfo);
    }
}
