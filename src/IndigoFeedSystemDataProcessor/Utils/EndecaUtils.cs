using Castle.Core.Logging;
using Endeca.Navigation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Indigo.Feeds.Utils;

namespace IndigoFeedSystemDataProcessor.Utils
{
    public static class EndecaUtils
    {
        private static readonly string EndecaUrl = ParameterUtils.GetParameter<string>("EndecaUrl");
        private static readonly int EndecaPort = ParameterUtils.GetParameter<int>("EndecaPort");
        private static readonly long EndecaEnglishLanguageFilterId = ParameterUtils.GetParameter<long>("EndecaEnglishLanguageFilterId");
        private static readonly long EndecaFrenchLanguageFilterId = ParameterUtils.GetParameter<long>("EndecaFrenchLanguageFilterId");
        private static readonly string EndecaCategoryId = ParameterUtils.GetParameter<string>("EndecaCategoryId");
        private static readonly long EndecaSectionId = ParameterUtils.GetParameter<long>("EndecaSectionId");

        public static ILogger Log { get; set; }
        public static HttpENEConnection EndecaConnection { get; private set; }

        static EndecaUtils()
        {
            var container = new WindsorBootstrap().Container;
            Log = container.Resolve<ILogger>();
            EndecaConnection = new HttpENEConnection(EndecaUrl, EndecaPort);
        }

        /// <summary>
        /// Given a dimension value, retrieve a localized title (if one exists). When
        /// English is the local language, we simply return the dimension value name.
        /// When French title is requested, we check if a localized version exists and 
        /// if it does we return it. Otherwise, the default English title is returned.
        /// </summary>
        /// <param name="dimVal">Dimension value</param>
        /// <param name="isFrench">French language flag</param>
        /// <returns>Localized dimension value title</returns>
        public static string GetLocalizedTitle(DimVal dimVal, bool isFrench = false)
        {
            if (isFrench && dimVal.Properties.Contains("localization_fr"))
            {
                return dimVal.Properties["localization_fr"].ToString();
            }
            return dimVal.Name;
        }

        /// <summary>
        /// Gets the refinements for the given set of parameters. Only specified refienement dimensions are returned.
        /// </summary>
        /// <param name="refinementIds">Ids for the refienement dimensions we want to surface</param>
        /// <param name="dimVals">Dimension values for filter dimensions</param>
        /// <param name="isFrench">French language flag</param>
        /// <param name="numRetries">Number of retries</param>
        /// <param name="specifyLanguage">Indicates whether we wish to specify language or not</param>
        /// <returns>Dimension list with refinements</returns>
        public static DimensionList GetRefinements(List<long> refinementIds, IEnumerable<DimVal> dimVals = null,
                                                   bool isFrench = false, bool specifyLanguage = false)
        {
            var dimValStrList = dimVals == null
                                    ? new List<string> { "0" }
                                    : dimVals.Select(dv => dv.Id.ToString(CultureInfo.InvariantCulture)).ToList();
            var query = CreateEndecaQuery(dimValStrList,
                                          new List<KeyValuePair<string, string>>
                                              {
                                                  new KeyValuePair<string, string>("Ns", "PID|1"),
                                                  new KeyValuePair<string, string>("As", "PID|1")
                                              }, null,
                                          null,
                                          false,
                                          isFrench,
                                          specifyLanguage);

            query.NavExposedRefinements =
                new DimValIdList(string.Join(" ",
                                             refinementIds.Select(id => id.ToString(CultureInfo.InvariantCulture))
                                                          .ToList()));

            var results = ExecuteEndecaQuery(query);
            return (results == null || results.Navigation == null) ? null : results.Navigation.RefinementDimensions;
        }

