//  ---------------------------------------------------------------------------------
//  Copyright © eDahab. All rights reserved.
//  ---------------------------------------------------------------------------------

namespace easyCheckBalance
{
    public class Config
    {
        public string IPAddress { get; set; }

        public int Port { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string ShortCode { get; set; }

        public string AllowList { get; set; }

        public bool IsLimited { get; set; }
        public string EDahabApiUrl { get; set; }
    }
}