namespace Diz.Core.export
{
    public interface IAsmCreationStep
    {
        bool Enabled { get; set; }
        void Generate();
    }

    public abstract class AsmCreationBase : IAsmCreationStep
    {
        public LogCreator LogCreator { get; init; }
        public bool Enabled { get; set; } = true;
        public ILogCreatorDataSource Data => LogCreator?.Data;

        public void Generate()
        {
            if (!Enabled)
                return;
            
            Execute();
        }
        protected abstract void Execute();
    }

    public class AsmCreationMainBankIncludes : AsmCreationBase
    {
        protected override void Execute()
        {
            var size = LogCreator.GetRomSize();
            
            for (var i = 0; i < size; i += Data.GetBankSize())
                LogCreator.WriteSpecialLine("incsrc", i);
            
            // output the include for labels.asm file
            LogCreator.WriteSpecialLine("incsrc");
        }
    }
    
    public class AsmCreationRomMap : AsmCreationBase
    {
        protected override void Execute()
        {
            LogCreator.WriteSpecialLine("map");
            LogCreator.WriteEmptyLine();
        }
    }
}