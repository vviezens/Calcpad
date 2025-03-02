using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Calcpad.Wpf
{
    public class TemplateYaml
    {
        public string FileType { get; set; }
        public List<string> Path { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int Likes { get; set; }
        public int Validations { get; set; }
        public string Creator { get; set; }
        public List<string> Authors { get; set; }
        public string CreatedDate { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }
        public List<string> ValidatorHashes { get; set; }
        public List<string> ValidatorLogos { get; set; }
        public string ValidatorContact { get; set; }
    }

    public class TemplateCollection
    {
        public List<TemplateYaml> Templates { get; set; }
    }
}
