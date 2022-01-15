using LightInject;

namespace Diz.Test.Utils;

public class DizTestCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // none should really be needed, but, add anything test-specific if needed. use sparingly.
        // consider overriding the Configure() method in ContainerFixture instead
    }
}