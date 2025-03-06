using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calcpad.Shared
{
    public class Template
    {

        public string FileType { get; set; }
        public string Title { get; set; }
        public List<string> Path { get; set; } = new List<string>();
        public int Likes { get; set; }
        public int Validations { get; set; }
        public string Creator { get; set; }
        public string CreatedDate { get; set; }


        public string Url { get; set; }


        // new fields

        public List<string> Tags { get; set; } = new List<string>();

        public string Template_name { get; set; }
        public string Id { get; set; }

        public string Version { get; set; }
        public string Description { get; set; }

        public string Author_name { get; set; }
        public string Author_public_key { get; set; }

        public string Created_at { get; set; }

        public string Updated_at { get; set; }

        public string Deprecated { get; set; }

        public int Deprecation_total_votes { get; set; }

        public int Deprecation_votes_needed { get; set; }

        public List<string> User_votes_user_ids { get; set; } = new List<string>();// list of user ids who voted
        public new List<string> User_votes_user_ids_weight { get; set; } = new List<string>(); //list of users weights

        public int Likes_total { get; set; } // total count of likes

        public List<string> Likes_users { get; set; } = new List<string>();  // public keys of users that gave likes

        public int Validation_total { get; set; }
        public List<string> Validation_user_ids { get; set; } = new List<string>(); // list of user ids that made validations

        public List<string> Validation_signatures { get; set; } = new List<string>(); // list of validation signatures

        public string Hash { get; set; }

        public string Content { get; set; }


    }




}
