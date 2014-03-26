using System;
using System.ComponentModel;

namespace AdvancedCodeGenerator
{
    [Serializable]
    class Settings
    {
        [Category("ActionScript")]
        [DisplayName("Access modifiers")]
        [Description("")]
        [DefaultValue(new string[] { "internal", "public", "protected", "private" })]
        public string[] ASAccessModifiers { get; set; }

        [Category("ActionScript")]
        [DisplayName("Order of access modifiers")]
        [Description("")]
        [DefaultValue(new string[]{ "internal", "public", "protected", "private"})]
        public string[] ASOrderOfAccessModifiers { get; set; }
        
        [Category("Haxe")]
        [DisplayName("Use private modifier explicitly")]
        [Description("Use private modifier explicitly for methods, vars, accessors")]
        [DefaultValue(true)]
        public Boolean UsePrivateExplicitly { get; set; }
    }
}