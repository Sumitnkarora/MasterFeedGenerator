using System.IO;
using System.Reflection;
using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Indigo.Feeds.Services.Interfaces;
using IndigoFeedSystemDataProcessor.Services.Concrete;
using IndigoFeedSystemDataProcessor.Services.Contract;

namespace IndigoFeedSystemDataProcessor
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

            Indigo.CSI.Client.WcfHelpers.Utils.RegsiterInterfacesAndWcfClientFactory(container);

            container.Register(Component.For<IBuilder>().ImplementedBy<Builder>())
                .Register(Component.For<IDefaultRecosGeneratorService>().ImplementedBy<DefaultRecosGeneratorService>().LifestyleTransient())
                .Register(typesToRegister.BasedOn<IBaseFeeds>().WithService.FromInterface().LifestyleSingleton())
                .AddFacility<LoggingFacility>(f => f.UseLog4Net("Log4net.config"));
        }
    }
}
