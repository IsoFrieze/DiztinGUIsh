using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Diz.Core.model.snes;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model
{
    /// <summary>
    /// One remembered place in the ROM: where the user was before a jump, so they can come back.
    ///
    /// This is a MODEL row, not a UI row -- a SNES address plus two strings describing why it was
    /// remembered -- which is why it lives in Diz.Core and not next to whichever controller happens
    /// to record it. The ViewModel layer (Diz.Ui.ViewModels) may reference Diz.Core and may NOT
    /// reference Diz.Controllers, so keeping it here is what lets the navigation-history ViewModel
    /// hold the real history list instead of a parallel copy that has to be kept in sync.
    ///
    /// The two description strings arrive as plain strings rather than as the caller's history-args
    /// object for the same reason: that object lives in Diz.Controllers, and only these two fields
    /// of it were ever read.
    ///
    /// IMMUTABLE once recorded. History is a log; nothing edits a point after the fact, which is
    /// also why the list holding these is created with AllowEdit/AllowNew/AllowRemove all false.
    ///
    /// The attributes are for WinForms' DataGridView column generation and are harmless elsewhere.
    /// </summary>
    public class NavigationEntry
    {
        /// <summary>
        /// The project data this point was recorded against. Carried for context; navigation itself
        /// converts through whatever project is open at the time, not through this.
        /// </summary>
        [Browsable(false)]
        public Data Data { get; }

        /// <summary>
        /// SNES address (not a ROM file offset). -1 means "this point does not name a real address",
        /// and navigating to such an entry is a no-op.
        /// </summary>
        [Browsable(false)]
        public int SnesOffset { get; }

        /// <param name="snesOffset">SNES address of the remembered point.</param>
        /// <param name="description">Why it was remembered, e.g. the action that jumped away.</param>
        /// <param name="position">Which end of that action this was, e.g. "start".</param>
        /// <param name="data">See <see cref="Data"/>.</param>
        public NavigationEntry(int snesOffset, [CanBeNull] string description, [CanBeNull] string position,
            [CanBeNull] Data data)
        {
            SnesOffset = snesOffset;
            Description = description ?? "";
            Position = position ?? "";

            Data = data;
        }

        [DisplayName("SNES Offset")]
        [Editable(false)]
        public string Address => Util.ToHexString6(SnesOffset);

        [Editable(false)]
        public string Description { get; }

        [Editable(false)]
        public string Position { get; }
    }
}
