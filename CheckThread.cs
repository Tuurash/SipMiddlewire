using System.Runtime.CompilerServices;
using System.Threading;

namespace Skylar.Services
{
    public static class CheckThread
    {
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static bool TryRegThread()
        {
            Endpoint ep = new Endpoint();
            try
            {
                if (ep != null && !ep.libIsThreadRegistered())
                { ep.libRegisterThread(Thread.CurrentThread.Name); return true; }
                else
                    return false;
            }
            catch
            { return false; }
        }
    }
}
