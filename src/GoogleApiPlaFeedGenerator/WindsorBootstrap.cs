using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using FeedGenerators.Core.Services.Abstract;
using GoogleApiPlaFeedGenerator.Execution;
using Indigo.Feeds.GoogleShoppingApiIntegration.Services.Concrete;
using Indigo.Feeds.GoogleShoppingApiIntegration.Services.Interfaces;
using Indigo.Feeds.Services.Interfaces;
using System.IO;
using System.Reflection;

namespace GoogleApiPlaFeedGenerator
{
    internal class WindsorBootstrap
    {
        public IWindsorContainer Container { get; private set; }

        public WindsorBootstrap()
        {
            var binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Container = new WindsorContainer();
            Container.Install(new WindsorInstaller(), FromAssembly.InDirectory(new AssemblyFilter(binPath)));
        }
    }

    internal class WindsorInstaller : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            var typesToRegister = Classes.FromAssemblyInDirectory(new AssemblyFilter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));

            container.Register(Component.For<IBuilder>().ImplementedBy<Builder>()).Register(Component.For<IRunner>().ImplementedBy<Runner>())
            .Register(typesToRegister.BasedOn<IBaseFeeds>().WithService.FromInterface().LifestyleSingleton())
            .Register(typesToRegister.BasedOn<ICoreBase>().WithService.FromInterface().LifestyleSingleton())
            .Register(typesToRegister.BasedOn<IExecutionLogLogger>().WithService.FirstInterface().LifestyleTransient())
            .Register(Component.For<IGooglePlaDataService>().ImplementedBy<GooglePlaDataService>().LifestyleSingleton())
            .Register(Component.For<IOutputInstructionProcessor>().ImplementedBy<OutputInstructionProcessor>().LifestyleSingleton())
            .Register(Component.For<IGoogleRequestProcessor>().ImplementedBy<GoogleRequestProcessor>().LifestyleSingleton())
            .AddFacility<LoggingFacility>(f => f.UseLog4Net("Log4net.config"));
        }
    }
}
