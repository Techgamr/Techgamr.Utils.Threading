using System;
using System.Collections.Concurrent;
using System.Threading;
using NUnit.Framework;

namespace Techgamr.Utils.Threading.Test
{
    [TestFixture]
    public class TaskQueueThreadTest
    {
        private static TaskQueueThread GetThread() => new("Test thread");

        [Test]
        public void Naming()
        {
            var lck = new object();
            var t = GetThread();

            t.AddTask(delegate { t.ScheduleStop(); });
            t.SuccessfulThreadExit += delegate
            {
                lock (lck)
                {
                    Monitor.PulseAll(lck);
                }
            };
            t.Start();

            lock (lck)
            {
                Monitor.Wait(lck);
            }
            
            Assert.IsTrue(t.ThreadName == "Test thread");
            Assert.IsTrue(t.Running == false);
        }

        [Test]
        public void RunsTask()
        {
            var lck = new object();
            var q = new ConcurrentQueue<int>();
            var t = GetThread();

            void Task()
            {
                q.Enqueue(1);
            }
            
            t.AddTask(Task);
            t.AddTask(delegate { t.ScheduleStop(); });
            t.SuccessfulThreadExit += delegate
            {
                lock (lck)
                {
                    Monitor.PulseAll(lck);
                }
            };
            t.Start();

            lock (lck)
            {
                Monitor.Wait(lck);
            }

            q.TryDequeue(out var o);
            Assert.IsTrue(o == 1);
        }
        
        [Test]
        public void Ordering()
        {
            var lck = new object();
            var q = new ConcurrentQueue<int>();
            var t = GetThread();

            void One()
            {
                q.Enqueue(1);
            }

            void Two()
            {
                q.Enqueue(2);
            }
            
            void Three()
            {
                q.Enqueue(3);
            }
            
            t.AddTask(One);
            t.AddTask(Two);
            t.AddTask(Three);
            t.AddTask(delegate { t.ScheduleStop(); });
            t.SuccessfulThreadExit += delegate
            {
                lock (lck)
                {
                    Monitor.PulseAll(lck);
                }
            };
            t.Start();

            lock (lck)
            {
                Monitor.Wait(lck);
            }

            for (var i = 1; i <= 3; i++)
            {
                q.TryDequeue(out var o);
                Assert.IsTrue(o == i);
            }
        }

        [Test]
        public void TaskExceptionHandling()
        {
            var lck = new object();
            var q = new ConcurrentQueue<string>();
            var t = GetThread();

            t.AddTask(() => throw new Exception("Test"));
            t.AddTask(() => t.ScheduleStop());
            t.SuccessfulThreadExit += () =>
            {
                lock (lck)
                {
                    Monitor.PulseAll(lck);
                }
            };
            t.TaskCrash += exception =>
            {
                q.Enqueue(exception.InnerException?.Message);
            };
            t.Start();

            lock (lck)
            {
                Monitor.Wait(lck);
            }
            
            q.TryDequeue(out var o);
            Assert.IsTrue(o == "Test");
        }
        
        [Test]
        public void TaskCrashHandlerCrashHandler()
        {
            var lck = new object();
            var q = new ConcurrentQueue<string>();
            var t = GetThread();

            t.AddTask(() => throw new Exception("Test"));
            t.AddTask(() => t.ScheduleStop());
            t.SuccessfulThreadExit += () =>
            {
                lock (lck)
                {
                    Monitor.PulseAll(lck);
                }
            };
            t.TaskCrash += exception =>
            {
                q.Enqueue(exception.InnerException?.Message);
                throw new Exception("Task Crash Test");
            };
            t.Start();

            lock (lck)
            {
                Monitor.Wait(lck);
            }
            
            q.TryDequeue(out var o);
            Assert.IsTrue(o == "Test");
        }
        
        [Test]
        public void SuccessfulThreadExitHandler()
        {
            var lck = new object();
            var q = new ConcurrentQueue<int>();
            var t = GetThread();

            t.AddTask(() => t.ScheduleStop());
            t.SuccessfulThreadExit += () =>
            {
                lock (lck)
                {
                    Monitor.PulseAll(lck);
                }
                
                q.Enqueue(1);

                throw new Exception("Test");
            };
            t.Start();

            lock (lck)
            {
                Monitor.Wait(lck);
            }
            
            q.TryDequeue(out var o);
            Assert.IsTrue(o == 1);
        }
    }
}
