﻿using CK.HomeAutomation.Networking;
using CK.HomeAutomation.Notifications;

namespace CK.HomeAutomation.Actuators
{
    public class VirtualButton : ButtonBase
    {
        public VirtualButton(string id, IHttpRequestController httpApiController, INotificationHandler notificationHandler)
            : base(id, httpApiController, notificationHandler)
        {
        }
    }
}
