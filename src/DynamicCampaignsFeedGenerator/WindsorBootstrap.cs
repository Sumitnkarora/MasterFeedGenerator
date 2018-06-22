using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using DynamicCampaignsFeedGenerator.Execution;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Services.Interfaces;
using System.IO;
using System.Reflection;

namespace DynamicCampaignsFeedGenerator
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
            .AddFacility<LoggingFacility>(f => f.UseLog4Net("Log4net.config"));
        }
    }
}
