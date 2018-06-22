
namespace CjFeedGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new WindsorBootstrap().Container;

            var builder = container.Resolve<IBuilder>();

            builder.Build(args);

            container.Dispose();
        }
    }
}
