using System.Diagnostics.Tracing;
using System.Threading;

namespace DiztinGUIsh.util
{
    /*[EventSource(Name = "GridUI")]
    internal sealed class DizUIGridTrace : EventSource
    {
        internal static class DebugCounters
        {
            public static int CellValueNeeded;
            public static int CellPainted;
            public static int ScrollEvents;
        }

        private void _IncrementEvent(int eventId, ref int value) =>
            WriteEvent(eventId, Interlocked.Increment(ref value));

        public void Message(string message) => WriteEvent(1, message);
        public void CellValueNeeded() => _IncrementEvent(2, ref DebugCounters.CellValueNeeded);
        public void CellPainted() => _IncrementEvent(3, ref DebugCounters.CellPainted);
        public void ScrollEvent() => _IncrementEvent(4, ref DebugCounters.ScrollEvents);

        public static readonly DizUIGridTrace Log = new();
    }*/
    
    [EventSource(Name="GridUI")]
    public sealed class DizUIGridTrace : EventSource {

        public static readonly DizUIGridTrace Log = new();

        public class Tasks {
            public const EventTask CellValueNeeded = (EventTask)0x1;
            public const EventTask CellPainting = (EventTask)0x2;
            public const EventTask SelectCell = (EventTask)0x3;
        }

        private const int CellValueNeededStart = 1;
        private const int CellValueNeededStop = 2;
        private const int CellPaintingStart = 3;
        private const int CellPaintingStop = 4;
        private const int SelectCellStart = 5;
        private const int SelectCellStop = 6;

        [Event(CellValueNeededStart, Task=Tasks.CellValueNeeded, Opcode=EventOpcode.Start)]
        public void CellValueNeeded_Start(string commandText = "") {
            if (IsEnabled()) WriteEvent(CellValueNeededStart, commandText);
        }

        [Event(CellValueNeededStop, Task=Tasks.CellValueNeeded, Opcode=EventOpcode.Stop)]
        public void CellValueNeeded_Stop(string commandText = "") {
            if (IsEnabled()) WriteEvent(CellValueNeededStop, commandText);
        }
        
        [Event(CellPaintingStart, Task=Tasks.CellPainting, Opcode=EventOpcode.Start)]
        public void CellPainting_Start(string commandText = "") {
            if (IsEnabled()) WriteEvent(CellPaintingStart, commandText);
        }

        [Event(CellPaintingStop, Task=Tasks.CellPainting, Opcode=EventOpcode.Stop)]
        public void CellPainting_Stop(string commandText = "") {
            if (IsEnabled()) WriteEvent(CellPaintingStop, commandText);
        }

        [Event(SelectCellStart, Task=Tasks.SelectCell, Opcode=EventOpcode.Start)]
        public void SelectCell_Start()
        {
            if (IsEnabled()) WriteEvent(SelectCellStart, "");
        }
        
        [Event(SelectCellStop, Task=Tasks.SelectCell, Opcode=EventOpcode.Stop)]
        public void SelectCell_Stop()
        {
            if (IsEnabled()) WriteEvent(SelectCellStop, "");
        }
    }
}