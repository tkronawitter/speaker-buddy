using System.Runtime.ExceptionServices;

namespace SpeakerBuddy.App.Tests;

// WinForms objects belong to a single-threaded apartment, as on the tray's UI thread
internal static class Sta
{
    public static void Run(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
