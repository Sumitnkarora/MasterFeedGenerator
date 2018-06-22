using System;
using Indigo.Feeds.Generator.Core.Execution.Contracts;
using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Utils;
using System.Linq;
using Sterling.Feed.Export.ProductData.IoC;
using ConsoleCommon;
using Sterling.Feed.Export.ProductData.Models;

namespace Sterling.Feed.Export.ProductData
{
    class Program
    {

        private static readonly string FullRunParameterName = ParameterUtils.GetParameter<string>("FullRunParameterName");
        private static readonly int AppRunType = ParameterUtils.GetParameter<int>("RunType");

        static void Main(string[] args)
        {
            var runType = GetRunType(args);
            var container = new WindsorBootstrap(runType).Container;
            var builder = container.Resolve<IBuilder>();
            try
            {
                if (runType == RunType.OnDemand)
                {
                    ProductDataParamsObject productArgs = new ProductDataParamsObject(args);

                    productArgs.CheckParams();
                    string _helptext = productArgs.GetHelpIfNeeded();
                    //Print help to console if requested
                    if (!string.IsNullOrEmpty(_helptext))
                    {
                        Console.WriteLine(_helptext);
                        Environment.Exit(0);
                    }
                    builder.Build(runType, productArgs.StartTime, productArgs.EndTime);
                }
                else
                {
                    builder.Build(runType, null, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                container.Dispose();
            }
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
