using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Indigo.Feeds.Generator.Core.Enums;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Sterling.Feed.Export.RetailCategorization.IoC
{
    internal class WindsorBootstrap
    {
        public IWindsorContainer Container { get; private set; }

        public WindsorBootstrap(RunType runType)
        {
            var binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Container = new WindsorContainer();
            var installers = new List<IWindsorInstaller>();
            installers.Add(FromAssembly.InDirectory(new AssemblyFilter(binPath)));
            installers.Add(new Installer());
            if (runType == RunType.Full)
            {
                installers.Add(Configuration.FromXmlFile("Config/WindsorFull.config"));
            }
            Container.Install(installers.ToArray());
        }
    }
}
