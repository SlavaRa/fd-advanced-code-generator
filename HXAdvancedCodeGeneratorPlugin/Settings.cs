using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace AdvancedCodeGenerator
{
    [Serializable]
    class Settings
    {
        [Category("Haxe")]
        [DisplayName("Use private modifier explicitly")]
        [Description("Use private modifier explicitly for methods, vars, accessors")]
        [DefaultValue(true)]
        public Boolean UsePrivateExplicitly { get; set; }

    }
}
