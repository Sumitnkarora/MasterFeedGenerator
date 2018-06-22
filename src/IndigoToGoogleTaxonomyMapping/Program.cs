using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndigoToGoogleTaxonomyMapping
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var container = new WindsorBootstrap().Container)
            {
                var mapper = container.Resolve<IMapper>();

                mapper.Execute();
            }
        }
    }
}
