namespace Diz.Core.commands
{
    public class MarkCommand
    {
        public int PropertyIndex { get; set; }
        public int Start { get; set; }
        public int Count { get; set; }
        public object Value { get; set; }
    }
}