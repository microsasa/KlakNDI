using Klak.Ndi.Interop;
using UnityEngine;

namespace Klak.Ndi
{
    public abstract class NdiAudioRenderer : MonoBehaviour
    {
        /// <summary>
        /// Sets up the audio format.
        /// </summary>
        /// <param name="audioFrame">The audio frame to use for setting up format.</param>
        public abstract void SetFormat(AudioFrame audioFrame);

        /// <summary>
        /// Plays samples.
        /// </summary>
        /// <param name="samples">The samples to play.</param>
        /// as "all locations".</param>
        public abstract void PlaySamples(float[] samples);

        /// <summary>
        /// Stops playback.
        /// </summary>
        public abstract void StopPlaying();
    }
}
