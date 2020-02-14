using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kastra.Core.Business;
using Kastra.Core.Dto;
using Kastra.Web.Identity;
using Kastra.Web.Models.Install;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Kastra.Controllers
{
    public class InstallController : Controller
    {
        #region Private properties

        private IConfiguration _configuration;
        private readonly ILogger _logger;

        #endregion

        public InstallController(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<InstallController>();
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Kastra - Installation";

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString) || !DatabaseExists(connectionString, true))
                return View();

            return Redirect("page/home");
        }

        [HttpPost]
        
        public IActionResult Database([FromBody] DatabaseViewModel databaseForm, [FromServices] IHostApplicationLifetime applicationLifetime)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrEmpty(connectionString) || DatabaseExists(connectionString))
                return BadRequest();

            // Create connexion string
            connectionString = GenerateConnectionString(databaseForm.DatabaseServer, databaseForm.DatabaseName,
                                databaseForm.DatabaseLogin, databaseForm.DatabasePassword, databaseForm.IntegratedSecurity);
            try
            {
                if (!DatabaseExists(connectionString))
                    return BadRequest("Cannot connect to the database");

                SaveConnectionString(connectionString);
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }

            // Restart application
            applicationLifetime.StopApplication();

            return Ok();
        }

        [HttpPost]
        
        public async Task<IActionResult> Account([FromBody] AccountViewModel model, [FromServices] ApplicationDbContext applicationDbContext,
                [FromServices] IApplicationManager applicationManager, [FromServices] IModuleManager moduleManager,
                [FromServices] UserManager<ApplicationUser> userManager, [FromServices] RoleManager<ApplicationRole> roleManager)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser user = new ApplicationUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };

                // Check password
                List<string> passwordErrors = await GetPasswordErrors(userManager, model.Password);

                if (passwordErrors.Count > 0)
                {
                    return BadRequest(passwordErrors);
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connectionString) || DatabaseExists(connectionString, true))
                {
                    return BadRequest();
                }

                try
                {
                    // Create identity tables
                    applicationDbContext.Database.Migrate();

                    // Create host user
                    IdentityResult result = await userManager.CreateAsync(user, model.Password);

                    if (!result.Succeeded)
                    {
                        return BadRequest(result.Errors);
                    }

                    // Install roles
                    ApplicationRole role = new ApplicationRole();
                    role.Name = "Administrator";
                    await roleManager.CreateAsync(role);

                    IdentityRoleClaim<string> roleClaim = new IdentityRoleClaim<string>();
                    roleClaim.ClaimType = "GlobalSettingsEdition";
                    roleClaim.ClaimValue = "GlobalSettingsEdition";
                    roleClaim.RoleId = role.Id;
                    await roleManager.AddClaimAsync(role, roleClaim.ToClaim());

                    // Add user to admin role
                    await userManager.AddToRoleAsync(user, role.Name);

                    // Create kastra tables
                    applicationManager.Install();

                    // Install default template
                    applicationManager.InstallDefaultTemplate();

                    // Install default page
                    applicationManager.InstallDefaultPage();

                    // Install default permissions
                    applicationManager.InstallDefaultPermissions();

                    // Install modules
                    moduleManager.InstallModules();
                }
                catch(Exception ex)
                {
                    _logger.LogError(0, ex, null);
                    return BadRequest(ex.Message);
                }
            }

            return Ok();
        }

        [HttpGet]
        public IActionResult CheckDatabase()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                if (DatabaseExists(connectionString))
                {
                    return Ok(true);
                }
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);   
            }

            return Ok(false);
        }

        [HttpGet]
        public IActionResult CheckDatabaseTables()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                if (DatabaseExists(connectionString, true))
                {
                    return Ok(true);
                }
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);   
            }

            return Ok(false);
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private bool DatabaseExists(string connectionString, bool checkTables = false)
        {
            int numberTables = 0;

            if (string.IsNullOrEmpty(connectionString))
                return false;

            string queryString = "SELECT Count(*) from sys.tables where name like 'Kastra%'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                if (!checkTables)
                {
                    return true;
                }

                SqlCommand command = new SqlCommand(queryString, connection);
                numberTables = (int)command.ExecuteScalar();
            }

            return (numberTables > 0);
        }

        private string GenerateConnectionString(string server, string databaseName, string login, string password, bool integratedSecurity)
        {
            if (integratedSecurity)
            {
                return $"Server={server};Database={databaseName};Integrated Security=True;";
            }
            else
            {
                return $"Server={server};Database={databaseName};Integrated Security=False;User Id={login};Password={password};";
            }
        }

        private void SaveConnectionString(string connectionString)
        {
            string json = System.IO.File.ReadAllText(@"appsettings.json");

            dynamic dynamicObject = JsonConvert.DeserializeObject<dynamic>(json);
            dynamicObject.ConnectionStrings.DefaultConnection = connectionString;

            string output = JsonConvert.SerializeObject(dynamicObject);

            System.IO.File.WriteAllText(@"appsettings.json", output);
        }

        private async Task<List<string>> GetPasswordErrors(UserManager<ApplicationUser> userManager, string password)
        {
            List<string> errors = new List<string>();
            IList<IPasswordValidator<ApplicationUser>> validators = userManager.PasswordValidators;

            if (validators is null)
            {
                return errors;
            }

            foreach (IPasswordValidator<ApplicationUser> validator in validators)
            {
                IdentityResult result = await validator.ValidateAsync(userManager, null, password);

                if (!result.Succeeded)
                {
                    foreach (IdentityError error in result.Errors)
                    {
                        errors.Add(error.Description);
                    }
                }
            }

            return errors;
        }

        private async Task<List<string>> GetUserErrors(UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            List<string> errors = new List<string>();
            IList<IUserValidator<ApplicationUser>> validators = userManager.UserValidators;

            if (validators is null)
            {
                return errors;
            }

            foreach (IUserValidator<ApplicationUser> validator in validators)
            {
                IdentityResult result = await validator.ValidateAsync(userManager, user);

                if (!result.Succeeded)
                {
                    foreach (IdentityError error in result.Errors)
                    {
                        errors.Add(error.Description);
                    }
                }
            }

            return errors;
        }

        #endregion
    }
}