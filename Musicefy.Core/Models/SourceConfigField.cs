using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class SourceConfigField
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public bool IsPassword { get; set; }
        public bool IsRequired { get; set; }
        public string Placeholder { get; set; }
        public string DefaultValue { get; set; }

        /// <summary>
        /// The type of field for UI rendering.
        /// Supported: "text" (default), "password", "select", "checkbox"
        /// </summary>
        public string FieldType { get; set; } = "text";

        /// <summary>
        /// For "select" fields: the available options as key-value pairs.
        /// Key is the stored value, Value is the display label.
        /// </summary>
        public Dictionary<string, string> Options { get; set; }
    }
}
