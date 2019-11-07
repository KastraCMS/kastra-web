using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kastra.Core.Business;
using Kastra.Core.Configuration;
using Kastra.Core.Dto;
using Kastra.Web.API.Models.Template;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kastra.Web.Areas.API.Controllers
{
    [Area("Api")]
    [Authorize("Administration")]
	public class TemplateController : Controller
    {
        private readonly IViewManager _viewManager;

		public TemplateController(IViewManager viewManager)
        {
            _viewManager = viewManager;
        }

        [HttpGet]
        public IActionResult Get(int id)
        {
			TemplateInfo template = _viewManager.GetPageTemplate(id);

			if (template == null)
            {
                return NotFound();
            }

			return Json(ToTemplateModel(template));
        }

        [HttpGet]
        public IActionResult List()
        {
			IList<TemplateInfo> templates = _viewManager.GetPageTemplatesList();
            List<TemplateModel> templateModels = new List<TemplateModel>(templates.Count);
            
			foreach (TemplateInfo template in templates)
            {
				templateModels.Add(ToTemplateModel(template));
            }

            return Ok(templateModels);
        }

        [HttpGet]
        public IActionResult ListViewPaths()
        {
            var controllers = GetControllerTypes()
                                .Select(c => c.Name.Replace("Controller", String.Empty))
                                .Select(c => new { Name = c, Value = c });

            return Ok(controllers);
        }

        [HttpGet]
        public IActionResult ListKeynames(string viewPath)
        {
            TypeInfo controllerType = GetControllerTypes()
                                        .SingleOrDefault(c => c.Name.Replace("Controller", String.Empty) == viewPath);

            if (controllerType == null)
            {
                return BadRequest("Forbidden view path.");
            }

            // Get all template available for the selected controller
            string viewDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Views", controllerType.Name.Replace("Controller", String.Empty));
            var templates = Directory.GetFiles(viewDirectoryPath)
                            .Select(c => Path.GetFileName(c).Replace(".cshtml", String.Empty))
                            .Select(c => new { Name = c, Value = c });

            return Ok(templates);
        }

        [HttpPost]
        public IActionResult Update([FromBody]TemplateModel model)
        {
            TemplateInfo template = null;
            
            if (model.Id > 0)
            {
                template = _viewManager.GetPageTemplate(model.Id);
            }

            if (template == null)
            {
                template = new TemplateInfo();
            }

            template.Name = model.Name;
            template.KeyName = model.KeyName;
            template.ModelClass = model.ModelClass;
            template.ViewPath = model.ViewPath.Replace("..", String.Empty);

            _viewManager.SavePageTemplate(template);

            return Ok(new { template.TemplateId });
        }

        [HttpDelete]
        public IActionResult Delete([FromBody]int id)
        {
            if (_viewManager.GetPageTemplate(id) == null)
            {
                return BadRequest("Template not found");
            }

            if (_viewManager.DeletePageTemplate(id))
            {
                return Ok();
            }

            return BadRequest();
        }
        
        #region Private methods
        
		public static TemplateModel ToTemplateModel(TemplateInfo templateInfo)
        {
            TemplateModel model = new TemplateModel();
			model.Id = templateInfo.TemplateId;
			model.Name = templateInfo.Name;
            model.KeyName = templateInfo.KeyName;
            model.ModelClass = templateInfo.ModelClass;
            model.ViewPath = templateInfo.ViewPath;

            return model;
        }

        /// <summary>
        /// Get all template controllers
        /// </summary>
        /// <returns></returns>
        public IList<TypeInfo> GetControllerTypes()
        {
            Type templateType = typeof(Kastra.Core.Templates.Controllers.TemplateController);
            IList<TypeInfo> types = new List<TypeInfo>();

            foreach(Assembly assembly in KastraAssembliesContext.Instance.Assemblies.Values)
            {
                foreach (TypeInfo typeInfo in assembly.DefinedTypes)
                {
                    if (typeInfo.IsSubclassOf(templateType))
                    {
                        types.Add(typeInfo);
                    }
                }
            }

            return types;
        }

        #endregion
    }
}
