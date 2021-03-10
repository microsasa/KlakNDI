using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Klak.Ndi.Interop
{

    public enum Bandwidth
    {
        MetadataOnly = -10,
        AudioOnly = 10,
        Lowest = 0,
        Highest = 100
    }

    public enum ColorFormat
    {
        BGRX_BGRA = 0,
        UYVY_BGRA = 1,
        RGBX_RGBA = 2,
        UYVY_RGBA = 3,
        BGRX_BGRA_Flipped = 200,
        Fastest = 100
    }

    public class Recv : SafeHandleZeroOrMinusOneIsInvalid
    {
        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct Settings
        {
            public Source Source;
            public ColorFormat ColorFormat;
            public Bandwidth Bandwidth;
            [MarshalAsAttribute(UnmanagedType.U1)]
            public bool AllowVideoFields;
            public IntPtr Name;
        }

        #region SafeHandle implementation

        Recv() : base(true) { }

        protected override bool ReleaseHandle()
        {
            _Destroy(handle);
            return true;
        }

        #endregion

        #region Public methods

        public static Recv Create(in Settings settings)
          => _Create(settings);

        public FrameType CaptureVideo
          (out VideoFrame video, IntPtr audio, IntPtr metadata, uint timeout)
          => _CaptureVideo(this, out video, audio, metadata, timeout);

        public void FreeVideoFrame(in VideoFrame frame)
          => _FreeVideo(this, frame);

        public FrameType CaptureAudio
          (IntPtr video, ref AudioFrame audio, IntPtr metadata, uint timeout)
          => _CaptureAudio(this, video, ref audio, metadata, timeout);

        public void AudioFrameToInterleaved_32f_v2
          (ref AudioFrame source, ref AudioFrameInterleaved dest)
          => _AudioFrameToInterleaved_32f_v2(ref source, ref dest);

        public void FreeAudioFrame(ref AudioFrame frame)
          => _FreeAudio(this, ref frame);

        public bool SetTally(in Tally tally)
          => _SetTally(this, tally);

        #endregion

        #region Unmanaged interface

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_create_v3")]
        static extern Recv _Create(in Settings Settings);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_destroy")]
        static extern void _Destroy(IntPtr recv);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_capture_v2")]
        static extern FrameType _CaptureVideo(Recv recv,
          out VideoFrame video, IntPtr audio, IntPtr metadata, uint timeout);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_free_video_v2")]
        static extern void _FreeVideo(Recv recv, in VideoFrame data);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_capture_v2")]
        static extern FrameType _CaptureAudio(Recv recv,
          IntPtr video, ref AudioFrame audio, IntPtr metadata, uint timeout);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_util_audio_to_interleaved_32f_v2")]
        static extern void _AudioFrameToInterleaved_32f_v2(ref AudioFrame src, ref AudioFrameInterleaved dst);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_free_audio_v2")]
        static extern void _FreeAudio(Recv recv, ref AudioFrame data);

        [DllImport(Config.DllName, EntryPoint = "NDIlib_recv_set_tally")]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool _SetTally(Recv recv, in Tally tally);

        #endregion
    }

}
