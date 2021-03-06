﻿using NLog;
using System;
using System.Threading.Tasks;

namespace NScheduler.Core.Schedules
{
    /// <summary>
    /// Base class defining <see cref="IJob"/>'s runtime schedule
    /// </summary>
    public abstract class Schedule
    {
        private readonly Logger log; 
        protected readonly DateTimeOffset createdOn;
        protected DateTimeOffset? nextFireTime;
        protected DateTimeOffset? prevFireTime;
        protected JobContext context;
        protected int reTryAttempts;
        protected int timesRun;

        protected Schedule()
        {
            createdOn = Time.Now();
            log = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// Asynchronously waits until schedule reaches its fire time
        /// </summary>
        internal async Task WaitUntilFire()
        {
            if (!nextFireTime.HasValue)
                   return;

            TimeSpan diff = nextFireTime.Value.Subtract(Time.Now());
            log.Debug($"Time until fire: {diff:fffff}");

            if (diff.Ticks > 0)
                  await Task.Delay(diff);
        }

        /// <summary>
        /// Sets job's context
        /// </summary>
        /// <param name="context"></param>
        internal void SetContext(JobContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Sets exact date & time of the next fire
        /// </summary>
        internal void SetNextFireTime()
        {
            timesRun++;
            prevFireTime = nextFireTime;
            nextFireTime = CalculateNextFireTime();
        }

        /// <summary>
        /// Allows a schedule to set initial fire time and 
        /// prepare itself before being enqueued into engine
        /// </summary>
        public virtual void SetInitialFireTime()
        {
            nextFireTime = createdOn;
        }

        /// <summary>
        /// Calculates exact date & time of the next fire
        /// </summary>
        /// <returns></returns>
        public abstract DateTimeOffset? CalculateNextFireTime();

        /// <summary>
        /// Updates schedule for a new time
        /// </summary>
        /// <param name="misfireTime"></param>
        /// <param name="diff"></param>
        public abstract void HandleMisfire(DateTimeOffset misfireTime, TimeSpan diff);

        /// <summary>
        /// Creates a clone of this schedule
        /// </summary>
        /// <returns></returns>
        public virtual Schedule Clone()
        {
            return MemberwiseClone() as Schedule;
        }
        
        /// <summary>
        /// Gets exact date & time of scheduled fire 
        /// </summary>
        public DateTimeOffset? GetNextFireTime() => nextFireTime;

        /// <summary>
        /// Gets max count of re-try attempts before job is considered faulted
        /// </summary>
        public int ReTryAttempts => reTryAttempts;
    }
}
