using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using FeedGenerators.Core.Services.Abstract;
using Indigo.Feeds.Generator.Core.Execution;
using Indigo.Feeds.Generator.Core.Execution.Contracts;
using Indigo.Feeds.Generator.Core.Processors;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using Indigo.Feeds.Generator.Core.Services;
using Indigo.Feeds.Services.Interfaces;
using Sterling.Feed.Export.CustomerData.Services;
using System.IO;
using System.Reflection;

namespace Sterling.Feed.Export.CustomerData.IoC
{
    internal class Installer : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            var typesToRegister = Classes.FromAssemblyInDirectory(new AssemblyFilter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));

            container.AddFacility<LoggingFacility>(f => f.UseLog4Net().WithAppConfig())
                .Register(Component.For<IBuilder>().ImplementedBy<Builder>())
                .Register(Component.For<IRunner>().ImplementedBy<Runner>())
                .Register(typesToRegister.BasedOn<IBaseFeeds>().WithService.FromInterface().LifestyleSingleton())
                .Register(typesToRegister.BasedOn<ICoreBase>().WithService.FromInterface().LifestyleSingleton())
                .Register(Component.For<IDataService>().ImplementedBy<CustomerDataService>().LifestyleSingleton())
                .Register(Component.For<IFileContentProcessor, IXmlFileContentProcessor>().ImplementedBy<XmlContentProcessor>()
                    .DependsOn(Dependency.OnAppSettingsValue("isGzip", "OutputProcessor.GzipFiles"))
                    .DependsOn(Dependency.OnAppSettingsValue("fileNameFormat", "OutputProcessor.FileNameFormat"))
                    .LifestyleSingleton());
        }
    }
}
