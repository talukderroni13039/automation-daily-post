using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using DailyPost.BackgroundWorker.Services;
using Microsoft.Extensions.Configuration;
using System.Text;
using Serilog;
using Quartz;

namespace DailyPost.BackgroundWorker
{
    public class Worker : IJob
    {
        private readonly IDailyPostService _iDailyPostService;
        private readonly IConfiguration _iConfiguration;
        public Worker(IDailyPostService dailyPostService, IConfiguration configuration)
        {
           
            _iDailyPostService = dailyPostService;
            _iConfiguration = configuration;
        }
        public async Task Execute(IJobExecutionContext context)
        {

            try
            {
                Log.Information("Worker running at: {time}", DateTimeOffset.Now);
                string baseUrl = "https://techjays-ai-manager.replit.app";
                string dashboardUrl = baseUrl + "/app/";

                // Create a unique Chrome user data directory for this session
                string uniqueId = Guid.NewGuid().ToString();
                string tempDir = Path.GetTempPath();
                string userDataDir = Path.Combine(tempDir, $"chrome-data-{uniqueId}");

                // Ensure directory exists
                Directory.CreateDirectory(userDataDir);

                // Configure Chrome options for complete isolation
                ChromeOptions options = new ChromeOptions();
                options.AddArgument("--headless=new");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument($"--user-data-dir={userDataDir}");
                options.AddArgument("--disable-extensions");
                options.AddArgument("--disable-default-apps");
                options.AddArgument("--no-first-run");
                options.AddArgument("--no-default-browser-check");

                // Configure service
                var service = ChromeDriverService.CreateDefaultService();
                service.SuppressInitialDiagnosticInformation = true;
                service.HideCommandPromptWindow = true;

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    // Step 1: Navigate to base URL to set the cookie context
                    driver.Navigate().GoToUrl(baseUrl);
                    Thread.Sleep(2000);

                    // Step 2: Add cookies (these are your cookies)
                    string csrftoken = _iConfiguration.GetValue<string>("Credentials:csrftoken");
                    string sessionid = _iConfiguration.GetValue<string>("Credentials:sessionid");
                    var cookies = new List<Cookie>
                        {
                            new Cookie("csrftoken", csrftoken, ".techjays-ai-manager.replit.app", "/", null),
                            new Cookie("sessionid", sessionid, "techjays-ai-manager.replit.app", "/", null)
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

                    Log.Information("Current URL: " + currentUrl);
                    if (currentUrl.Contains("/app"))
                    {
                        Log.Information("Login with cookies succeeded and dashboard is loaded!");
                    }
                    else
                    {
                        Log.Error("Login failed with cookies!");
                        return;
                    }

                    // Option 1: By href attribute
                    var createReportLink = driver.FindElement(By.CssSelector("a[href='/app/reports/new/']"));
                    createReportLink.Click();
                    Log.Information("Create New Report' button clicked");

                    Thread.Sleep(3000); // wait for navigation
                    var textarea = driver.FindElement(By.CssSelector("textarea[placeholder='What did you work on today?']"));

                    // read the message from json file 
                    Message message = await _iDailyPostService.ReadMessageFromJsonFile();
                    var status = await GenerateStatusMessage(message);
                    textarea.SendKeys(status);   // Enter the message
                    Thread.Sleep(1000);     // let the JS bindings process the input

                    var submitButton = driver.FindElement(By.CssSelector("button[type='submit']"));
                    submitButton.Click();

                    Log.Information("submitButton clicked");
                    Thread.Sleep(30000);  // Wait 30 seconds

                    //======================================Report Submitted===================================//

                    var screenShotPath = await _iDailyPostService.TakeScreenShot(driver);

                    string emailTemplate = _iConfiguration.GetValue<string>("ReceiverEmail:EmailTemplate");
                    string subjectTemplate = _iConfiguration.GetValue<string>("ReceiverEmail:SubjectTemplate");
                    string recipientEmail = _iConfiguration.GetValue<string>("ReceiverEmail:Email");
                    string recipientName = _iConfiguration.GetValue<string>("ReceiverEmail:RecipientName");

                    string formattedDate = DateTime.Now.ToString("dd/MM/yyyy");  // 18/04/2025

                    string emailBody = string.Format(emailTemplate, recipientName, formattedDate);
                    string subject = string.Format(subjectTemplate, formattedDate);
                    EmailInfo emailInfo = new EmailInfo()
                    {
                        To = recipientEmail,
                        Subject = subject,
                        Body = emailBody,
                        Files = new List<string>() { screenShotPath },
                    };
                    await _iDailyPostService.SendEmail(emailInfo);
                    Log.Information("Email sent Sucessfully");
                    driver.Quit();
                }
                if (Directory.Exists(userDataDir))
                {
                    Directory.Delete(userDataDir, true);
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex,"Exception Occured "+ ex.Message);
            }
       
        }
        private void KillExistingChromeProcesses()
        {
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = "-c \"pkill -f chrome || true; pkill -f chromedriver || true\"";
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit(5000);
                Log.Information("Cleared existing Chrome processes");
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to kill Chrome processes: {ex.Message}");
            }
        }
        private async Task<string> GenerateStatusMessage(Message message)
        {
            // Split the comma-separated values
            string[] projects = message.ProjectName.Split(',', StringSplitOptions.TrimEntries);
            string[] tasks = message.TaskName.Split(',', StringSplitOptions.TrimEntries);
            string[] hours = message.Hour.Split(',', StringSplitOptions.TrimEntries);

            // Build a single sentence
            StringBuilder status = new StringBuilder();
            status.Append("I have worked ");

            // Assume the arrays should be of equal length, or at least have matching pairs
            int count = Math.Min(Math.Min(projects.Length, tasks.Length), hours.Length);

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    if (i == count - 1)
                        status.Append(" and ");
                    else
                        status.Append(", ");
                }

                status.AppendFormat("{0} hours on {1} project for the task - {2}", hours[i], projects[i], tasks[i]);
            }

            // Add the note at the end as part of the same sentence
            if (!string.IsNullOrEmpty(message.Note))
            {
                status.AppendFormat("; additional notes: {0}", message.Note);
            }

            status.Append(".");
            return status.ToString();

            //"I have worked 6 hours on Project X for the task API Integration and 2 hours on Project Y for the task Task 2; additional notes: Completed all required test cases."
        }
    }
}
