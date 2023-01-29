using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace easyCheckBalance.Models
{
    public class EDahabUserInfo
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string CategoryCode { get; set; }
        public string Gender { get; set; }
        public string Msisdn { get; set; }
        public string Status { get; set; }
        public string IsBarred { get; set; }
    }
}
