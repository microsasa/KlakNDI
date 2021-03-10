using Klak.Ndi.Interop;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Klak.Ndi
{
    // Todo: consider grabbing video and audio frames on the same thread instead of using the Unity's update loop for
    // video and a separate thread for audio.
    // See: https://dev.azure.com/msresearch/Malta/_workitems/edit/138412

    //[ExecuteInEditMode]
    public sealed partial class NdiReceiver : MonoBehaviour
    {
        #region Internal objects

        Recv _recv;
        FormatConverter _converter;
        MaterialPropertyBlock _override;

        bool _audioRunning = false;
        SynchronizationContext _mainThreadContext;
        CancellationTokenSource _tokenSource;
        CancellationToken _cancellationToken;

        void PrepareInternalObjects()
        {
            if (_recv == null) _recv = RecvHelper.TryCreateRecv(_ndiName);
            if (_converter == null) _converter = new FormatConverter(_resources);
            if (_override == null) _override = new MaterialPropertyBlock();
        }

        void ReleaseInternalObjects()
        {
            if (_audioRunning)
            {
                if (_audioRenderer != null)
                {
                    _audioRenderer.StopPlaying();
                }

                _tokenSource.Cancel();
                while (_audioRunning)
                {
                    Thread.Sleep(5);
                }
            }

            _recv?.Dispose();
            _recv = null;

            _converter?.Dispose();
            _converter = null;
        }

        #endregion

        #region Video receiver implementation

        void ProcessVideoFrame()
        {
            var rt = TryReceiveVideoFrame();
            if (rt == null) return;

            // Material property override
            if (_targetRenderer != null)
            {
                _targetRenderer.GetPropertyBlock(_override);
                _override.SetTexture(_targetMaterialProperty, rt);
                _targetRenderer.SetPropertyBlock(_override);
            }

            // External texture update
            if (_targetTexture != null)
                Graphics.Blit(rt, _targetTexture);
        }

        RenderTexture TryReceiveVideoFrame()
        {
            // Try getting a video frame.
            var frameOrNull = RecvHelper.TryCaptureVideoFrame(_recv);
            if (frameOrNull == null) return null;
            var frame = (Interop.VideoFrame)frameOrNull;

            // Pixel format conversion
            var rt = _converter.Decode
              (frame.Width, frame.Height,
               Util.CheckAlpha(frame.FourCC), frame.Data);

            // Copy the metadata if any.
            if (frame.Metadata != System.IntPtr.Zero)
                metadata = Marshal.PtrToStringAnsi(frame.Metadata);
            else
                metadata = null;

            // Free the frame up.
            _recv.FreeVideoFrame(frame);

            return rt;
        }

        #endregion

        #region Audio receiver implementation

        struct AudioBuffer
        {
            public float[] Buffer;
            public int NumChannels;
            public int SampleRate;
            public int NumSamples;
        }

        AudioBuffer audioBuffer;

        void ProcessAudioFrames()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    Interop.AudioFrame audio = new Interop.AudioFrame();

                    var type = _recv.CaptureAudio(IntPtr.Zero, ref audio, IntPtr.Zero, 0);
                    if (type == Interop.FrameType.Audio)
                    {
                        _mainThreadContext.Post(ProcessAudioFrame, audio);
                        _recv.FreeAudioFrame(ref audio);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _audioRunning = false;
            }
        }

        void ProcessAudioFrame(System.Object data)
        {
            if (_audioRenderer == null)
            {
                return;
            }

            AudioFrame audioFrame = (AudioFrame)data;
            if (audioBuffer.Buffer == null ||
                audioBuffer.SampleRate != audioFrame.SampleRate ||
                audioBuffer.NumChannels != audioFrame.NumChannels ||
                audioBuffer.NumSamples != audioFrame.NumSamples)
            {
                audioBuffer.SampleRate = audioFrame.SampleRate;
                audioBuffer.NumChannels = audioFrame.NumChannels;
                audioBuffer.NumSamples = audioFrame.NumSamples;
                audioBuffer.Buffer = new float[audioBuffer.NumSamples * audioBuffer.NumChannels];

                _audioRenderer.SetFormat(audioFrame);
            }

            unsafe
            {
                int sizeInBytes = audioFrame.NumSamples * audioFrame.NumChannels * sizeof(float);
                using (var nativeArray = new NativeArray<byte>(
                    sizeInBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
                {
                    AudioFrameInterleaved interleavedAudio = new AudioFrameInterleaved()
                    {
                        SampleRate = audioFrame.SampleRate,
                        NumChannels = audioFrame.NumChannels,
                        NumSamples = audioFrame.NumSamples,
                        Timecode = audioFrame.Timecode
                    };

                    interleavedAudio.Data = (IntPtr)nativeArray.GetUnsafePtr();

                    // Convert from float planar to float interleaved audio
                    _recv.AudioFrameToInterleaved_32f_v2(ref audioFrame, ref interleavedAudio);

                    var totalSamples = interleavedAudio.NumSamples * interleavedAudio.NumChannels;
                    void* audioDataPtr = interleavedAudio.Data.ToPointer();

                    for (int i = 0; i < totalSamples; i++)
                    {
                        audioBuffer.Buffer[i] = UnsafeUtility.ReadArrayElement<float>(audioDataPtr, i);
                    }
                }
            }

            _audioRenderer.PlaySamples(audioBuffer.Buffer);
        }

#endregion

        #region Component state controller

        internal void Restart() => ReleaseInternalObjects();

        #endregion

        #region MonoBehaviour implementation

        void OnDisable() => ReleaseInternalObjects();

        void Update()
        {
            PrepareInternalObjects();

            if (_recv != null)
            {
                ProcessVideoFrame();

                if (!_audioRunning)
                {
                    _mainThreadContext = SynchronizationContext.Current;

                    _tokenSource = new CancellationTokenSource();
                    _cancellationToken = _tokenSource.Token;

                    _audioRunning = true;
                    Task.Run(ProcessAudioFrames);
                }
            }
        }

         void OnDestroy() => ReleaseInternalObjects();

        #endregion
    }

}
