﻿using Diz.Core.model;

namespace Diz.Core.arch
{
    public interface IReadOnlyCpuOperableByteSource : IReadOnlyByteSource
    {
        public Architecture GetArchitecture(int i);
        public FlagType GetFlag(int i);
        int GetMxFlags(int i);
        
        bool GetMFlag(int i);
        bool GetXFlag(int i);
    }
    
    public interface ICpuOperableByteSource : IReadOnlyCpuOperableByteSource
    {
        void SetMxFlags(int i, int mx);
        int Step(int offset, bool branch, bool force, int prevOffset);
    }
}