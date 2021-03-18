using System;

namespace Techgamr.Utils.Threading
{
    public class ThreadTaskException : Exception
    {
        public ThreadTaskException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }

    public class ThreadCrashException : Exception
    {
        public ThreadCrashException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
