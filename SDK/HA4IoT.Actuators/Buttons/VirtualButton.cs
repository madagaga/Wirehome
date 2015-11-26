﻿using HA4IoT.Contracts;
using HA4IoT.Contracts.Notifications;
using HA4IoT.Networking;

namespace HA4IoT.Actuators
{
    public class VirtualButton : ButtonBase
    {
        public VirtualButton(ActuatorId id, IHttpRequestController api, INotificationHandler log)
            : base(id, api, log)
        {
        }
    }
}