using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skylar.Services.SbServices.Calls
{
    public class CallStateEventArgs : EventArgs
    {
        public CallStateEventArgs(CallManager call, sbsip_inv_state state)
        {
            Call = call;
            State = state;
        }

        public CallManager Call { get; private set; }
        public sbsip_inv_state State { get; private set; }
    }
}
