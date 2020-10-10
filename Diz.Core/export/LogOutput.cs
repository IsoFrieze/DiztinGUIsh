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
        protected StringBuilder outputBuilder = new StringBuilder();
        protected StringBuilder errorBuilder = new StringBuilder();

        public string OutputString => outputBuilder.ToString();
        public string ErrorString => errorBuilder.ToString();

        protected override void Init()
        {
            Debug.Assert(LogCreator.Settings.outputToString && LogCreator.Settings.structure == LogCreator.FormatStructure.SingleFile);
        }

        public override void WriteLine(string line) => outputBuilder.AppendLine(line);
        public override void WriteErrorLine(string line) => errorBuilder.AppendLine(line);

        public override void Finish(LogCreator.OutputResult result)
        {
            result.outputStr = OutputString;
        }
    }

    public class LogCreatorStreamOutput : LogCreatorOutput
    {
        protected Dictionary<string, StreamWriter> outputStreams = new Dictionary<string, StreamWriter>();
        protected StreamWriter errorOutputStream;

        // references to stuff in outputStreams
        private string activeStreamName;
        private StreamWriter activeOutputStream;

        protected string folder;
        protected string filename; // if set to single file output moe.

        protected override void Init()
        {
            var basePath = LogCreator.Settings.fileOrFolderOutPath;

            if (LogCreator.Settings.structure == LogCreator.FormatStructure.OneBankPerFile)
                basePath += "\\"; // force it to treat it as a path. not the best way.

            folder = Path.GetDirectoryName(basePath);

            if (LogCreator.Settings.structure == LogCreator.FormatStructure.SingleFile)
            {
                filename = Path.GetFileName(LogCreator.Settings.fileOrFolderOutPath);
                SwitchToStream(filename);
            }
            else
            {
                SwitchToStream("main");
            }

            SwitchToStream(LogCreator.Settings.errorFilename, isErrorStream: true);
        }

        public override void Finish(LogCreator.OutputResult result)
        {
            foreach (var stream in outputStreams)
            {
                stream.Value.Close();
            }
            outputStreams.Clear();

            activeOutputStream = null;
            errorOutputStream = null;
            activeStreamName = "";

            if (result.error_count == 0)
                File.Delete(BuildStreamPath(LogCreator.Settings.errorFilename));
        }

        public override void SwitchToBank(int bankNum)
        {
            var bankStr = Util.NumberToBaseString(bankNum, Util.NumberBase.Hexadecimal, 2);
            SwitchToStream($"bank_{ bankStr}");
        }

        public override void SwitchToStream(string streamName, bool isErrorStream = false)
        {
            // don't switch off the main file IF we're only supposed to be outputting one file
            if (LogCreator.Settings.structure == LogCreator.FormatStructure.SingleFile &&
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

        public void SetActiveStream(string streamName, StreamWriter streamWriter)
        {
            activeStreamName = streamName;
            activeOutputStream = streamWriter;
        }

        protected virtual StreamWriter OpenNewStream(string streamName)
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
