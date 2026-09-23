namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Serializes install changes across Visual Studio instances. Acquire and release on the same thread.</summary>
public static class InstallLock
{
    public const string MutexName = @"Local\TheKameleon.Superpowers.Install";
    private static readonly Mutex SharedMutex = new Mutex(initiallyOwned: false, MutexName);

    public static IDisposable Acquire(TimeSpan timeout)
    {
        try
        {
            if (!SharedMutex.WaitOne(timeout))
            {
                throw new TimeoutException("Another Visual Studio window is changing the Superpowers installation. Try again in a moment.");
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous owner exited without releasing; this thread now owns the mutex.
        }

        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose()
        {
            SharedMutex.ReleaseMutex();
        }
    }
}
