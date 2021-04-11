using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Diz.Core.util;

namespace Diz.Core.export
{
    public abstract class LogCreatorOutput
    {
        public class OutputResult
        {
            public bool Success;
            public int ErrorCount = -1;
            public LogCreator LogCreator;
            public string OutputStr = ""; // this is only populated if outputString=true
        }
        
        protected LogCreator LogCreator;

        public void Init(LogCreator logCreator)
        {
            LogCreator = logCreator;
            Init();
        }

        protected virtual void Init() { }
        public virtual void Finish(OutputResult result) { }
        public virtual void SwitchToBank(int bankNum) { }
        public virtual void SwitchToStream(string streamName, bool isErrorStream = false) { }
        public abstract void WriteLine(string line);
        public abstract void WriteErrorLine(string line);
    }

    public class LogCreatorStringOutput : LogCreatorOutput
    {
        private readonly StringBuilder outputBuilder = new();
        private readonly StringBuilder errorBuilder = new();

        public string OutputString => outputBuilder.ToString();
        public string ErrorString => errorBuilder.ToString();

        protected override void Init()
        {
            Debug.Assert(LogCreator.Settings.OutputToString && LogCreator.Settings.Structure == LogWriterSettings.FormatStructure.SingleFile);
        }

        public override void WriteLine(string line) => outputBuilder.AppendLine(line);
        public override void WriteErrorLine(string line) => errorBuilder.AppendLine(line);

        public override void Finish(OutputResult result)
        {
            result.OutputStr = OutputString;
        }
    }

    public class LogCreatorStreamOutput : LogCreatorOutput
    {
        private readonly Dictionary<string, StreamWriter> outputStreams = new();
        private StreamWriter errorOutputStream;

        // references to stuff in outputStreams
        private string activeStreamName;
        private StreamWriter activeOutputStream;

        private string folder;
        private string filename; // if set to single file output mode.

        protected override void Init()
        {
            var basePath = LogCreator.Settings.FileOrFolderOutPath;

            if (LogCreator.Settings.Structure == LogWriterSettings.FormatStructure.OneBankPerFile)
                basePath += "\\"; // force it to treat it as a path. not the best way.

            folder = Path.GetDirectoryName(basePath);

            if (LogCreator.Settings.Structure == LogWriterSettings.FormatStructure.SingleFile)
            {
                filename = Path.GetFileName(LogCreator.Settings.FileOrFolderOutPath);
                SwitchToStream(filename);
            }
            else
            {
                SwitchToStream("main");
            }

            SwitchToStream(LogCreator.Settings.ErrorFilename, isErrorStream: true);
        }

        public override void Finish(OutputResult result)
        {
            foreach (var stream in outputStreams)
            {
                stream.Value.Close();
            }
            outputStreams.Clear();

            activeOutputStream = null;
            errorOutputStream = null;
            activeStreamName = "";

            if (result.ErrorCount == 0)
                File.Delete(BuildStreamPath(LogCreator.Settings.ErrorFilename));
        }

        public override void SwitchToBank(int bankNum)
        {
            var bankStr = Util.NumberToBaseString(bankNum, Util.NumberBase.Hexadecimal, 2);
            SwitchToStream($"bank_{bankStr}");
        }

        public override void SwitchToStream(string streamName, bool isErrorStream = false)
        {
            // don't switch off the main file IF we're only supposed to be outputting one file
            if (LogCreator.Settings.Structure == LogWriterSettings.FormatStructure.SingleFile &&
                !string.IsNullOrEmpty(activeStreamName))
                return;

            var whichStream = outputStreams.TryGetValue(streamName, out var outputStream) 
                ? outputStream 
                : OpenNewStream(streamName);

            if (!isErrorStream)
                SetActiveStream(streamName, whichStream);
            else
                errorOutputStream = whichStream;
        }

        private void SetActiveStream(string streamName, StreamWriter streamWriter)
        {
            activeStreamName = streamName;
            activeOutputStream = streamWriter;
        }

        protected StreamWriter OpenNewStream(string streamName)
        {
            var streamWriter = new StreamWriter(BuildStreamPath(streamName));
            outputStreams.Add(streamName, streamWriter);
            return streamWriter;
        }

        private string BuildStreamPath(string streamName)
        {
            var fullOutputPath = Path.Combine(folder, streamName);

            if (!Path.HasExtension(fullOutputPath))
                fullOutputPath += ".asm";
            return fullOutputPath;
        }

        public override void WriteLine(string line)
        {
            Debug.Assert(activeOutputStream != null && !string.IsNullOrEmpty(activeStreamName));
            activeOutputStream.WriteLine(line);
        }

        public override void WriteErrorLine(string line)
        {
            errorOutputStream?.WriteLine(line);
        }
    }
}
