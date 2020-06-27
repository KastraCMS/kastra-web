using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Kastra.Core.Business;
using Kastra.Core.Dto;
using Kastra.Core.DTO;
using Kastra.Web.API.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Kastra.Web.API.Controllers
{
    [Area("Api")]
    [Authorize("Administration")]
    public class UserController : Controller
    {
        #region Private members

        private readonly ILogger _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IStatisticsManager _statisticsManager;

        #endregion

        public UserController(UserManager<ApplicationUser> userManager, 
                              RoleManager<ApplicationRole> roleManager,
                              IStatisticsManager statisticsManager,
                              ILoggerFactory loggerFactory,
                              SignInManager<ApplicationUser> signInManager)
        {
            _logger = loggerFactory.CreateLogger<UserController>();
            _userManager = userManager;
            _roleManager = roleManager;
            _statisticsManager = statisticsManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult List()
        {
            List<ApplicationUser> users = _userManager.Users.ToList();
            List<UserModel> userModels = new List<UserModel>(users.Count);

            foreach (ApplicationUser user in users)
            {
                userModels.Add(ToUserModel(user));
            }

            return Json(userModels);
        }

        [HttpPost]
        
        public async Task<IActionResult> Update([FromBody]UserModel model)
        {
            // Save user
            ApplicationUser user = _userManager.Users.Include(u => u.Roles).SingleOrDefault(u => u.Id == model.Id);

            if(user == null)
            {
                user = new ApplicationUser();
                user.UserName = model.Email;
                user.Email = model.Email;
                user.EmailConfirmed = true;

                IdentityResult result = await _userManager.CreateAsync(user, model.Password);

                if(!result.Succeeded)
                {
                    return BadRequest(result);
                }
            }
            else {
                user.UserName = model.UserName ?? model.Email;
                user.Email = model.Email;
                user.DateModified = DateTime.UtcNow;

                await _userManager.UpdateAsync(user);
            }

            // Save roles
            IdentityUserRole<String> currentRole = null;
            IList<ApplicationRole> roles = _roleManager.Roles.ToList();
            IList<IdentityUserRole<String>> currentRoles = user.Roles.ToList();

            if(roles != null)
            {
                foreach (var role in roles)
                {
                    currentRole = currentRoles.SingleOrDefault(cr => cr.RoleId == role.Id);

                    if (currentRole != null)
                    {
                        if (!model.Roles.Any(sr => sr == role.Id))
                            await _userManager.RemoveFromRoleAsync(user, role.Name);
                    }
                    else
                    {
                        if (model.Roles.Any(sr => sr == role.Id))
                        {
                            await _userManager.AddToRoleAsync(user, role.Name);
                        }
                    }
                }
            }

            return Ok(new { Succeeded = true, UserId = user.Id });
        }

		[HttpGet]
		public IActionResult Get(string id)
		{
			if (String.IsNullOrEmpty(id))
			{
				return BadRequest();
			}

			ApplicationUser user = _userManager.Users.Include(u => u.Roles).SingleOrDefault(u => u.Id == id);

            if(user == null)
			{
				return NotFound();
			}
            
			return Json(ToUserModel(user));
		}

        [HttpDelete]
        
        public async Task<IActionResult> Delete([FromBody]string id)
        {
            ApplicationUser user = _userManager.Users.SingleOrDefault(u => u.Id == id);

            if (user == null)
            {
                return BadRequest("User not found");
            }

            await _userManager.DeleteAsync(user);

            return Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody]LoginViewModel model)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                _logger.LogInformation(1, "User logged in.");

                ApplicationUser user = await _userManager.FindByEmailAsync(model.Email);

                return Ok();
            }

            return Unauthorized();
        }

        #region Private methods
        
        public static UserModel ToUserModel(ApplicationUser user)
        {
            UserModel model = new UserModel();
            model.Id = user.Id;
            model.UserName = user.UserName;
            model.Email = user.Email;
			model.Roles = user.Roles.Select(r => r.RoleId).ToArray();

            return model;
        }

        #endregion
    }
}
