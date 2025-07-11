﻿using DailyPost.BackgroundWorker.Services;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Quartz;
using Serilog;
using System.IO;
using System.Text;

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
            string userDataDir = null;
            IWebDriver driver = null;

            try
            {
                (driver, userDataDir) = SetupChromeDriver();
                await AuthenticateWithCookies(driver);    // Authenticate with cookies
                await NavigateToCreateReport(driver);   // Navigate to create report page
                await FillAndSubmitReport(driver);  // Fill and submit the report form
                await CaptureScreenshotAndSendEmail(driver); // Capture screenshot and send email

                Log.Information("Daily report process completed successfully");

            }
            catch (Exception ex)
            {
                Log.Error(ex,"Exception Occured "+ ex.Message);
            }
       
        }

        private (IWebDriver driver, string userDataDir) SetupChromeDriver()
        {
            Log.Information("Setting up Chrome driver");

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

            var driver = new ChromeDriver(options);
            Log.Information("Chrome driver setup completed");

            return (driver, userDataDir);
        }

        private async Task AuthenticateWithCookies(IWebDriver driver)
        {
            Log.Information("Starting authentication process");

            string baseUrl = "https://aimanager.techjays.com";

            // Step 1: Navigate to base URL to set the cookie context
            driver.Navigate().GoToUrl(baseUrl);
            await Task.Delay(2000);

            // Step 2: Add authentication cookies
            string csrftoken = _iConfiguration.GetValue<string>("Credentials:csrftoken");
            string sessionid = _iConfiguration.GetValue<string>("Credentials:sessionid");

            if (string.IsNullOrEmpty(csrftoken) || string.IsNullOrEmpty(sessionid))
            {
                throw new InvalidOperationException("Authentication cookies are missing from configuration");
            }

            var cookies = new List<Cookie>
        {
            new Cookie("csrftoken", csrftoken, "aimanager.techjays.com", "/", null),
            new Cookie("sessionid", sessionid, "aimanager.techjays.com", "/", null)
        };

            foreach (var cookie in cookies)
            {
                driver.Manage().Cookies.AddCookie(cookie);
            }

            await Task.Delay(1000);
            Log.Information("Authentication cookies added successfully");
        }

        private async Task NavigateToCreateReport(IWebDriver driver)
        {
            Log.Information("Navigating to dashboard and create report page");

            string baseUrl = "https://aimanager.techjays.com";
            string dashboardUrl = baseUrl + "/app/";

            // Navigate to dashboard
            driver.Navigate().GoToUrl(dashboardUrl);
            await Task.Delay(4000); // Wait for dashboard to load

            // Verify login succeeded
            string currentUrl = driver.Url;
            Log.Information("Current URL: {url}", currentUrl);

            if (!currentUrl.Contains("/app"))
            {
                throw new InvalidOperationException("Authentication failed - not redirected to dashboard");
            }

            Log.Information("Dashboard loaded successfully, navigating to create report");

            // Click on "Create New Report" link
            var createReportLink = driver.FindElement(By.CssSelector("a[href='/app/reports/new/']"));
            createReportLink.Click();

            Log.Information("'Create New Report' button clicked");
            await Task.Delay(3000); // Wait for navigation
        }

        private async Task FillAndSubmitReport(IWebDriver driver)
        {
            Log.Information("Filling and submitting report form");

            try
            {
                // Find the textarea for the report message
                var textarea = driver.FindElement(By.CssSelector("textarea[x-model='newMessage']"));

                // Generate the status message
                Message message = await _iDailyPostService.ReadMessageFromJsonFile();
                var status = await GenerateStatusMessage(message);

                // Fill the form
                textarea.SendKeys(status);
                await Task.Delay(1000); // Let the JS bindings process the input

                Log.Information("Report message entered successfully");

                // Submit the form
                var submitButton = driver.FindElement(By.CssSelector("button[type='submit']"));
                submitButton.Click();

                Log.Information("Report form submitted successfully");
                await Task.Delay(30000); // Wait for submission to complete
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while filling and submitting report");
                throw;
            }
        }

        private async Task CaptureScreenshotAndSendEmail(IWebDriver driver)
        {
            Log.Information("Capturing screenshot and preparing email");

            try
            {
                // Capture screenshot
                var screenShotPath = await _iDailyPostService.TakeScreenShot(driver);
                Log.Information("Screenshot captured at: {path}", screenShotPath);

                // Prepare email details
                string emailTemplate = _iConfiguration.GetValue<string>("ReceiverEmail:EmailTemplate");
                string subjectTemplate = _iConfiguration.GetValue<string>("ReceiverEmail:SubjectTemplate");
                string recipientEmail = _iConfiguration.GetValue<string>("ReceiverEmail:Email");
                string recipientName = _iConfiguration.GetValue<string>("ReceiverEmail:RecipientName");

                if (string.IsNullOrEmpty(recipientEmail))
                {
                    throw new InvalidOperationException("Recipient email is not configured");
                }

                string formattedDate = DateTime.Now.ToString("dd/MM/yyyy");
                string emailBody = string.Format(emailTemplate, recipientName, formattedDate);
                string subject = string.Format(subjectTemplate, formattedDate);

                // Send email with screenshot
                EmailInfo emailInfo = new EmailInfo()
                {
                    To = recipientEmail,
                    Subject = subject,
                    Body = emailBody,
                    Files = new List<string>() { screenShotPath }
                };

                await _iDailyPostService.SendEmail(emailInfo);
                Log.Information("Email sent successfully to: {email}", recipientEmail);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while capturing screenshot or sending email");
                throw;
            }
        }

        private void CleanupResources(IWebDriver driver, string userDataDir)
        {
            Log.Information("Starting cleanup process");

            try
            {
                // Close browser
                driver?.Quit();
                Log.Information("Chrome driver closed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while closing Chrome driver");
            }

            try
            {
                // Clean up temporary directory
                if (!string.IsNullOrEmpty(userDataDir) && Directory.Exists(userDataDir))
                {
                    Directory.Delete(userDataDir, true);
                    Log.Information("Temporary directory cleaned up: {dir}", userDataDir);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while cleaning up temporary directory: {dir}", userDataDir);
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
