
namespace GooglePlaFeedGenerator
{
    class GoogleFeed
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
