﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TRL.Common.Data
{
    public interface ISubscriber
    {
        void Subscribe();
        void Unsubscribe();
        int SubscriptionsCounter { get; }
    }
}
