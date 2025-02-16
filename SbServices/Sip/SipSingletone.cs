using System.Windows;

namespace Skylar.Services.SbServices.Sip
{
    sealed class SipSingletone
    {
        private static SipSingletone instance;
        private static readonly object InstanceLock = new object();

        SipManager sipManager;

        private SipSingletone()
        {
            sipManager = new SipManager(Application.Current.Dispatcher.Thread);
        }

        public static SipSingletone GetSipInstance
        {
            get
            {
                lock (InstanceLock)
                {
                    if (instance == null)
                        instance = new SipSingletone();
                    return instance;
                }
            }
        }

        public SipManager SbSip
        {
            get => sipManager;
        }
    }
}
