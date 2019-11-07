using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kastra.Core.Business;
using Kastra.Core.Configuration;
using Kastra.Core.Dto;
using Kastra.Web.API.Models.ModuleDefinition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kastra.Web.API.Controllers
{
    [Area("Api")]
    [Authorize("Administration")]
    public class ModuleDefinitionController : Controller
    {
        private readonly IModuleManager _moduleManager;
        private readonly ISecurityManager _securityManager;
        private readonly IViewManager _viewManager;
        private readonly AppSettings _appSettings;

        public ModuleDefinitionController(IModuleManager moduleManager,
                                          ISecurityManager securityManager,
                                          IViewManager viewManager,
                                          IOptions<AppSettings> appSettings)
        {
            _moduleManager = moduleManager;
            _securityManager = securityManager;
            _viewManager = viewManager;
            _appSettings = appSettings.Value;
        }

        [HttpGet]
        public IActionResult List()
        {
            IList<ModuleDefinitionInfo> moduleDefinitions = _viewManager.GetModuleDefsList();
            List<ModuleDefinitionModel> moduleDefinitionModels = new List<ModuleDefinitionModel>(moduleDefinitions.Count);

            foreach (ModuleDefinitionInfo moduleDefinition in moduleDefinitions)
            {
                moduleDefinitionModels.Add(ToModuleDefinitionModel(moduleDefinition));
            }

            return Json(moduleDefinitionModels);
        }

        [HttpGet]
        public IActionResult ModuleFound()
        {
            ModuleDefinitionInfo moduleDefinition = null;
            ModuleInstallModel moduleModel = null;
            List<ModuleInstallModel> model = new List<ModuleInstallModel>();
            IList<ModuleDefinitionInfo> modulesDefinitions = _viewManager.GetModuleDefsList();

            foreach (Assembly assembly in KastraAssembliesContext.Instance.GetModuleAssemblies())
            {
                moduleDefinition = modulesDefinitions.SingleOrDefault(m => IsInFolder(assembly.Location, m.Path));

                moduleModel = new ModuleInstallModel();
                moduleModel.AssemblyName = assembly.GetName().Name;
                moduleModel.Version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
                moduleModel.ModuleDefinitionId = moduleDefinition?.ModuleDefId ?? 0;
                moduleModel.Name = moduleDefinition?.Name ?? String.Empty;

                model.Add(moduleModel);
            }

            return Ok(model);
        }

        [HttpPost]
        
        public IActionResult Install([FromBody] string assemblyName)
        {
            Assembly assembly = null;

            if(String.IsNullOrEmpty(assemblyName))
            {
                return BadRequest();
            }

            // Get assembly with assembly name
            assembly = KastraAssembliesContext.Instance.GetModuleAssemblies()
                                              .SingleOrDefault(a => a.GetName().Name == assemblyName);

            if(assembly == null)
            {
                return BadRequest("Module assembly not found");
            }

            // Install module
            _moduleManager.InstallModule(assembly);

            return Ok();
        }

        [HttpPost]
        
        public IActionResult Uninstall([FromBody] ModuleDefinitionModel model)
        {
            Assembly assembly = null;
            ModuleDefinitionInfo moduleDefinition = null;
            IList<ModuleInfo> modules = null;
            IList<ModulePermissionInfo> modulesPermissions = null;

            if (model ==  null || String.IsNullOrEmpty(model.Name))
            {
                return BadRequest();
            }

            // Get assembly with assembly name
            assembly = KastraAssembliesContext.Instance.GetModuleAssemblies()
                                              .SingleOrDefault(a => a.GetName().Name == model.Name);

            if (assembly == null)
            {
                return BadRequest("Module assembly not found");
            }

            // Uninstall module
            _moduleManager.UninstallModule(assembly);

            // Get module definition
            moduleDefinition = _viewManager.GetModuleDef(model.Id, true);

            if(moduleDefinition == null)
            {
                return BadRequest("Cannot find the module definition");
            }

            modules = _viewManager.GetModulesList().Where(m => m.ModuleDefId == moduleDefinition.ModuleDefId).ToList();

            // Remove module controls
            foreach(ModuleControlInfo moduleControl in moduleDefinition.ModuleControls)
            {
                _viewManager.DeleteModuleControl(moduleControl.ModuleControlId);
            }

            // Remove all modules
            if(modules != null)
            {
                foreach (ModuleInfo module in modules)
                {
                    modulesPermissions = _securityManager.GetModulePermissionsByModuleId(module.ModuleId);

                    if (modulesPermissions != null)
                    {
                        foreach (ModulePermissionInfo modulePermission in modulesPermissions)
                        {
                            _securityManager.DeleteModulePermission(modulePermission.ModulePermissionId);
                        }
                    }

                    _viewManager.DeleteModule(module.ModuleId);
                }
            }

            // Remove module definition
            _viewManager.DeleteModuleDef(moduleDefinition.ModuleDefId);

            return Ok();
        }

        #region Private methods

		public static ModuleDefinitionModel ToModuleDefinitionModel(ModuleDefinitionInfo moduleDefinitionInfo)
        {
            ModuleDefinitionModel model = new ModuleDefinitionModel();
			model.Id = moduleDefinitionInfo.ModuleDefId;
			model.Name = moduleDefinitionInfo.Name;
				
            return model;
        }

        private bool IsInFolder(string assemblyPath, string moduleDefinitionFolder)
        {

            string moduleRootDirectory = Path.Combine(Directory.GetCurrentDirectory(), 
                                                      _appSettings.Configuration.ModuleDirectoryPath, 
                                                      moduleDefinitionFolder);
            Uri moduleUri = new Uri(moduleRootDirectory);
            Uri assemblyUri = new Uri(assemblyPath);

            return (assemblyUri.LocalPath).Contains(moduleUri.LocalPath);
        }

        #endregion
    }
}
