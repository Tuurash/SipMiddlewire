using NAudio.Wave;

namespace Skylar.Services
{
    public class RingtoneServices
    {
        WaveFileReader waveReader;
        WaveOut output;

        public void StartRinging()
        {
            int waveOutDevices = WaveOut.DeviceCount;
            for (int i = 0; i < waveOutDevices; i++)
                playSound(i);
        }

        public void playSound(int deviceNumber)
        {
            disposeWave();// stop previous sounds before starting
            waveReader = new NAudio.Wave.WaveFileReader(Skylar.Properties.Resources.RingtoneClassic);
            var waveOut = new NAudio.Wave.WaveOut();
            waveOut.DeviceNumber = deviceNumber;
            output = waveOut;
            output.Init(waveReader);
            output.Play();
        }

        public void disposeWave()
        {
            if (output != null)
            {
                if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                {
                    output.Stop();
                    output.Dispose();
                    output = null;
                }
            }
        }
    }
}
