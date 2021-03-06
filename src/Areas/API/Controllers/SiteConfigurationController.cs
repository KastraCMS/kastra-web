﻿using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Kastra.Core.Business;
using Kastra.Core.Dto;
using Kastra.Core.Services;
using Kastra.Web.Areas.API.Models.SiteConfiguration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Hosting;
using Kastra.Core.Configuration;
using Kastra.Core.Constants;

namespace Kastra.Web.API.Controllers
{
    [Area("Api")]
    [Authorize("Administration")]
	public class SiteConfigurationController : Controller
    {
        private readonly CacheEngine _cacheEngine;
		private readonly IParameterManager _parameterManager;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IEmailSender _emailSender;
        private readonly IEmailManager _emailManager;

        public SiteConfigurationController(CacheEngine cacheEngine, IParameterManager parametermanager,
            IWebHostEnvironment hostingEnvironment, IEmailSender emailSender, IEmailManager emailManager)
		{
            _cacheEngine = cacheEngine;
			_parameterManager = parametermanager;
            _hostingEnvironment = hostingEnvironment;
            _emailSender = emailSender;
            _emailManager = emailManager;
		}

		[HttpGet]
		public IActionResult Get()
		{
            SiteConfigurationModel model = null;
			SiteConfigurationInfo configuration = _parameterManager.GetSiteConfiguration();

			if(configuration == null)
			{
				return NotFound();
			}

            // Get themes list
            DirectoryInfo themeDirectory = new DirectoryInfo(Path.Combine(_hostingEnvironment.WebRootPath, "themes"));
            DirectoryInfo[] themes = themeDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly);

            model = new SiteConfigurationModel();
            model.Title = configuration.Title;
            model.Description = configuration.Description;
            model.HostUrl = configuration.HostUrl;
            model.CacheActivated = configuration.CacheActivated;
            model.SmtpHost = configuration.SmtpHost;
            model.SmtpPort = configuration.SmtpPort.ToString();
            model.SmtpCredentialsUser = configuration.SmtpCredentialsUser;
            model.SmtpCredentialsPassword = configuration.SmtpCredentialsPassword;
            model.SmtpEnableSsl = configuration.SmtpEnableSsl;
            model.EmailSender = configuration.EmailSender;
            model.RequireConfirmedEmail = configuration.RequireConfirmedEmail;
            model.Theme = configuration.Theme;
            model.ThemeList = themes?.Select(t => t.Name)?.OrderBy(t => t)?.ToArray() ?? new string[] { SiteConfiguration.DefaultTheme };
            model.CookieUsePolicyUrl = configuration.CookieUsePolicyUrl;
            model.ConsentNotice = configuration.ConsentNotice;
            model.CheckConsentNeeded = configuration.CheckConsentNeeded;

            return Json(model);
		}

        [HttpPost]
        
        public IActionResult Update([FromBody]SiteConfigurationModel model)
        {
            SiteConfigurationInfo conf = new SiteConfigurationInfo();
            conf.Title = model.Title;
            conf.Description = model.Description;
            conf.HostUrl = model.HostUrl;
            conf.CacheActivated = model.CacheActivated;
            conf.SmtpHost = model.SmtpHost;
            conf.SmtpPort = int.Parse(model.SmtpPort);
            conf.SmtpCredentialsUser = model.SmtpCredentialsUser;
            conf.SmtpCredentialsPassword = model.SmtpCredentialsPassword;
            conf.SmtpEnableSsl = model.SmtpEnableSsl;
            conf.EmailSender = model.EmailSender;
            conf.RequireConfirmedEmail = model.RequireConfirmedEmail;
            conf.Theme = model.Theme;
            conf.CookieUsePolicyUrl = model.CookieUsePolicyUrl;
            conf.ConsentNotice = model.ConsentNotice;
            conf.CheckConsentNeeded = model.CheckConsentNeeded;

            // Cache
            if (model.CacheActivated)
            {
                _cacheEngine.EnableCache();
                _cacheEngine.ClearSiteConfig();
            }
            else
            {
                _cacheEngine.DisableCache();
            }

            _parameterManager.SaveSiteConfiguration(conf);
            _cacheEngine.ClearAllCache();

            return Ok();
        }

        /// <summary>
        /// Get the mail templates.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult GetMailTemplateList()
        {
            IList<MailTemplateModel> mailTemplates = _emailManager.GetMailTemplates()?.Select(mt => new MailTemplateModel() {
                Id = mt.MailtemplateId,
                Keyname = mt.Keyname,
                Subject = mt.Subject,
                Message = mt.Message
            })?.ToList();

            return Ok(mailTemplates);
        }

        [HttpPost]
        public IActionResult SaveMailTemplate([FromBody] MailTemplateModel model)
        {
            if (string.IsNullOrEmpty(model.Keyname))
            {
                throw new ArgumentNullException(nameof(model.Keyname));
            }

            MailTemplateInfo mailTemplate = _emailManager.GetMailTemplate(model.Keyname);

            if (mailTemplate != null)
            {
                mailTemplate.Subject = model.Subject;
                mailTemplate.Message = model.Message;
                _emailManager.UpdateMailTemplate(mailTemplate);

                return Ok();
            }

            return BadRequest("The mail template was not updated");
        }

        /// <summary>
        /// Send a test email to the user.
        /// </summary>
        /// <param name="userManager"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> TestMailAsync([FromServices]UserManager<ApplicationUser> userManager)
        {
            ApplicationUser user = await userManager.GetUserAsync(HttpContext.User);
            _emailSender.SendEmail(user.Email, "Test", "Test message.");

            return Ok();
        }

        /// <summary>
        /// Restart the website.
        /// </summary>
        /// <returns></returns>
        /// <param name="applicationLifetime">Application lifetime.</param>
        [HttpGet]
        public IActionResult Restart([FromServices] IHostApplicationLifetime applicationLifetime)
        {
            applicationLifetime.StopApplication();

            return Ok();
        }

        /// <summary>
        /// Gets the application versions.
        /// </summary>
        /// <returns>The application versions.</returns>
        [HttpGet]
        public IActionResult GetApplicationVersions()
        {
            // Get application version
            Assembly assembly = Assembly.GetExecutingAssembly();
            string applicationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                        .InformationalVersion;

            // Get the Kastra core version
            string kastraVersion = typeof(Configuration).Assembly
                                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                        .InformationalVersion;

            // Get the dotnet core version
            string dotnetCoreVersion = Assembly.GetEntryAssembly()?
                                    .GetCustomAttribute<TargetFrameworkAttribute>()?
                                    .FrameworkName;

            // Get the OS
            string osPlatform = RuntimeInformation.OSDescription;

            var stats = new
            {
                ApplicationVersion = applicationVersion, 
                CoreVersion = kastraVersion,
                OsPlatform = osPlatform,
                AspDotnetVersion = dotnetCoreVersion
            };

            return Json(stats);
        }
    }
}
