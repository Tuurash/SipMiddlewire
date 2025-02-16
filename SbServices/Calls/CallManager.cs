using Skylar.Services.SbServices.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Skylar.Services.SbServices.Calls
{
    public class CallManager : Call
    {
        private readonly Object locker = new object();

        public delegate void CallMediaStateHandler(object sender, CallMediaStateEventArgs e);

        public delegate void CallStateHandler(object sender, CallStateEventArgs e);

        public enum Type
        {
            UNKNOWN,
            OUTBOUND,
            INBOUND,
            MISSED,
            DECLINED
        }

        private readonly AccountManager account;
        private readonly Dictionary<int, CallManager> bridgedCalls = new Dictionary<int, CallManager>();
        private readonly Regex calleridregex = new Regex(@"(?:""(.*?)""\s\<)?sip\:(\d+)\@", RegexOptions.IgnoreCase);
        private Type callingType = Type.UNKNOWN;
        private sbsip_inv_state lastState = sbsip_inv_state.SBSIP_INV_STATE_NULL;

        private readonly DtmfDialerWorker _dtmfBackgroundWorke;

        internal CallManager(AccountManager account) : base(account)
        {
            this.account = account;
            _dtmfBackgroundWorke = new DtmfDialerWorker(this) { WorkerSupportsCancellation = true };
        }

        internal CallManager(AccountManager account, int id) : base(account, id)
        {
            this.account = account;
            _dtmfBackgroundWorke = new DtmfDialerWorker(this) { WorkerSupportsCancellation = true };
        }

        internal CallManager(AccountManager account, int id, Type type) : this(account, id)
        {
            CallType = type;
        }

        public int ID
        {
            get { lock (locker) { return getId(); } }
        }

        public sbsip_inv_state State
        {
            get
            {
                //lock (locker)
                {
                    if (lastState == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                        return lastState;
                    if (getInfo() == null)
                        return sbsip_inv_state.SBSIP_INV_STATE_NULL;
                    return getInfo().state;
                }
            }
        }

        public String CallingName
        {
            get
            {
                //lock (locker)
                {
                    if (getInfo() == null)
                        return "";
                    return calleridregex.Match(getInfo().remoteUri).Groups[1].Value;
                    //"\"BOWLNGGREN, OH\" <sip:4194192696@199.191.59.179>"
                }
            }
        }

        public String CallingNumber
        {
            get
            {
                //lock (locker)
                {
                    if (getInfo() == null)
                        return "";
                    return calleridregex.Match(getInfo().remoteUri).Groups[2].Value;
                    //"\"BOWLNGGREN, OH\" <sip:4194192696@199.191.59.179>"
                }
            }
        }

        public Boolean HasAudioOut
        {
            get
            {
                //lock (locker)
                {
                    if (getAudioMedia() == null) return false;
                    return
                        Endpoint.instance()
                            .audDevManager()
                            .getCaptureDevMedia()
                            .getPortInfo()
                            .listeners.Contains(getAudioMedia().getPortId());
                }
            }
        }

        public Boolean HasAudioIn
        {
            get
            {
                //lock (locker)
                {
                    if (getAudioMedia() == null) return false;
                    return
                        getAudioMedia()
                            .getPortInfo()
                            .listeners.Contains(Endpoint.instance().audDevManager().getCaptureDevMedia().getPortId());
                }
            }
        }

        public Boolean HasAudio
        {
            get
            {
                //lock (locker)
                {
                    return HasAudioIn && HasAudioOut;
                }
            }
        }

        public Dictionary<int, CallManager> BridgedCalls
        {
            get
            {
                //lock (locker)
                {
                    foreach (var x in bridgedCalls)//bridgedCalls.ForEach((x) =>
                    {
                        if (x.Value.LastState == sbsip_inv_state.SBSIP_INV_STATE_NULL ||
                            x.Value.LastState == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED ||
                            x.Value.State == sbsip_inv_state.SBSIP_INV_STATE_NULL ||
                            x.Value.State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                        {
                            bridgedCalls.Remove(x.Key);
                        }
                    };
                    return bridgedCalls;
                }
            }
        }

        public Boolean IsBridged
        {
            get
            {
                //lock (locker)
                {
                    var am = getAudioMedia();

                    if (am == null) return false;

                    return bridgedCalls.Count(c =>
                    {
                        var a = c.Value.getAudioMedia();
                        if (a == null) return false;
                        return a.getPortInfo().listeners.Contains(am.getPortId()) &&
                        am.getPortInfo().listeners.Contains(a.getPortId());
                    }) > 0;
                }
            }
        }

        public Boolean IsHeld
        {
            get
            {
                //lock (locker)
                {
                    var ci = getInfo();

                    if (ci == null)
                        return false;

                    var cmiv = ci.media;

                    foreach (var cmi in cmiv.Where(cmi => cmi.type == sbmedia_type.SBMEDIA_TYPE_AUDIO))
                        switch (cmi.status)
                        {
                            case sbsua_call_media_status.SBSUA_CALL_MEDIA_ACTIVE:
                                return false;
                            case sbsua_call_media_status.SBSUA_CALL_MEDIA_REMOTE_HOLD:
                            case sbsua_call_media_status.SBSUA_CALL_MEDIA_LOCAL_HOLD:
                                return true;
                        }
                    return false;
                }
            }
        }

        public Type CallType
        {
            get
            {
                //lock (locker)
                {
                    return callingType;
                }
            }
            private set
            {
                //lock (locker)
                {
                    callingType = value;
                }
            }
        }

        public sbsip_inv_state LastState
        {
            get
            {
                //lock (locker)
                {
                    if (lastState == sbsip_inv_state.SBSIP_INV_STATE_NULL)
                        lastState = State;
                    return lastState;
                }
            }
            private set
            {
                //lock (locker)
                {
                    lastState = value;
                }
            }
        }

        internal static CallManager makeCall(AccountManager account, string number)
        {
            return new CallManager(account).makeCall(number);
        }

        /*
         * TODO: needs cleaned up, possible duplicate code of getMedia 
         * 
         */

        public override void onCallMediaState(OnCallMediaStateParam prm)
        {
            base.onCallMediaState(prm);

            //lock (locker)
            {

                var ci = getInfo();

                if (ci == null)
                    return;

                var cmiv = ci.media;

                foreach (var cmi in cmiv.Where(cmi => cmi.type == sbmedia_type.SBMEDIA_TYPE_AUDIO))
                {
                    NotifyCallMediaState(cmi.status);

                    if (cmi.status != sbsua_call_media_status.SBSUA_CALL_MEDIA_ACTIVE ||
                        cmi.status != sbsua_call_media_status.SBSUA_CALL_MEDIA_REMOTE_HOLD) continue;

                    // connect ports
                    connectAudioDevice();
                }
            }
        }

        public event CallMediaStateHandler CallMediaStateChange;

        private void NotifyCallMediaState(sbsua_call_media_status state)
        {
            // Make sure someone is listening to event
            if (CallMediaStateChange == null) return;
            CallMediaStateChange(null, new CallMediaStateEventArgs(this, state));
        }

        public event CallStateHandler CallStateChange;

        private void NotifyCallState(sbsip_inv_state state)
        {
            // Make sure someone is listening to event
            if (CallStateChange == null) return;
            CallStateChange(null, new CallStateEventArgs(this, state));
        }

        public override void onCallState(OnCallStateParam prm)
        {
            base.onCallState(prm);
            //lock (locker)
            {
                var ci = getInfo();

                if (ci == null)
                    return;

                if (ci.state == sbsip_inv_state.SBSIP_INV_STATE_INCOMING ||
                    LastState == sbsip_inv_state.SBSIP_INV_STATE_INCOMING)
                    CallType = Type.INBOUND;
                if (ci.state == sbsip_inv_state.SBSIP_INV_STATE_EARLY || ci.state == sbsip_inv_state.SBSIP_INV_STATE_CALLING)
                    CallType = Type.OUTBOUND;

                NotifyCallState(ci.state);
                if (ci.state == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                {
                    if (LastState == sbsip_inv_state.SBSIP_INV_STATE_INCOMING)
                        CallType = Type.MISSED;
                    /* Delete the call */
                    Dispose();
                }
                LastState = ci.state;
            }
        }

        public CallManager stopAll()
        {
            //lock (locker)
            {
                var am = getAudioMedia();
                if (am == null) return this;

                // This will disconnect the sound device/mic to the call audio media
                Endpoint.instance().audDevManager().getCaptureDevMedia().stopTransmit(am);

                // And this will disconnect the call audio media to the sound device/speaker
                am.stopTransmit(Endpoint.instance().audDevManager().getPlaybackDevMedia());

                return this;
            }
        }

        public CallManager stopTransmit()
        {
            //lock (locker)
            {
                var am = getAudioMedia();
                if (am == null) return this;

                // This will disconnect the sound device/mic to the call audio media
                Endpoint.instance().audDevManager().getCaptureDevMedia().stopTransmit(am);

                return this;
            }
        }

        public CallManager stopRecieve()
        {
            //lock (locker)
            {
                var am = getAudioMedia();
                if (am == null) return this;

                // And this will disconnect the call audio media to the sound device/speaker
                am.stopTransmit(Endpoint.instance().audDevManager().getPlaybackDevMedia());

                return this;
            }
        }

        public CallManager startAll()
        {
            //lock (locker)
            {
                return connectAudioDevice();
            }
        }

        public CallManager startTransmit()
        {
            //lock (locker)
            {
                var am = getAudioMedia();
                if (am == null) return this;

                // This will disconnect the sound device/mic to the call audio media
                Endpoint.instance().audDevManager().getCaptureDevMedia().startTransmit(am);

                return this;
            }
        }

        public CallManager startRecieve()
        {
            //lock (locker)
            {
                var am = getAudioMedia();
                if (am == null) return this;

                // And this will disconnect the call audio media to the sound device/speaker
                am.startTransmit(Endpoint.instance().audDevManager().getPlaybackDevMedia());

                return this;
            }
        }

        public virtual CallManager answer()
        {
            //lock (locker)
            {
                if (State != sbsip_inv_state.SBSIP_INV_STATE_INCOMING) return this;
                var op = new CallOpParam(true);
                op.statusCode = sbsip_status_code.SBSIP_SC_OK;
                base.answer(op);
                connectAudioDevice();
                return this;
            }
        }

        public virtual void hangup()
        {
            //lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED || State == sbsip_inv_state.SBSIP_INV_STATE_NULL) return;

                var op = new CallOpParam(true);
                hangup(op);
            }
        }

        public virtual void decline()
        {
            //lock (locker)
            {
                if (State != sbsip_inv_state.SBSIP_INV_STATE_INCOMING) return;

                CallType = Type.DECLINED;

                var op = new CallOpParam(true) { statusCode = sbsip_status_code.SBSIP_SC_DECLINE };

                hangup(op);
            }
        }

        public CallManager hold()
        {
            lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_CONFIRMED && IsHeld == false)
                    setHold(new CallOpParam(true));
                return this;
            }
        }

        public CallManager retrieve()
        {
            lock (locker)
            {
                if (State != sbsip_inv_state.SBSIP_INV_STATE_CONFIRMED || IsHeld == false) return this;

                var op = new CallOpParam(true);
                if (op.opt != null) op.opt.flag |= (uint)sbsua_call_flag.SBSUA_CALL_UNHOLD;

                if (IsHeld)
                    reinvite(op);
                return this;
            }
        }

        private new void hangup(CallOpParam op)
        {
            //lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_NULL || State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED) return;

                base.hangup(op);
            }

            /* If we don't dispose of the call it will notify its diconnected state */
            // Manually notify of disconnect
            //NotifyCallState(sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED);

            // And delete the call
            //Dispose();
        }

        public String getSipAddress(string number)
        {
            //lock (locker)
            {
                var host = account.RegURI.Split(':')[1] + ":" + account.RegURI.Split(':')[2];
                //TODO: better way to get the host?
                return "sip:" + number + "@" + host;
            }
        }

        public virtual CallManager makeCall(string number)
        {
            //lock (locker)
            {
                base.makeCall(getSipAddress(number), new CallOpParam(true));
                return this;
            }
        }

        public CallManager safeBridge(CallManager call)
        {
            //lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_NULL || State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                    return this;

                if (call.State == sbsip_inv_state.SBSIP_INV_STATE_NULL || call.State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                    return this;

                if (!IsHeld && !call.IsHeld)
                {
                    bridge(call);
                }
                else
                {
                    var waitForCall = false;
                    var waitForThis = false;

                    if (IsHeld)
                    {
                        waitForThis = true;
                        CallMediaStateChange += (sender, e) =>
                        {
                            if (!waitForThis || waitForCall || call.IsHeld || IsHeld) return;
                            waitForThis = false;
                            bridge(call);
                        };
                        try
                        {
                            retrieve();
                        }
                        catch (ApplicationException ex)
                        {

                        }
                    }
                    if (call.IsHeld)
                    {
                        waitForCall = true;
                        call.CallMediaStateChange += (sender, e) =>
                        {
                            if (!waitForCall || waitForThis || call.IsHeld || IsHeld) return;
                            waitForCall = false;
                            call.bridge(this);
                        };
                        try
                        {
                            call.retrieve();
                        }
                        catch (ApplicationException ex)
                        {

                        }
                    }
                }

                return this;
            }
        }

        public CallManager bridge(CallManager call)
        {
            if (call == this) return this;

            lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_NULL || State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                    return this;

                if (call.State == sbsip_inv_state.SBSIP_INV_STATE_NULL || call.State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                    return this;

                if (!BridgedCalls.ContainsKey(call.ID))
                    BridgedCalls.Add(call.ID, call);

                if (!call.BridgedCalls.ContainsKey(ID))
                    call.BridgedCalls.Add(ID, this);

                var call1 = getAudioMedia();
                var call2 = call.getAudioMedia();

                if (call1 == null || call2 == null) return this;

                call.CallStateChange += Call_BridgedCallDisconnected;
                CallStateChange += Call_BridgedCallDisconnected;

                call1.startTransmit(call2);
                call2.startTransmit(call1);

                return this;
            }
        }

        private void Call_BridgedCallDisconnected(object sender, CallStateEventArgs e)
        {
            //lock (locker)
            {
                if ((LastState == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED || LastState == sbsip_inv_state.SBSIP_INV_STATE_NULL) == false ||
                    (State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED || State == sbsip_inv_state.SBSIP_INV_STATE_NULL) == false)
                    return;

                e.Call.CallStateChange -= Call_BridgedCallDisconnected;

                if (!BridgedCalls.ContainsKey(e.Call.ID))
                    BridgedCalls.Remove(e.Call.ID);

                if (LastState == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED || LastState == sbsip_inv_state.SBSIP_INV_STATE_NULL || State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED || State == sbsip_inv_state.SBSIP_INV_STATE_NULL)
                    return;

                if (!e.Call.BridgedCalls.ContainsKey(ID))
                    e.Call.BridgedCalls.Remove(ID);
            }
        }

        public CallManager unbridge(CallManager call)
        {
            if (call == this) return this;

            lock (locker)
            {

                if (!BridgedCalls.ContainsKey(call.ID))
                    BridgedCalls.Remove(call.ID);

                if (!call.BridgedCalls.ContainsKey(ID))
                    call.BridgedCalls.Remove(ID);

                var call1 = getAudioMedia();
                var call2 = call.getAudioMedia();

                if (call1 == null || call2 == null) return this;

                call1.stopTransmit(call2);
                call2.stopTransmit(call1);

                return this;
            }
        }

        public CallManager connectAudioDevice()
        {
            lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED || State == sbsip_inv_state.SBSIP_INV_STATE_NULL)
                    return this;

                var am = getAudioMedia();

                var captureDevMedia = Endpoint.instance().audDevManager().getCaptureDevMedia();

                if (am == null || captureDevMedia == null)
                {
                    //throw new Exception("invalid audio devices");
                }

                // This will connect the sound device/mic to the call audio media
                captureDevMedia.startTransmit(am);

                // And this will connect the call audio media to the sound device/speaker
                am.startTransmit(captureDevMedia);


                if (captureDevMedia.getPortInfo().listeners.Count(s => s == am.getPortId()) < 1)
                {
                    captureDevMedia.startTransmit(am);
                }

                if (am.getPortInfo().listeners.Count(s => s == captureDevMedia.getPortId()) < 1)
                {
                    am.startTransmit(captureDevMedia);
                }

                return this;
            }
        }

        public AudioMedia getAudioMedia()
        {
            lock (locker)
            {
                if (State == sbsip_inv_state.SBSIP_INV_STATE_NULL || State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                    return null;
                for (var i = 0; i < getInfo().media.Count; ++i)
                {
                    if (getInfo().media[i].type != sbmedia_type.SBMEDIA_TYPE_AUDIO)
                        continue;

                    return AudioMedia.typecastFromMedia(getMedia((uint)i));
                }
                return null;
            }
        }

        public String getCallerDisplay()
        {
            //lock (locker)
            {
                return CallingName.Length > 0 ? CallingName : CallingNumber;
            }
        }

        public String getCallDurationString()
        {
            //lock (locker)
            {
                var seconds = getInfo().totalDuration.sec;
                return string.Format("{0:00}:{1:00}:{2:00}", seconds / 3600, (seconds / 60) % 60, seconds % 60);
            }
        }

        public void transfer(String number)
        {
            //lock (locker)
            {
                xfer(getSipAddress(number), new CallOpParam(true));
            }
        }

        public void transferAttended(String number)
        {
            //lock (locker)
            {
                transferAttended(makeCall(account, number));
            }
        }

        public void transferAttended(CallManager call)
        {
            //lock (locker)
            {
                xferReplaces(call, new CallOpParam(true));
            }
        }

        public new CallInfo getInfo()
        {
            //lock (locker)
            {
                return base.getInfo();
            }
        }

        public void dialDtmf(string digits, bool queue = true)
        {
            //lock (locker)
            {
                if (queue == false)
                {
                    if (State == sbsip_inv_state.SBSIP_INV_STATE_CONFIRMED)
                    {
                        base.dialDtmf(digits);
                    }
                    return;
                }

                _dtmfBackgroundWorke.DtmfQueue.Enqueue(digits);

                if (_dtmfBackgroundWorke.IsBusy) return;
                _dtmfBackgroundWorke.RunWorkerAsync();
            }
        }

        public new int getId()
        {
            //lock (locker)
            {
                try
                {
                    return base.getId();
                }
                catch (AccessViolationException ex)
                {
                    return -1;
                }
            }
        }
    }
}
