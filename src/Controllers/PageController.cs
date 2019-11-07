using Kastra.Core.Business;
using Kastra.Core.Services;
using Kastra.Core.Templates.Controllers;
using Kastra.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Kastra.Web.Controllers
{
    [SiteConfiguration]
    public class PageController : TemplateController
    {
        public PageController(IViewManager viewManager,
                              CacheEngine cacheEngine, 
                              IViewComponentDescriptorCollectionProvider viewcomponents, 
                              IParameterManager parameterManager) 
                            : base(viewManager, cacheEngine, viewcomponents, parameterManager){}

        public IActionResult Home()
        {
            return Index("Home", 0, string.Empty, string.Empty);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
