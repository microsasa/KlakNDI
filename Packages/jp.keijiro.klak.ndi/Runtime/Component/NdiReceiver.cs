using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Klak.Ndi
{

    //[ExecuteInEditMode]
    public sealed partial class NdiReceiver : MonoBehaviour
    {
        #region Internal objects

        Interop.Recv _recv;
        FormatConverter _converter;
        MaterialPropertyBlock _override;

        CancellationTokenSource _tokenSource;
        CancellationToken _cancellationToken;

        void PrepareNdiReceiver()
        {
            lock (this)
            {
                if (_recv == null) _recv = RecvHelper.TryCreateRecv(_ndiName);
            }
        }

        void PrepareOtherInternalObjects()
        {
            if (_converter == null) _converter = new FormatConverter(_resources);
            if (_override == null) _override = new MaterialPropertyBlock();
        }

        void ReleaseInternalObjects()
        {
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
            PrepareNdiReceiver();

            // Do nothing if the recv object is not ready.
            if (_recv == null) return null;

            PrepareOtherInternalObjects();

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

#if DEBUG_AUDIO
        int _audioFrameCount = 0;
#endif
        void ProcessAudioFrames()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    PrepareNdiReceiver();

                    if (_recv == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    Interop.AudioFrame audio = new Interop.AudioFrame();

                    var type = _recv.CaptureAudio(IntPtr.Zero, ref audio, IntPtr.Zero, 0);
                    if (type == Interop.FrameType.Audio)
                    {
#if DEBUG_AUDIO
                        _audioFrameCount += audio.NumSamples;
                        if (_audioFrameCount >= audio.SampleRate)
                        {
                            _audioFrameCount %= audio.SampleRate;
                            Debug.Log($"Audio frame count = {_audioFrameCount}");
                        }
#endif

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
                ReleaseInternalObjects();
            }
        }

#endregion

#region Component state controller

        internal void Restart() => ReleaseInternalObjects();

#endregion

#region MonoBehaviour implementation

        void OnDisable() => ReleaseInternalObjects();

        private void Awake()
        {
            _tokenSource = new CancellationTokenSource();
            _cancellationToken = _tokenSource.Token;

            Task.Run(ProcessAudioFrames);
        }

        void Update()
        {
            ProcessVideoFrame();
        }

        private void OnDestroy()
        {
            _tokenSource?.Cancel();
        }

#endregion
    }

}
