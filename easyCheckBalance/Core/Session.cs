//  ---------------------------------------------------------------------------------
//  Copyright © eDahab. All rights reserved.
//  ---------------------------------------------------------------------------------

using easyCheckBalance.Models;

namespace easyCheckBalance.Core
{
    public class Session
    {
        public string MSISDN { get; set; }
        public string CurrentMenu { get; set; }
        public EDahabUserInfo EdahabUser { get; set; }
    }
}
