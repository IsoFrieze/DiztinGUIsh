namespace Diz.Core.commands
{
    public record MarkCommand
    {
        public int Property { get; init; }
        public int Start { get; init; }
        public int Count { get; init; }
        public object Value { get; init; }
    }
}