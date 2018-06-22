using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Execution.Contracts;
using Sterling.Feed.Export.StoreInformation.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Indigo.Feeds.Utils;


namespace Sterling.Feed.Export.StoreInformation
{
    class Program
    {
        
        private static readonly string FullRunParameterName = ParameterUtils.GetParameter<string>("FullRunParameterName");
        private static readonly int AppRunType = ParameterUtils.GetParameter<int>("RunType");

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            var runType = GetRunType(args);
            var container = new WindsorBootstrap(runType).Container;
            var builder = container.Resolve<IBuilder>();
            DateTime? startTime = null;
            DateTime? endTime = null;
            builder.Build(runType, startTime, endTime);
            container.Dispose();
        }

        private static RunType GetRunType(string[] args)
        {
            bool forceFullRun = args.ToList().Contains(FullRunParameterName, StringComparer.InvariantCultureIgnoreCase);
            if (forceFullRun)
            {
                return RunType.Full;
            }

            RunType runType;
            if (Enum.IsDefined(typeof(RunType), AppRunType) && Enum.TryParse(AppRunType.ToString(), out runType))
            {
                return runType;
            }

            return RunType.NotDefined;
        }
    }
}
