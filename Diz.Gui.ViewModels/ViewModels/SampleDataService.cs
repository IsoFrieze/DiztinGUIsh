﻿using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.util;
using DynamicData;
using Diz.Core.model;

namespace Diz.Gui.ViewModels.ViewModels
{
    public static class SampleDataService
    {
        // create a global cache for this data, if it's modified, everything will get a copy
        public static Lazy<Data> SourceData { get; } = new(SampleRomData.CreateSampleData);
    }
}