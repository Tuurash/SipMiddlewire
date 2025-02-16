using FunCoding.CoreMessenger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skylar.Services
{
    #region User Related

    public class UserUpdateStatus : Message
    {
        public bool UserUpdated { get; private set; }
        public UserUpdateStatus(object sender, bool content) : base(sender) => UserUpdated = content;
    }
    public class UserAdded : Message
    {
        public bool UserUpdated { get; private set; }
        public UserAdded(object sender, bool content) : base(sender) => UserUpdated = content;
    }

    #endregion

    public class CallEnded : Message
    {
        public bool isCallEnded { get; private set; }
        public CallEnded(object sender, bool content) : base(sender) => isCallEnded = content;
    }

    public class CallInitiate : Message
    {
        public string DialedNum { get; private set; }
        public CallInitiate(object sender, string content) : base(sender) => DialedNum = content;
    }
}
