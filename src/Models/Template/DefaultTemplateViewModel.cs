﻿using Kastra.Core.Modules;

namespace Kastra.Web.Models.Template
{
    public class DefaultTemplateViewModel
    {
        public ModuleDataComponent Body { get; set; }
        public ModuleDataComponent Header { get; set; }
        public ModuleDataComponent Footer { get; set; }
    }
}
