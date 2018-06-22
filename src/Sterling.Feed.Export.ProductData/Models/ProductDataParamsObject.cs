using ConsoleCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class ProductDataParamsObject : ParamsObject
    {
        public ProductDataParamsObject(string[] args)
          : base(args)
        {
        }

        [Switch("StartTime")]
        public DateTime StartTime { get; set; }

        [Switch("EndTime")]
        public DateTime EndTime { get; set; }

        [HelpText(0)]
        public string Description
        {
            get { return "Exports product feed to Sterling"; }
        }

        [HelpText(2)]
        public override string Usage
        {
            get { return base.Usage; }
        }

        public override Dictionary<Func<bool>, string> GetParamExceptionDictionary()
        {
            Dictionary<Func<bool>, string> _exceptionChecks = new Dictionary<Func<bool>, string>();

            Func<bool> _isDateInFuture = new Func<bool>(() => this.EndTime < this.StartTime);
            Func<bool> _isDateNotSpecified = new Func<bool>(() => this.EndTime == DateTime.MinValue || this.StartTime == DateTime.MinValue);

            _exceptionChecks.Add(_isDateNotSpecified,
                                 "Please specify the start and end time in this format : Sterling.Feed.Export.ProductData.exe /STARTTIME:\"StartTime\" /ENDTIME:\"EndTime\"");
            _exceptionChecks.Add(_isDateInFuture,
                                 "Please set the end time ahead of start time!");
            return _exceptionChecks;
        }
        

    }
}
