using Assets.Scripts;
using Klak.Ndi;
using Klak.Ndi.Interop;
using UnityEngine;

namespace Assets.Malta
{
    public class AudioSourceNdiAudioRenderer : NdiAudioRenderer
    {
        public AudioSource Renderer;
        private AudioClip _audioClip;
        private CircularBuffer<float> _audioBuffer;
        private object _audioBufferLock = new object();

        public override void SetFormat(AudioFrame audioFrame)
        {
            lock (_audioBufferLock)
            {
                if (_audioBuffer == null)
                {
                    _audioBuffer = new CircularBuffer<float>(audioFrame.NumSamples * audioFrame.NumChannels);
                }

                _audioClip = AudioClip.Create(
                    "NdiReceiver", audioFrame.SampleRate, audioFrame.NumChannels, audioFrame.SampleRate, true);

                AudioSettings.outputSampleRate = audioFrame.SampleRate;

                // Note: Unity AudioSource does not have a perfectly smooth transition when the number of channels is
                // changing dynamically. There can be a short audio artifact at the start of an audio clip with a 
                // number of channels different than the clip before.
                // Note: If the above artifact is an issue, a 'hack' to solve it is to keep the number of channels
                // fixed in the audio settings and manually adjust incoming audio samples to match that number of
                // channels.
                switch (audioFrame.NumChannels)
                {
                    case 1:
                        {
                            AudioSettings.speakerMode = AudioSpeakerMode.Mono;
                            break;
                        }
                    case 2:
                        {
                            AudioSettings.speakerMode = AudioSpeakerMode.Stereo;
                            break;
                        }
                    default:
                        {
                            Debug.LogWarning(
                                $"Unsupported number of audio channels: {audioFrame.NumChannels}. There may be audio artifacts.");
                            break;
                        }
                }
            }
        }

        public override void PlaySamples(float[] samples)
        {
            lock (_audioBufferLock)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _audioBuffer.Write(samples[i]);
                }
            }

            SetupAudioPlayback();
        }

        public override void StopPlaying()
        {
            TearDownAudioPlayback();
        }

        private void SetupAudioPlayback()
        {
            if (Renderer.isPlaying == false)
            {
                Renderer.loop = true;
                Renderer.clip = _audioClip;
                Renderer.Play();
            }
        }

        private void TearDownAudioPlayback()
        {
            if (Renderer.isPlaying)
            {
                Renderer.Stop();
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_audioBufferLock)
            {
                int length = data.Length;

                for (int i = 0; i < length; i++)
                {
                    if (_audioBuffer == null || _audioBuffer.IsEmpty())
                    {
                        data[i] = 0.0f;
                    }
                    else
                    {
                        data[i] = _audioBuffer.Read();
                    }
                }
            }
        }
    }
}
