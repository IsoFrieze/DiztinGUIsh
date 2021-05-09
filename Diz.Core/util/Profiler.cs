#define JETBRAINS_PROFILING_ENABLED

using System;
using JetBrains.Profiler.SelfApi;
#if JETBRAINS_PROFILING_ENABLED

#endif

namespace Diz.Core.util
{
    // setup:
    // ProfilerDotTrace.Enabled = true; // can take a bit to download packages when run for first time
    //
    // to capture a snapshot of one function or scope:
    // using var captureSession = new CaptureSnapshot();
    //
    // or, to manually start/stop
    // ProfilerDotTrace.BeginSnapshot() / ProfilerDotTrace.EndSnapshot() 
    
    public static class ProfilerDotTrace
    {
        public class CaptureSnapshot : IDisposable
        {
            private readonly bool skipped;
            public CaptureSnapshot(bool shouldSkip = false)
            {
                skipped = shouldSkip;
                if (!shouldSkip)
                    BeginSnapshot();
            }

            private void ReleaseUnmanagedResources()
            {
                if (!skipped)
                    EndSnapshot();
            }

            public void Dispose()
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }

            ~CaptureSnapshot()
            {
                ReleaseUnmanagedResources();
            }
        }

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (!value && _enabled)
                {
                    Shutdown();
                }

                _enabled = value;

                if (_enabled)
                {
                    // initialize the API and download the tool (if needed)
                    DotTrace.EnsurePrerequisite();
                    Init();
                }
            }
        }

        private static bool _initialized;
        private static bool _enabled;
        private static bool _capturingSnapshot;

        // returns true if we're initialized
        // false if we weren't able to initialize
        private static bool Init()
        {
            if (!Enabled)
                return false;

            if (_initialized)
                return true;

#if JETBRAINS_PROFILING_ENABLED
            var config = new DotTrace.Config();
            config.SaveToDir("c:\\tmp\\snapshot");
            DotTrace.Attach(config);

            // now ready, call DotTrace.StartCollectingData() to begin snapshot.
#endif

            return _initialized = true;
        }

        public static void BeginSnapshot()
        {
            if (!Init() || _capturingSnapshot)
                return;

#if JETBRAINS_PROFILING_ENABLED
            DotTrace.StartCollectingData();
#endif

            _capturingSnapshot = true;
        }

        public static void EndSnapshot()
        {
            if (!_initialized || !_capturingSnapshot)
                return;

            // stop collecting current snaphot and save it to disk.
            // after this, need to call StartCollectingData() again to start a new snapshot, or quit.
            DotTrace.SaveData();

            _capturingSnapshot = false;
        }

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            EndSnapshot();

#if JETBRAINS_PROFILING_ENABLED
            DotTrace.Detach();
#endif

            _initialized = false;
        }
    }
}