using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Diz.Core.util;

namespace Diz.Core.export
{
    public abstract class LogCreatorOutput
    {
        protected LogCreator LogCreator;

        public void Init(LogCreator logCreator)
        {
            LogCreator = logCreator;
            Init();
        }

        protected virtual void Init() { }
        public virtual void Finish(LogCreator.OutputResult result) { }
        public virtual void SwitchToBank(int bankNum) { }
        public virtual void SwitchToStream(string streamName, bool isErrorStream = false) { }
        public abstract void WriteLine(string line);
        public abstract void WriteErrorLine(string line);
    }

    public class LogCreatorStringOutput : LogCreatorOutput
    {
        protected StringBuilder OutputBuilder = new StringBuilder();
        protected StringBuilder ErrorBuilder = new StringBuilder();

        public string OutputString => OutputBuilder.ToString();
        public string ErrorString => ErrorBuilder.ToString();

        protected override void Init()
        {
            Debug.Assert(LogCreator.Settings.OutputToString && LogCreator.Settings.Structure == LogCreator.FormatStructure.SingleFile);
        }

        public override void WriteLine(string line) => OutputBuilder.AppendLine(line);
        public override void WriteErrorLine(string line) => ErrorBuilder.AppendLine(line);

        public override void Finish(LogCreator.OutputResult result)
        {
            result.OutputStr = OutputString;
        }
    }

    public class LogCreatorStreamOutput : LogCreatorOutput
    {
        protected Dictionary<string, StreamWriter> OutputStreams = new Dictionary<string, StreamWriter>();
        protected StreamWriter ErrorOutputStream;

        // references to stuff in outputStreams
        private string activeStreamName;
        private StreamWriter activeOutputStream;

        protected string Folder;
        protected string Filename; // if set to single file output moe.

        protected override void Init()
        {
            var basePath = LogCreator.Settings.FileOrFolderOutPath;

            if (LogCreator.Settings.Structure == LogCreator.FormatStructure.OneBankPerFile)
                basePath += "\\"; // force it to treat it as a path. not the best way.

            Folder = Path.GetDirectoryName(basePath);

            if (LogCreator.Settings.Structure == LogCreator.FormatStructure.SingleFile)
            {
                Filename = Path.GetFileName(LogCreator.Settings.FileOrFolderOutPath);
                SwitchToStream(Filename);
            }
            else
            {
                SwitchToStream("main");
            }

            SwitchToStream(LogCreator.Settings.ErrorFilename, isErrorStream: true);
        }

        public override void Finish(LogCreator.OutputResult result)
        {
            foreach (var stream in OutputStreams)
            {
                stream.Value.Close();
            }
            OutputStreams.Clear();

            activeOutputStream = null;
            ErrorOutputStream = null;
            activeStreamName = "";

            if (result.ErrorCount == 0)
                File.Delete(BuildStreamPath(LogCreator.Settings.ErrorFilename));
        }

        public override void SwitchToBank(int bankNum)
        {
            var bankStr = Util.NumberToBaseString(bankNum, Util.NumberBase.Hexadecimal, 2);
            SwitchToStream($"bank_{ bankStr}");
        }

        public override void SwitchToStream(string streamName, bool isErrorStream = false)
        {
            // don't switch off the main file IF we're only supposed to be outputting one file
            if (LogCreator.Settings.Structure == LogCreator.FormatStructure.SingleFile &&
                !string.IsNullOrEmpty(activeStreamName))
                return;

            var whichStream = OutputStreams.TryGetValue(streamName, out var outputStream) 
                ? outputStream 
                : OpenNewStream(streamName);

            if (!isErrorStream)
                SetActiveStream(streamName, whichStream);
            else
                ErrorOutputStream = whichStream;
        }

        public void SetActiveStream(string streamName, StreamWriter streamWriter)
        {
            activeStreamName = streamName;
            activeOutputStream = streamWriter;
        }

        protected virtual StreamWriter OpenNewStream(string streamName)
        {
            var streamWriter = new StreamWriter(BuildStreamPath(streamName));
            OutputStreams.Add(streamName, streamWriter);
            return streamWriter;
        }

        private string BuildStreamPath(string streamName)
        {
            var fullOutputPath = Path.Combine(Folder, streamName);

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
            ErrorOutputStream?.WriteLine(line);
        }
    }
}
