﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Indigo.Feeds.Services.Interfaces;
using Castle.Facilities.Logging;
using FeedGenerators.Core;

namespace GoogleCategoryImporter
{
    public class WindsorBootstrap
    {
        public IWindsorContainer Container { get; private set; }

        public WindsorBootstrap()
        {
            var binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Container = new WindsorContainer();
            Container.Install(FromAssembly.InDirectory(new AssemblyFilter(binPath)));
        }
    }

    public class WindsorInstaller : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            var typesToRegister = Classes.FromAssemblyInDirectory(new AssemblyFilter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));

            container.Register(Component.For<IBuilder>().ImplementedBy<Builder>())
            .Register(typesToRegister.BasedOn<IBaseFeeds>().WithService.FromInterface().LifestyleSingleton())
            .Register(Component.For<PathResolver>().ImplementedBy<PathResolver>())
            .AddFacility<LoggingFacility>(f => f.UseLog4Net("Log4net.config"));
        }
    }
}
