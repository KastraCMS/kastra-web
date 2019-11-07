using System.Collections.Generic;
using Kastra.Core.Business;
using Kastra.Core.Constants;
using Kastra.Core.Dto;
using Kastra.Core.Services;
using Kastra.Web.Admin.Models.Module;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kastra.Web.Admin.Controllers
{
    [Area("Admin")]
    [Authorize("Administration")]
    public class ModuleController : Controller
    {
        #region Private members

        private readonly IViewManager _viewManager;
        private readonly CacheEngine _cacheEngine;

        #endregion

        public ModuleController(CacheEngine cacheEngine, IViewManager viewManager)
        {
            _cacheEngine = cacheEngine;
            _viewManager = viewManager;
        }

        [Route("[area]/[controller]/[action]/mid/{moduleId}/mc/{moduleControl}/ma/{moduleAction?}")]
        [Route("[area]/[controller]/[action]/{moduleId}/{moduleControl}/{moduleAction?}")]
        public IActionResult Settings(int moduleId, string moduleControl, string moduleAction)
        {
            PageInfo page = null;
            SettingsModel model = new SettingsModel();
            ViewEngine viewEngine = null;
            
            // Get module
            ModuleInfo module = _viewManager.GetModule(moduleId, true, true);

            if (module == null)
                return RedirectToAction("Index", "Home");

            // Get page
            page = _viewManager.GetPage(module.PageId);

            if (page == null)
                return RedirectToAction("Index", "Home");

            viewEngine = new ViewEngine(_cacheEngine);
            model.ModuleSettingsView = viewEngine.GetModuleDataByModuleId(page, module, moduleControl, moduleAction);
            model.ModuleSettingsView.RequiredClaims = new List<PermissionInfo>();
            model.ModuleSettingsView.RequiredClaims.Add(new PermissionInfo { Name = ModuleConfiguration.GrantedAccessPermission });

            return View(model);
        }
    }
}
