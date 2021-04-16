namespace Diz.Core.export
{
    public abstract class AsmStepExtraLabelOutputBase : AsmCreationBase
    {
        public LabelTracker LabelTracker { get; init; }
    }

    public class AsmStepWriteUnvisitedLabels : AsmStepExtraLabelOutputBase
    {
        protected override void Execute()
        {
            LogCreator.SwitchOutputStream("labels");

            foreach (var (snesAddress, _) in LabelTracker.UnvisitedLabels)
            {
                WriteUnusedLabel(snesAddress);
            }
        }

        private void WriteUnusedLabel(int snesAddress)
        {
            var pcOffset = Data.ConvertSnesToPc(snesAddress);
            LogCreator.WriteLine(LogCreator.LineGenerator.GenerateSpecialLine("labelassign", pcOffset));
        }
    }

    public class AsmStepWriteUnvisitedLabelsIndex : AsmStepExtraLabelOutputBase
    {
        protected override void Execute()
        {
            // part 2: optional: if requested, print all labels regardless of use.
            // Useful for debugging, documentation, or reverse engineering workflow.
            // this file shouldn't need to be included in the build, it's just reference documentation
            LogCreator.SwitchOutputStream("all-labels.txt"); // TODO: csv in the future. escape commas

            foreach (var (snesAddress, _) in Data.Labels.Labels)
            {
                WriteLabel(snesAddress);
            }
        }

        private void WriteLabel(int snesAddress)
        {
            // not the best place to add formatting, TODO: cleanup
            var category = LabelTracker.UnvisitedLabels.ContainsKey(snesAddress) ? "UNUSED" : "USED";
            var labelPcAddress = Data.ConvertSnesToPc(snesAddress);
            LogCreator.WriteLine($";!^!-{category}-! " +
                                 LogCreator.LineGenerator.GenerateSpecialLine("labelassign", labelPcAddress));
        }
    }
}