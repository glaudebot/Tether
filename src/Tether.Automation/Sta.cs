using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Tether.Automation;

/// <summary>Runs work on a dedicated STA thread (clipboard and SendKeys need one).</summary>
public static class Sta
{
    public static T Run<T>(Func<T> f)
    {
        T result = default!;
        ExceptionDispatchInfo? error = null;
        var t = new Thread(() =>
        {
            try { result = f(); }
            catch (Exception e) { error = ExceptionDispatchInfo.Capture(e); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error?.Throw();
        return result;
    }

    public static void Run(Action a) => Run(() => { a(); return 0; });
}
