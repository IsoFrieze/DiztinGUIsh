using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Diz.Core.export
{
    public class LogCreator : ILogCreatorForGenerator
    {
        public LogWriterSettings Settings { get; set; }
        public ILogCreatorDataSource Data { get; init; }
        private LogCreatorOutput Output { get; set; }
        public LineGenerator LineGenerator { get; private set; }
        public LabelTracker LabelTracker { get; private set; }
        private LogCreatorTempLabelGenerator LogCreatorTempLabelGenerator { get; set; }
        public DataErrorChecking DataErrorChecking { get; private set; }

        public class ProgressEvent
        {
            public enum Status
            {
                StartInit,
                DoneInit,
                StartTemporaryLabelsGenerate,
                DoneTemporaryLabelsGenerate,
                StartMainOutputSteps,
                StartNewMainOutputStep,
                DoneMainOutputSteps,
                StartTemporaryLabelsRemoval,
                EndTemporaryLabelsRemoval,
                FinishingCleanup,
                Done,
            }

            public Status State { get; init; }
        }

        public event EventHandler<ProgressEvent> ProgressChanged;
        
        protected virtual void OnProgressChanged(ProgressEvent.Status status)
        {
            ProgressChanged?.Invoke(this, new ProgressEvent {
                State = status,
            });
        }
        
        public virtual LogCreatorOutput.OutputResult CreateLog()
        {
            Init();

            try
            {
                CreateTemporaryLabels();
                
                WriteAllOutput();
            }
            finally
            {
                // MODIFIES UNDERLYING DATA. WE MUST ALWAYS MAKE SURE TO UNDO THIS
                RemoveTemporaryLabels();
            }

            OnProgressChanged(ProgressEvent.Status.FinishingCleanup);
            var result = GetResult();
            CloseOutput(result);

            OnProgressChanged(ProgressEvent.Status.Done);
            return result;
        }

        private void RemoveTemporaryLabels()
        {
            OnProgressChanged(ProgressEvent.Status.StartTemporaryLabelsRemoval);
            LogCreatorTempLabelGenerator?.ClearTemporaryLabels();
            OnProgressChanged(ProgressEvent.Status.EndTemporaryLabelsRemoval);
        }

        private void CreateTemporaryLabels()
        {
            OnProgressChanged(ProgressEvent.Status.StartTemporaryLabelsGenerate);
            LogCreatorTempLabelGenerator?.GenerateTemporaryLabels();
            OnProgressChanged(ProgressEvent.Status.DoneTemporaryLabelsGenerate);
        }

        public IAsmCreationStep CurrentOutputStep { get; private set; }
        
        protected virtual void WriteAllOutput()
        {
            OnProgressChanged(ProgressEvent.Status.StartMainOutputSteps);
            
            Steps.ForEach(step =>
            {
                if (step == null)
                    return;
                
                CurrentOutputStep = step;
                OnProgressChanged(ProgressEvent.Status.StartNewMainOutputStep);
                CurrentOutputStep.Generate();
                CurrentOutputStep = null;
            });
            
            OnProgressChanged(ProgressEvent.Status.DoneMainOutputSteps);
        }

        protected virtual void Init()
        {
            OnProgressChanged(ProgressEvent.Status.StartInit);
            
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
            
            OnProgressChanged(ProgressEvent.Status.DoneInit);
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
        public void WriteLine(string line) => Output.WriteLine(line);
        protected internal void WriteEmptyLine() => WriteSpecialLine("empty");
        internal void WriteSpecialLine(string special, int offset = -1)
        {
            if (special == "empty" && !Settings.OutputExtraWhitespace)
                return;
            
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
