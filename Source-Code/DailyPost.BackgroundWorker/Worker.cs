using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support;
using System.Text.Json;
using DailyPost.BackgroundWorker.Services;
using Microsoft.Extensions.Configuration;

namespace DailyPost.BackgroundWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IDailyPostService _iDailyPostService;
        private readonly IConfiguration _iConfiguration;
        public Worker(ILogger<Worker> logger, IDailyPostService dailyPostService,IConfiguration configuration)
        {
            _logger = logger;
            _iDailyPostService = dailyPostService;
            _iConfiguration = configuration;    
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            string baseUrl = "https://techjays-ai-manager.replit.app";
            string dashboardUrl = baseUrl + "/app/";

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--start-maximized");

            using (IWebDriver driver = new ChromeDriver(options))
            {
                // Step 1: Navigate to base URL to set the cookie context
                driver.Navigate().GoToUrl(baseUrl);
                Thread.Sleep(2000);

                // Step 2: Add cookies (these are your cookies)
                var cookies = new List<Cookie>
                        {
                            new Cookie("csrftoken", "iXmOcF9BfkGWkUVdUfZCFESJom9dQBGU", ".techjays-ai-manager.replit.app", "/", null),
                            new Cookie("sessionid", "jld1fqf8pcg9x5svl82hqq2h1osdf0la", "techjays-ai-manager.replit.app", "/", null)
                        };

                foreach (var cookie in cookies)
                {
                    driver.Manage().Cookies.AddCookie(cookie);
                }

                Thread.Sleep(1000);

                // Step 3: Navigate to dashboard
                driver.Navigate().GoToUrl(dashboardUrl);
                Thread.Sleep(4000); // Wait for dashboard to load

                // Step 4: Check if login succeeded (you can enhance this by checking for specific elements)
                string currentUrl = driver.Url;
                string pageTitle = driver.Title;

                Console.WriteLine("Current URL: " + currentUrl);
                Console.WriteLine("Page Title: " + pageTitle);

                if (currentUrl.Contains("/app"))
                {
                    Console.WriteLine("✅ Login with cookies succeeded and dashboard is loaded!");
                }
                else
                {
                    Console.WriteLine("❌ Login failed or redirected elsewhere.");
                }

                // Option 1: By href attribute
                var createReportLink = driver.FindElement(By.CssSelector("a[href='/app/reports/new/']"));
                createReportLink.Click();
                Console.WriteLine("📝 'Create New Report' button clicked.");

                Thread.Sleep(3000); // wait for navigation

                // Optional: Check if you are on the new report page
                Console.WriteLine("Now on page: " + driver.Url);


                var textarea = driver.FindElement(By.CssSelector("textarea[placeholder='What did you work on today?']"));

                // Enter the message
                string message = "I have worked on orbcomm fe4- enricher and NTC project. I have worked 6 hour for fe4-enricher for Unit Test case implementation for Rename Event Mapping and Engine Hourtask and 2 hour for NTC Rooma Assignment -Implement CRM into CRA task.";

                textarea.SendKeys(message);
                Thread.Sleep(1000); // let the JS bindings process the input

                // Re-enable button wait if needed (for AlpineJS to detect input and reportId state)
                var submitButton = driver.FindElement(By.CssSelector("button[type='submit']"));
              //  submitButton.Click();

                Console.WriteLine("✅ Report submitted.");
                Thread.Sleep(4000); // Wait for dashboard to load
                                    //======================================Report Submitted===================================//

                var screenShotPath=  await _iDailyPostService.TakeScreenShot(driver);

                string emailTemplate = _iConfiguration.GetValue<string>("ReceiverEmail:EmailTemplate");
                string subjectTemplate = _iConfiguration.GetValue<string>("ReceiverEmail:SubjectTemplate");
                string recipientEmail = _iConfiguration.GetValue<string>("ReceiverEmail:Email");
                string recipientName = _iConfiguration.GetValue<string>("ReceiverEmail:RecipientName");

                string formattedDate = DateTime.Now.ToString("dd/MM/yyyy");  // 18/04/2025

                string emailBody = string.Format(emailTemplate, recipientName, formattedDate);
                string subject= string.Format(subjectTemplate, formattedDate);
                EmailInfo emailInfo = new EmailInfo()
                {
                    To = recipientEmail,
                    Subject = subject,
                    Body = emailBody,
                    Files = new List<string>() { screenShotPath },
                };
                await _iDailyPostService.SendEmail(emailInfo);

                driver.Quit();
            }
        }
    }
}
