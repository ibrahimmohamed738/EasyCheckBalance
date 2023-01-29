//  ---------------------------------------------------------------------------------
//  Copyright © eDahab. All rights reserved.
//  ---------------------------------------------------------------------------------

namespace easyCheckBalance.Core
{
    public class Response
    {
        public string Text { get; set; }

        public ResponseType ResponseType { get; set; }
    }

    public enum ResponseType
    {
        Notification,
        Prompt
    }
}
