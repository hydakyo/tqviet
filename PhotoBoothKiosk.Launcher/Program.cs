using System.ServiceProcess;

namespace PhotoBoothKiosk.Launcher
{
    static class Program
    {
        static void Main()
        {
            ServiceBase.Run(new KioskWatcherService());
        }
    }
}