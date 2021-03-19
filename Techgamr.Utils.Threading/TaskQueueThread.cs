using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Techgamr.Utils.Threading
{
    public class TaskQueueThread
    {
        // Fields
        protected readonly Thread Thread;
        protected readonly ConcurrentQueue<ThreadStart> Tasks = new();
        protected readonly object MainLock = new();
        private volatile bool _running;
        private volatile bool _isWorking;

        // API
        public bool Running
        {
            get => _running;
            protected set => _running = value;
        }
        public bool IsWorking
        {
            get => _isWorking;
            protected set => _isWorking = value;
        }
        public string? ThreadName => Thread.Name;
        public int InQueue => Tasks.Count;

        public event ThreadCrashEvent? ThreadCrash;
        public event TaskCrashEvent? TaskCrash;

        public static event ThreadCreateEvent? OnThreadCreate;

        /**
         * Mainly for testing but could be used in production
         */
        public event SuccessfulThreadExitEvent? SuccessfulThreadExit;

        public TaskQueueThread(string? threadName = null, bool background = false)
        {
            Thread = new Thread(Run) {IsBackground = background};
            
            try
            {
                Thread.Name = threadName;
            }
            // ignore, it's not the end of the world if the thread name could not be set
            catch (InvalidOperationException) {}
            
            OnThreadCreate?.Invoke(this);
        }

        protected virtual bool ExecTasks()
        {
            if (Tasks.IsEmpty) return false;
            Tasks.TryDequeue(out var threadStart);
            if (threadStart == null) return false;
            try
            {
                threadStart();
                return true;
            }
            catch (Exception e)
            {
                throw new ThreadTaskException($"Exception in {nameof(TaskQueueThread)} task", e);
            }
        }

        public virtual void StopAsync()
        {
            Running = false;
            lock (MainLock)
            {
                Monitor.PulseAll(MainLock);
            }
        }

        public virtual void StopSync()
        {
            StopAsync();
            Join();
        }

        protected virtual void Run()
        {
            try
            {
                Running = true;

                // Complete all tasks before an exit.
                while (Running || !Tasks.IsEmpty)
                {
                    IsWorking = true;
                    bool res;
                    try
                    {
                        res = ExecTasks();
                    }
                    catch (ThreadTaskException e)
                    {
                        try
                        {
                            TaskCrash?.Invoke(e);
                        }
                        catch (Exception)
                        {
                            // If the crash handler crashed, just ignore
                        }
                        continue;
                    }

                    if (res) continue;
                    lock (MainLock)
                    {
                        IsWorking = false;
                        Monitor.Wait(MainLock);
                    }
                }
            }
            catch (Exception e)
            {
                var exitWithException = true;
                ThreadCrash?.Invoke(e, Thread.CurrentThread.Name, ref exitWithException);
                if (exitWithException) throw;
                return;
            }
            try
            {
                SuccessfulThreadExit?.Invoke();
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public virtual void Start() => Thread.Start();

        public virtual void AddTask(ThreadStart task)
        {
            Tasks.Enqueue(task);
            lock (MainLock)
            {
                Monitor.PulseAll(MainLock);
            }
        }

        public virtual void Join() => Thread.Join();
    }
}
