using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kastra.Core.Business;
using Kastra.Core.Constants;
using Kastra.Core.Dto;
using Kastra.Web.API.Models.Role;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kastra.Web.API.Controllers
{
	[Area("Api")]
    [Authorize("Administration")]
    public class RoleController : Controller
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ISecurityManager _securityManager;
        
        public RoleController(RoleManager<ApplicationRole> roleManager, ISecurityManager securityManager)
        {
            _roleManager = roleManager;
            _securityManager = securityManager;
        }

        [HttpGet]
        public IActionResult List()
        {
            IList<ApplicationRole> roles = _roleManager.Roles.ToList();
            List<RoleModel> roleModels = new List<RoleModel>(roles.Count);

            foreach (ApplicationRole role in roles)
            {
                roleModels.Add(ToRoleModel(role));
            }

            return Json(roleModels);
        }

        [HttpPost]
        
        public async Task<IActionResult> Update([FromBody]RoleModel model)
        {
            bool createMode = false;

            // Save role
            ApplicationRole role = _roleManager.Roles.SingleOrDefault(r => r.Id == model.Id);

            if (role == null)
            {
                createMode = true;
                role = new ApplicationRole();
            }

            role.Name = model.Name;

            // Save permissions
            IdentityRoleClaim<string> currentPermission = null;
            IList<PermissionInfo> permissions = _securityManager.GetPermissionsList();
            IList<IdentityRoleClaim<string>> currentPermissions = role.Claims.ToList();

            if (createMode)
            {
                await _roleManager.CreateAsync(role);
            }
            else
            {
                await _roleManager.UpdateAsync(role);
            }

            foreach (PermissionInfo permission in permissions)
            {
                currentPermission = currentPermissions.SingleOrDefault(c => c.ClaimType == ModuleConfiguration.ModulePermissionType && c.ClaimValue == permission.PermissionId.ToString());

                if (currentPermission != null)
                {
                    if (!model.Permissions.Any(sp => sp == permission.PermissionId))
                    {
                        await _roleManager.RemoveClaimAsync(role, currentPermission.ToClaim());
                    }
                }
                else
                {
                    if (model.Permissions.Any(sp => sp == permission.PermissionId))
                    {
                        currentPermission = new IdentityRoleClaim<string>();
                        currentPermission.ClaimType = ModuleConfiguration.ModulePermissionType;
                        currentPermission.ClaimValue = permission.PermissionId.ToString();
                        currentPermission.RoleId = role.Id;

                        await _roleManager.AddClaimAsync(role, currentPermission.ToClaim());
                    }
                }
            }

            return Ok(new { RoleId = role.Id });
        }


		[HttpGet]
        public IActionResult Get(string id)
        {
			ApplicationRole role = _roleManager.Roles
			                                   .Include(r => r.Claims)
			                                   .SingleOrDefault(r => r.Id == id);

            if(role == null)
			{
				return NotFound();
			}
         
			return Json(ToRoleModel(role));
        }

        [HttpDelete]
        
        public async Task<IActionResult> Delete([FromBody]string id)
        {
            ApplicationRole role = _roleManager.Roles.SingleOrDefault(r => r.Id == id);

            if (role == null)
            {
                return BadRequest("Role not found");
            }

            await _roleManager.DeleteAsync(role);

            return Ok();
        }

        #region Private methods

		public static RoleModel ToRoleModel(ApplicationRole roleInfo)
        {         
			RoleModel model = new RoleModel();
            model.Id = roleInfo.Id;
            model.Name = roleInfo.Name;
			model.Permissions = new List<int>(roleInfo.Claims.Count);

			int permissionId = 0;

			foreach(var claim in roleInfo.Claims)
			{
				if(claim.ClaimType == ModuleConfiguration.ModulePermissionType && int.TryParse(claim.ClaimValue, out permissionId))
				{
					model.Permissions.Add(permissionId);
				}
			}
            
            return model;
        }

        #endregion
    }
}
