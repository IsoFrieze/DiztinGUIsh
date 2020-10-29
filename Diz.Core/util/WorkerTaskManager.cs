using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Diz.Core.util
{
    public class WorkerTaskManager
    {
        private readonly List<Task> tasks = new List<Task>();
        private readonly object taskLock = new object();

        private readonly ManualResetEvent notFinishing = new ManualResetEvent(false);
        private volatile bool finished = false;
        private Timer timer;

        public WorkerTaskManager()
        {
            var oneSecond = TimeSpan.FromSeconds(1);
            timer = new Timer(state => Update(), null, oneSecond, oneSecond);
        }

        public void StartFinishing()
        {
            notFinishing.Set();
        }

        public void Update()
        {
            if (finished)
            {
                timer.Change(-1, -1);
                timer.Dispose();
                timer = null;
            }

            lock (tasks)
                tasks.RemoveAll(IsCompleted);
        }

        public Task Run(Action action)
        {
            var task = new Task(action);

            lock (taskLock)
                tasks.Add(task);
            
            task.Start();

            return task;
        }

        private static bool IsCompleted(Task task) => task.IsCompleted;

        // task-oriented version of below
        /*public Task RunTask_WaitForAllTasksToComplete()
        {
            return Task.Run(WaitForAllTasksToComplete);
        }*/

        // blocking method
        public void WaitForAllTasksToComplete()
        {
            // wait until we've been told we should be finishing
            notFinishing.WaitOne();

            while (true)
            {
                List<Task> tasksCopy;
                lock (taskLock)
                {
                    if (tasks.Count == 0)
                        break;

                    tasksCopy = new List<Task>(tasks);
                }

                // anything in our original list, let it run to completion
                Task.WaitAll(tasksCopy.ToArray());
            }

            finished = true;
        }
    }
}