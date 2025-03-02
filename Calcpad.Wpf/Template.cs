using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calcpad.Wpf
{
    public class Template
    {

        public string FileType { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public List<string> Path { get; set; } = new List<string>();
        public int Likes { get; set; }
        public int Validations { get; set; }
        public string Creator { get; set; }
        public string CreatedDate { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }

        /*
        public string Authors { get; set; }

        public List<string> ValidatorHashes { get; set; } = new List<string>();
        public List<string> ValidatorLogos { get; set; } = new List<string>();
        public string ValidatorContact { get; set; }
        */
    }




}
