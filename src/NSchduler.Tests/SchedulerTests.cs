﻿using NSchduler.Tests.Jobs;
using NScheduler.Core;
using NScheduler.Core.Schedules;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NSchduler.Tests
{
    [TestFixture]
    public class SchedulerTests
    {
        private Scheduler scheduler;

        [Test]
        public async Task Test()
        {
            scheduler = new Scheduler();
            EverySecondSchedule schedule = new EverySecondSchedule();

            WriteDebugTextJob job = new WriteDebugTextJob("Hello 1s span");
            WriteDebugTextJob job2 = new WriteDebugTextJob("Hello 5s span");
            WriteDebugTextJob job3 = new WriteDebugTextJob("Hello 10s span");

            
            await scheduler.ScheduleJob(job, schedule);

  
            await scheduler.Run();
        }              
    }
}
