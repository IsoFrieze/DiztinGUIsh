using Diz.Core.util;

namespace Diz.Core.export
{
    public interface IAsmCreationStep
    {
        LogCreator LogCreator { get; }
        void Generate();
    }

    public class AsmCreationBankManager
    {
        public LogCreator LogCreator { get; init; }
        public ILogCreatorDataSource Data => LogCreator?.Data;
        public int CurrentBank { get; protected set; } = -1;

        public void SwitchBanksIfNeeded(int pointer)
        {
            var snesAddress = Data.ConvertPCtoSnes(pointer);
            var newBank = RomUtil.GetBankFromSnesAddress(snesAddress);

            if (newBank != CurrentBank)
                SwitchBank(pointer, newBank, snesAddress);
        }

        private void SwitchBank(int pointer, int newBank, int snesAddress)
        {
            LogCreator.OpenNewBank(pointer, newBank);
            CurrentBank = newBank;

            if (snesAddress % Data.GetBankSize() == 0)
                return;

            LogCreator.ReportError(pointer, "An instruction crossed a bank boundary.");
        }
    }

    public class AsmCreationBase
    {
        public LogCreator LogCreator { get; init; }
        public ILogCreatorDataSource Data => LogCreator?.Data;
    }

    public class AsmCreationInstructions : AsmCreationBase, IAsmCreationStep
    {
        public AsmCreationBankManager BankManager { get; protected set; }

        public void Generate()
        {
            var size = LogCreator.GetRomSize();
            BankManager = new AsmCreationBankManager
            {
                LogCreator = LogCreator,
            };

            // perf: this is the meat of the export, takes a while
            for (var pointer = 0; pointer < size;)
            {
                WriteAddress(ref pointer);
            }
        }

        // write one line of the assembly output
        // address is a "PC address" i.e. offset into the ROM.
        // not a SNES address.
        protected void WriteAddress(ref int pointer)
        {
            BankManager.SwitchBanksIfNeeded(pointer);

            WriteBlankLineIfStartingNewParagraph(pointer);
            var lineTxt = LogCreator.LineGenerator.GenerateNormalLine(pointer);
            LogCreator.WriteLine(lineTxt);
            LogCreator.DataErrorChecking.CheckForErrorsAt(pointer);
            WriteBlankLineIfEndPoint(pointer);

            pointer += LogCreator.GetLineByteLength(pointer);
        }

        private void WriteBlankLineIfStartingNewParagraph(int pointer)
        {
            if (!Data.IsLocationAReadPoint(pointer) && !AreAnyLabelsPresentAt(pointer))
                return;

            LogCreator.GenerateEmptyLine();
        }

        private bool AreAnyLabelsPresentAt(int pointer)
        {
            var snesAddress = Data.ConvertPCtoSnes(pointer);
            return Data.Labels.GetLabel(snesAddress)?.Name.Length > 0;
        }

        private void WriteBlankLineIfEndPoint(int pointer)
        {
            if (!Data.IsLocationAnEndPoint(pointer))
                return;

            LogCreator.GenerateEmptyLine();
        }
    }
}