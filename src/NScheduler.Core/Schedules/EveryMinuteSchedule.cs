﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScheduler.Core.Schedules
{
    public class EveryMinuteSchedule : PeriodicSchedule<EveryMinuteSchedule>
    {
        public override TimeInterval Period => TimeInterval.Minutes;
    }
}
