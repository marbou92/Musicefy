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
    }
}
