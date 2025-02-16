using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skylar.Services.SbServices.Calls
{
    public class IncomingCallEventArgs : EventArgs
    {
        public IncomingCallEventArgs(Call call)
        {
            Call = call;
        }

        public Call Call { get; private set; }
    }
}
