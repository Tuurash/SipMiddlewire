using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skylar.Services.SbServices.Calls
{
    public class CallMediaStateEventArgs : EventArgs
    {
        public CallMediaStateEventArgs(CallManager call, sbsua_call_media_status state)
        {
            Call = call;
            State = state;
        }

        public CallManager Call { get; private set; }
        public sbsua_call_media_status State { get; private set; }
    }
}
