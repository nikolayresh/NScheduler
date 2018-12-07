﻿using NLog;
using NScheduler.Core.Schedules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NScheduler.Core
{
    public class Scheduler
    {
        private const int PauseWaitMs = 1000;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly SortedSet<JobHolder> jobsQueue;
        private readonly List<JobHolder> nextJobs;
        private readonly object pauseLock;
        private volatile bool paused;
        private volatile bool running;
        private volatile Task execTask;

        public Scheduler()
        {
            this.jobsQueue = new SortedSet<JobHolder>(NextFireTimeComparator.GetInstance());
            this.nextJobs = new List<JobHolder>();
            this.pauseLock = new object();
        }

        /// <summary>
        /// Entry point to start scheduler's execution
        /// </summary>
        /// <returns></returns>
        public virtual Task Run()
        {
            if (execTask != null)
                  return execTask;

            Debug("Scheduler starting ...");

            running = true;
            paused = false;

            execTask = Task.Run(() =>
            {
                Debug("Scheduler started");

                while (running)
                {
                    lock (pauseLock)
                    {
                        while (paused && running)
                        {
                            try
                            {
                                // wait until scheduler resumes
                                Monitor.Wait(pauseLock, PauseWaitMs);
                            } catch
                            {
                            }
                        }

                        if (!running)
                               break;
                    }

                    DateTimeOffset now = DateTimeOffset.Now;
                    nextJobs.Clear();

                    lock (jobsQueue)
                    {
                        while (true)
                        {
                            JobHolder jh = jobsQueue.FirstOrDefault();
                            if (jh == null)
                                  break;

                            DateTimeOffset? nextFireTime = jh.Schedule.GetNextFireTime();

                            if (!nextFireTime.HasValue)
                            {
                                jobsQueue.Remove(jh);
                                continue;
                            }

                            if (nextFireTime > now)
                                  break;

                            nextJobs.Add(jh);
                            jobsQueue.Remove(jh);
                        }                                                                        
                    } // end LOCK

                    if (nextJobs.Count > 0)
                    {
                        // check if pause requested
                        // just after jobs fetched
                        bool pauseReq;
                        lock (pauseLock)
                            pauseReq = paused;

                        if (pauseReq)
                        {
                            // save jobs until next resume
                            lock (jobsQueue)
                            {
                                foreach (JobHolder jh in nextJobs)
                                   jobsQueue.Add(jh);
                                continue;
                            }
                        }

                        foreach (JobHolder jh in nextJobs)
                        {
                            Task.Run(async () => 
                            {
                                try
                                {
                                    await jh.Job.Execute(jh.Context);
                                    jh.Context.OnJobExecuted(jh);
                                    lock (jobsQueue) jobsQueue.Add(jh);
                                } catch (Exception ex)
                                {
                                    Exception lastError = ex;
                                    int maxReTry = jh.Schedule.ReTryAttempts;

                                    if (maxReTry > 0)
                                    {
                                        while (maxReTry-- > 0)
                                        {
                                            try
                                            {
                                                jh.Context.IncrementReTryAttempt();
                                                await jh.Job.Execute(jh.Context);
                                                jh.Context.OnJobExecuted(jh);
                                                lock (jobsQueue) jobsQueue.Add(jh);
                                                return;
                                            } catch (Exception exOnReTry)
                                            {
                                                lastError = exOnReTry;
                                                jh.Context.SetLastError(lastError);
                                                continue;
                                            }
                                        }
                                    }

                                    jh.Context.OnJobFaulted(lastError, jh);
                                }                                  
                            });
                        }
                    }
                }

                Debug("Scheduler shutting down ...");
            });
            return execTask;
        }

        protected void Debug(string msg)
        {
            if (logger.IsDebugEnabled)
                  logger.Debug(msg);
        }

        /// <summary>
        /// Schedules a new job 
        /// </summary>
        /// <param name="job"></param>
        /// <param name="schedule"></param>
        /// <returns></returns>
        public virtual Task ScheduleJob(IJob job, Schedule schedule)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job), "Job is NULL");
            }

            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule), "Job's schedule is NULL");
            }

            lock (jobsQueue)
                jobsQueue.Add(new JobHolder(job, schedule));
            return Task.CompletedTask;
        }

        public virtual Task ScheduleJob(Action action, Schedule schedule)
        {
            IJob job = ActionJob.FromAction(action);
            return ScheduleJob(job, schedule);
        }


        public virtual Task ScheduleJob(Action<JobContext> action, Schedule schedule)
        {
            IJob job = ActionJob.FromAction(action);
            return ScheduleJob(job, schedule);
        }
        
        /// <summary>
        /// Un-schedules specified job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public virtual Task<bool> UnScheduleJob(IJob job)
        {
            lock (jobsQueue)
            {
                int res = jobsQueue.RemoveWhere(jh => jh.Job.Equals(job));
                return Task.FromResult(res > 0);
            }
        } 

        /// <summary>
        /// Stops scheduler and all pending jobs
        /// </summary>
        /// <returns></returns>
        public async virtual Task Stop()
        {
            Task task = execTask;
            if (task == null || !running) return;

            Debug("Scheduler stopping ...");

            running = false;
            await task.ConfigureAwait(false);
            task.SafeDispose();
            execTask = null;

            Debug("Scheduler stopped");
        }

        /// <summary>
        /// Pauses scheduler until it gets resumed
        /// </summary>
        /// <returns></returns>
        public virtual Task Pause()
        {
            Task task = execTask;
            if (task == null || !running)
            {
                throw new InvalidOperationException("Cannot pause scheduler since it's not running");
            }

            lock (pauseLock)
            {
                paused = true;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Resumes scheduler after pause
        /// </summary>
        /// <returns></returns>
        public virtual Task Resume()
        {
            Task task = execTask;
            if (task == null || !running)
            {
                throw new InvalidOperationException("Cannot resume scheduler since it's not running");
            }

            lock (pauseLock)
            {
                paused = false;
                Monitor.Pulse(pauseLock);
                return Task.CompletedTask;
            }
        }
    }
}
