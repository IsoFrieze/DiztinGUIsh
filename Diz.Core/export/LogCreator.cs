using System.Collections.Generic;
using System.Diagnostics;

namespace Diz.Core.export
{
    public class LogCreator : ILogCreatorForGenerator
    {
        public LogWriterSettings Settings { get; init; }
        public ILogCreatorDataSource Data { get; init; }
        private LogCreatorOutput Output { get; set; }
        public LineGenerator LineGenerator { get; private set; }
        public LabelTracker LabelTracker { get; private set; }
        private LogCreatorTempLabelGenerator LogCreatorTempLabelGenerator { get; set; }
        public DataErrorChecking DataErrorChecking { get; private set; }
        
        public virtual LogCreatorOutput.OutputResult CreateLog()
        {
            Init();

            try
            {
                LogCreatorTempLabelGenerator?.GenerateTemporaryLabels();
                WriteAllOutput();
            }
            finally
            {
                // MODIFIES UNDERLYING DATA. WE MUST ALWAYS MAKE SURE TO UNDO THIS
                LogCreatorTempLabelGenerator?.ClearTemporaryLabels();
            }

            var result = GetResult();
            CloseOutput(result);

            return result;
        }

        protected virtual void Init()
        {
            Debug.Assert(Settings.RomSizeOverride == -1 || Settings.RomSizeOverride <= Data.GetRomSize());

            InitOutput();
            
            DataErrorChecking = new DataErrorChecking { Data = Data };
            DataErrorChecking.ErrorNotifier += (_, errorInfo) => OnErrorReported(errorInfo.Offset, errorInfo.Msg);
            
            LineGenerator = new LineGenerator(this, Settings.Format);
            LabelTracker = new LabelTracker(this);
            
            if (Settings.Unlabeled != LogWriterSettings.FormatUnlabeled.ShowNone)
            {
                LogCreatorTempLabelGenerator = new LogCreatorTempLabelGenerator
                {
                    LogCreator = this,
                    GenerateAllUnlabeled = Settings.Unlabeled == LogWriterSettings.FormatUnlabeled.ShowAll,
                };
            }

            RegisterSteps();
        }

        public List<IAsmCreationStep> Steps { get; private set; }

        public void RegisterSteps()
        {
            Steps = new List<IAsmCreationStep>
            {
                new AsmCreationRomMap {LogCreator = this},
                new AsmCreationMainBankIncludes
                {                    
                    Enabled = Settings.Structure == LogWriterSettings.FormatStructure.OneBankPerFile,
                    LogCreator = this
                },
                
                new AsmCreationInstructions {LogCreator = this},
                
                new AsmStepWriteUnvisitedLabels
                {
                    LogCreator = this, 
                    LabelTracker = LabelTracker,
                },
                
                new AsmStepWriteUnvisitedLabelsIndex
                {
                    Enabled = Settings.IncludeUnusedLabels,
                    LogCreator = this,
                    LabelTracker = LabelTracker,
                }
            };
        }
        
        private void InitOutput()
        {
            Output = Settings.OutputToString ? new LogCreatorStringOutput() : Output = new LogCreatorStreamOutput();
            Output.Init(this);
        }

        private void CloseOutput(LogCreatorOutput.OutputResult result)
        {
            Output?.Finish(result);
            Output = null;
        }

        private LogCreatorOutput.OutputResult GetResult()
        {
            var result = new LogCreatorOutput.OutputResult
            {
                ErrorCount = Output.ErrorCount,
                Success = true,
                LogCreator = this
            };

            if (Settings.OutputToString)
                result.OutputStr = ((LogCreatorStringOutput) Output)?.OutputString;

            return result;
        }

        protected internal void OnErrorReported(int offset, string msg) => Output.WriteErrorLine(offset, msg);
        public int GetRomSize() => Settings.RomSizeOverride != -1 ? Settings.RomSizeOverride : Data.GetRomSize();
        protected virtual void WriteAllOutput() => Steps.ForEach(step => step?.Generate());
        public void WriteLine(string line) => Output.WriteLine(line);
        protected internal void WriteEmptyLine() => WriteSpecialLine("empty");
        internal void WriteSpecialLine(string special, int offset = -1)
        {
            var output = LineGenerator.GenerateSpecialLine(special, offset); 
            WriteLine(output);
        }

        protected internal void SetBank(int pointer, int bankToSwitchTo)
        {
            Output.SetBank(bankToSwitchTo);
            OnBankSwitched(pointer);
        }

        private void OnBankSwitched(int pointer)
        {
            WriteEmptyLine();
            WriteSpecialLine("org", pointer);
            WriteEmptyLine();
        }

        protected internal void SwitchOutputStream(string streamName)
        {
            Output.SwitchToStream(streamName);
            
            if (Settings.Structure == LogWriterSettings.FormatStructure.SingleFile) 
                WriteEmptyLine();
        }
        
        public void OnLabelVisited(int snesAddress) => LabelTracker.OnLabelVisited(snesAddress);
        public int GetLineByteLength(int offset) => Data.GetLineByteLength(offset, GetRomSize(), Settings.DataPerLine);
    }
}
