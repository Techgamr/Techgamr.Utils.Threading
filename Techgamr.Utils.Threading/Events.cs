using System;

namespace Techgamr.Utils.Threading
{
    public delegate void ThreadCrashEvent(Exception e, string? threadName, ref bool exitWithException);
    public delegate void TaskCrashEvent(ThreadTaskException e);
    public delegate void SuccessfulThreadExitEvent();
}
