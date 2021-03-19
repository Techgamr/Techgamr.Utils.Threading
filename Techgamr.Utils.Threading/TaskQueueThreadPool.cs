using System;
using System.Threading;

namespace Techgamr.Utils.Threading
{
    public class TaskQueueThreadPool
    {
        protected int AddTaskLastUsed;
        protected TaskQueueThread[] Threads;
        protected readonly bool Background;
        protected volatile bool Started;
        public readonly string PoolName;
        protected volatile bool Rebuilding;
        protected volatile object RebuildLock = new();

        public TaskQueueThreadPool(int initialSize, string poolName, bool background = true)
        {
            Background = background;
            PoolName = poolName;
            // Allocate empty array
            Threads = new TaskQueueThread[initialSize];
            // ReSharper disable once VirtualMemberCallInConstructor
            PopulateArray();
        }
        
        protected virtual void PopulateArray()
        {
            for (var i = 0; i < Threads.Length; i++) Threads[i] = CreateQueueThread(i);
            Started = false;
        }

        protected virtual TaskQueueThread CreateQueueThread(int index) => new(GetThreadName(index), Background);

        protected virtual string GetThreadName(int index) => $"{PoolName} #{index}";

        public virtual void Expand(int by)
        {
            CheckAndThrowIfNotStarted();
            var oldLen = Threads.Length;
            var newLen = oldLen + by;
            if (Threads.Length >= newLen)
                throw new InvalidOperationException(
                    $"Array length is {Threads.Length} but tried to Expand() to {newLen}. Use Rebuild(int) instead.");
            Array.Resize(ref Threads, newLen);
            for (var i = oldLen; i < newLen; i++)
            {
                Threads[i] = CreateQueueThread(i);
                if (Started) Threads[i].Start();
            }
        }

        public virtual void Resize(int newSize)
        {
            var currentSize = Threads.Length;
            if (currentSize == newSize) return;
            if (currentSize < newSize) Expand(Threads.Length - newSize);
            else Rebuild(newSize);
        }

        public virtual void Rebuild() => Rebuild(Threads.Length);

        public virtual void Rebuild(int newSize)
        {
            CheckAndThrowIfNotStarted();
            lock (RebuildLock) Rebuilding = true;
            StopSync();
            Threads = new TaskQueueThread[newSize];
            PopulateArray();
            Start();
            lock (RebuildLock)
            {
                Rebuilding = false;
                Monitor.PulseAll(RebuildLock);
            }
        }

        protected virtual void CheckAndWaitForRebuilding()
        {
            lock (RebuildLock)
            {
                if (Rebuilding) Monitor.Wait(RebuildLock);
            }
        }

        protected virtual void CheckAndThrowIfNotStarted()
        {
            if (!Started) throw new InvalidOperationException("Pool not started");
        }

        public virtual void Start()
        {
            if (Started) throw new InvalidOperationException("Pool already started");
            foreach (var thread in Threads) thread.Start();
            Started = true;
        }

        public virtual void StopAsync()
        {
            CheckAndThrowIfNotStarted();
            foreach (var thread in Threads) thread.StopAsync();
        }

        public virtual void StopSync()
        {
            CheckAndThrowIfNotStarted();
            foreach (var thread in Threads) thread.StopSync();
        }

        public virtual bool AddTask(ThreadStart task)
        {
            CheckAndThrowIfNotStarted();
            CheckAndWaitForRebuilding();

            // Find the first idle thread
            for (var i = 0; i < Threads.Length; i++)
            {
                var thread = Threads[i];
                if (thread.IsWorking || i == AddTaskLastUsed) continue;
                thread.AddTask(task);
                AddTaskLastUsed = i;
                // Console.WriteLine($"Add task last used: {_addTaskLastUsed}, i: {i}, threadName: {thread.ThreadName}, len: {_threads.Length}");
                return true;
            }

            // If no threads are idle, find the one with the least tasks
            var oneWithLeastJobs = -1;
            var c = -1;
            for (var i = 0; i < Threads.Length; i++)
            {
                var t = Threads[i];
                if (c == -1 || i == AddTaskLastUsed || c >= t.InQueue)
                {
                    c = t.InQueue;
                    oneWithLeastJobs = i;
                }
            }

            // Check for failure
            if (oneWithLeastJobs == -1) return false;
            
            // Add task to the thread
            Threads[oneWithLeastJobs].AddTask(task);
            AddTaskLastUsed = oneWithLeastJobs;
            return true;
        }
    }
}
