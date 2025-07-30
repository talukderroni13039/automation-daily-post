using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Serilog;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
namespace DailyPost.BackgroundWorker.Services
{
    public class DailyPostService : IDailyPostService
    {
        public IConfiguration _configuration;
        public DailyPostService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<Message> ReadMessageFromJsonFile()
        {
            try
            {
                var projectRoot = Path.Combine(Directory.GetCurrentDirectory(), "Message");
                string filePath = Path.Combine(projectRoot, "message.json");
                string jsonString = await File.ReadAllTextAsync(filePath);
                // Deserialize the JSON to your object
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // This allows for case-insensitive property matching
                };

                Message message = JsonSerializer.Deserialize<Message>(jsonString, options);
                return message;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception Occured " + ex.Message);
                return null;
            }
        }
        public async Task<string> TakeScreenShotd(IWebDriver driver)
        {
            try
            {
                var screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshot");
                string fileName = $"ReportConfirmation_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string screenshotPath = Path.Combine(screenshotDir, fileName);

                // Create directory if it doesn't exist
                Directory.CreateDirectory(screenshotDir); // This is safe even if directory exists

                // Delete all existing files in the screenshot directory
                foreach (string file in Directory.GetFiles(screenshotDir))
                {
                    File.Delete(file);
                }

                // Take screenshot
                Screenshot screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                screenshot.SaveAsFile(screenshotPath);
                return screenshotPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception Occured " + ex.Message);
                return string.Empty;
            }
        }
        public async Task<string> TakeScreenShot(IWebDriver driver)
        {
            try
            {
                var screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshot");
                string fileName = $"ReportConfirmation_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string screenshotPath = Path.Combine(screenshotDir, fileName);

                Directory.CreateDirectory(screenshotDir);

                // Delete all existing files in the screenshot directory
                foreach (string file in Directory.GetFiles(screenshotDir))
                {
                    File.Delete(file);
                }

                // 🔥 WORKING FULL PAGE SCREENSHOT (Standard Selenium)
                // Store original window size
                var originalSize = driver.Manage().Window.Size;

                // Get full page dimensions
                var totalHeight = (long)((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.documentElement.scrollHeight");

                var totalWidth = (long)((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.documentElement.scrollWidth");

                // Set window to capture full page (make it bigger, not smaller)
                int newWidth = Math.Max(1920, (int)totalWidth);
                int newHeight = Math.Max(1080, (int)totalHeight);

                driver.Manage().Window.Size = new System.Drawing.Size(newWidth, newHeight);
                await Task.Delay(2000); // Wait for resize

                // Scroll to top
                ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollTo(0, 0)");
                await Task.Delay(1000);

                // Take screenshot
                Screenshot screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                screenshot.SaveAsFile(screenshotPath);

                // Restore original size
                driver.Manage().Window.Size = originalSize;

                Log.Information("Full page screenshot saved at: {screenshotPath}", screenshotPath);
                return screenshotPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception Occurred " + ex.Message);
                return string.Empty;
            }
        }
        public async Task<bool> SendEmail(EmailInfo emailInfo)
        {
            // Email configuration
            string senderEmail = _configuration.GetValue<string>("MailSettings:Email").Trim();
            string password = _configuration.GetValue<string>("MailSettings:Password").Trim();
            string host = _configuration.GetValue<string>("MailSettings:Host").Trim();
            int port = _configuration.GetValue<int>("MailSettings:Port");
            string displayName = _configuration.GetValue<string>("MailSettings:DisplayName");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (MailMessage message = new MailMessage())
            using (SmtpClient smtpClient = new SmtpClient(host, port))
            {
                // Set sender email and display name
                message.From = new MailAddress(senderEmail, displayName, Encoding.UTF8);

                // Set recipient email(s)
                if (!string.IsNullOrEmpty(emailInfo.To))
                {
                    foreach (string toEmailId in emailInfo.To.Split(','))
                    {
                        message.To.Add(new MailAddress(toEmailId.Trim()));
                    }
                }

                // Set CC email(s)
                if (!string.IsNullOrEmpty(emailInfo.Cc))
                {
                    foreach (string ccEmailId in emailInfo.Cc.Split(','))
                    {
                        message.CC.Add(new MailAddress(ccEmailId.Trim()));
                    }
                }

                // Set Bcc email(s)
                if (!string.IsNullOrEmpty(emailInfo.Bcc))
                {
                    foreach (string bccEmailId in emailInfo.Bcc.Split(','))
                    {
                        message.Bcc.Add(new MailAddress(bccEmailId.Trim()));
                    }
                }

                // Set email body and subject
                message.Body = emailInfo.Body;
                message.IsBodyHtml = true;
                message.Subject = emailInfo.Subject;

                // Set email priority
                message.Priority = MailPriority.High;

                // Add attachments
                if (emailInfo.Files != null)
                {
                    foreach (var file in emailInfo.Files)
                    {
                        var attachment = new Attachment(file);
                        attachment.ContentDisposition.CreationDate = File.GetCreationTime(file);
                        attachment.ContentDisposition.ModificationDate = File.GetLastWriteTime(file);
                        attachment.ContentDisposition.ReadDate = File.GetLastAccessTime(file);
                        attachment.ContentDisposition.FileName = Path.GetFileName(file);
                        attachment.ContentDisposition.Size = new FileInfo(file).Length;
                        attachment.ContentDisposition.DispositionType = DispositionTypeNames.Attachment;
                        message.Attachments.Add(attachment);
                    }
                }

                // SMTP client configuration
                smtpClient.EnableSsl = true;
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(senderEmail, password);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.Timeout = 60 * 5 * 1000; // 5 minutes

                try
                {
                    await smtpClient.SendMailAsync(message);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception Occured " + ex.Message);
                    return false;
                }
            }
        }
    }

}