        /// <summary>
        /// Creates an Endeca query given the supplied parameters and values.
        /// </summary>
        /// <param name="dimensionIds">List of dimension IDs for the N parameter</param>
        /// <param name="urlParams">List of other URL parameters</param>
        /// <param name="pageOffset">Current page offset (zero-based)</param>
        /// <param name="itemsPerPage">Number of items per page</param>
        /// <param name="allRefinements">Return refinements with query</param>
        /// <param name="isFrench">French language flag</param>
        /// <param name="specifyLanguage">Indicates whether we wish to specify language in the query or not</param>
        /// <returns></returns>
        public static ENEQuery CreateEndecaQuery(List<string> dimensionIds, List<KeyValuePair<string, string>> urlParams,
                                                 long? pageOffset, long? itemsPerPage, bool allRefinements = false,
                                                 bool isFrench = false, bool specifyLanguage = false)
        {
            var queryString = new UrlGen(string.Empty, Encoding.UTF8.WebName);

            // Append dimension IDs supplied to the N parameter
            var dimValsStr = dimensionIds == null ? "0" : string.Join(" ", dimensionIds);
            if (specifyLanguage)
            {
                dimValsStr += " " + (isFrench ? EndecaEnglishLanguageFilterId : EndecaFrenchLanguageFilterId);
            }
            queryString.AppendParam("N", dimValsStr);
            if (urlParams != null)
            {
                foreach (var param in urlParams)
                {
                    queryString.AppendParam(param.Key, param.Value);
                }
            }

            ENEQuery query = new UrlENEQuery(queryString.ToString(), Encoding.UTF8.WebName);

            itemsPerPage = itemsPerPage ?? 1;

            query.NavERecsOffset = pageOffset == null ? 0 : (long)pageOffset; // Start item (zero based)
            query.NavNumERecs = (long)itemsPerPage; // Number of items per page
            query.NavAllRefinements = allRefinements; // Supress the refinements (faster)
            query.NavExposedRefinements = new DimValIdList(EndecaCategoryId);

            return query;
        }

        /// <summary>
        /// Given an Endeca connection and a query, execute the query and return the results.
        /// </summary>
        /// <param name="query">Endeca query</param>
        /// <param name="retries">Desired number of retries</param>
        /// <returns>Endeca query results</returns>
        public static ENEQueryResults ExecuteEndecaQuery(ENEQuery query)
        {
            var results = EndecaConnection.Query(query);
            return results;
        }

        public static IEnumerable<EndecaLinkInfo> GetPageLinks(long dimensionId, bool isFrench = false, bool specifyLanguage = false)
        {
            var sections =
                GetRefinements(new List<long> { 0, EndecaSectionId }, null, isFrench, specifyLanguage)
                    .GetDimension(EndecaSectionId)
                    .Refinements;

            var links = new List<EndecaLinkInfo>();

            foreach (var sectionDimVal in sections.Cast<DimVal>())
            {
                links.AddRange(GetDimensionValueLinks(dimensionId, sectionDimVal, null, isFrench));
            }

            // Get all the distinct dimensionValue links to be used with "home" sectionValue
            var homeLinks = GeneralUtils.DeepCopy(GeneralUtils.DistinctBy(links, cl => cl.DimensionValue));
            homeLinks = homeLinks.Select(cli =>
            {
                cli.SectionValue = null;
                return cli;
            });

            links.AddRange(homeLinks.ToList());

            return links;
        }

        private static List<EndecaLinkInfo> GetDimensionValueLinks(long dimensionId, DimVal sectionValue,
                                                                   DimVal dimensionValue = null, bool isFrench = false, bool specifyLanguage = false)
        {
            var linkInfoList = new List<EndecaLinkInfo>();
            var dimValList = dimensionValue == null
                                 ? new List<DimVal> { sectionValue }
                                 : new List<DimVal> { sectionValue, dimensionValue };
            var dimension =
                GetRefinements(new List<long> { EndecaSectionId, dimensionId }, dimValList, isFrench, specifyLanguage)
                    .GetDimension(dimensionId);
            if (dimension != null && dimension.Refinements != null &&
                dimension.Refinements.Count > 0)
            {
                var dimensionRefinements = dimension.Refinements;
                if (dimensionRefinements.Count > 0)
                {
                    foreach (var dimRef in dimensionRefinements)
                    {
                        var dimVal = (DimVal)dimRef;

                        linkInfoList.Add(new EndecaLinkInfo(sectionValue, dimVal));

                        var linkInfoListNew = GetDimensionValueLinks(dimensionId, sectionValue, dimVal, isFrench);
                        if (linkInfoListNew != null && linkInfoListNew.Count > 0)
                        {
                            linkInfoList.AddRange(linkInfoListNew);
                        }
                    }
                }
            }

            return linkInfoList;
        }

        public class EndecaLinkInfo : ICloneable
        {
            public EndecaLinkInfo(DimVal section, DimVal dimensionValue)
            {
                DimensionValue = dimensionValue;
                SectionValue = section;
            }

            public object Clone()
            {
                return new EndecaLinkInfo(SectionValue, DimensionValue);
            }

            public DimVal DimensionValue { get; private set; }
            public DimVal SectionValue { get; set; }
        }
    }
}
