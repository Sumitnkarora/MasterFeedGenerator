using BVRatingImporter.Entities;
using Castle.Core.Logging;
using Indigo.Feeds.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Transactions;

namespace BVRatingImporter.Repositories
{
    public class RatingsImportRepository
    {
        private static readonly string BatchDbConnectionString = ConfigurationManager.ConnectionStrings["BatchDB"].ConnectionString;
        private static readonly int DbCommandTimeout = ParameterUtils.GetParameter<int>("DbCommandTimeout");

        private readonly ILogger _logger;

        public RatingsImportRepository(ILogger logger)
        {
            _logger = logger;
        }

        public void RemoveAllRatings()
        {
            _logger.Debug("In RemoveAllRatings.");

            ExecuteNonQuery("upsProductRatingsRemoveAll");

            _logger.Debug("Exiting RemoveAllRatings");
        }

        public void BulkInsert(IEnumerable<ProductRating> productRatings)
        {
            _logger.Debug("In BulkInsert");

            const string insertStatement =
                @"insert into [dbo].[tblProductRatings] (
	[PID],
	[Language],

	NativeRating1s,
	NativeRating2s,
	NativeRating3s,
	NativeRating4s,
	NativeRating5s,

	ExternalRating1s,
	ExternalRating2s,
	ExternalRating3s,
	ExternalRating4s,
	ExternalRating5s,

    NativeTotalReviews,
    ExternalTotalReviews, 
    DateCreated
)
values (
	{0},
    '{1}',

    {2},
	{3},
	{4},
	{5},
	{6},
	
    {7},
	{8},
	{9},
	{10},
	{11},

    {12},
    {13}, 
    GETDATE()
);
";

            var stringBuilder = new StringBuilder();

            productRatings.ToList().ForEach(productRating =>
            {
                var parameters = new List<object>
                {
                    productRating.PID,
                    productRating.LanguageString
                };

                for (var i = 1; i <= 5; i++)
                {
                    parameters.Add(productRating.NativeRatings[i]);
                }

                for (var i = 1; i <= 5; i++)
                {
                    parameters.Add(productRating.ExternalRatings[i]);
                }

                parameters.AddRange(new object[]
                {productRating.NativeTotalReviewCount, productRating.ExternalTotalReviewCount});

                stringBuilder.AppendFormat(insertStatement, parameters.ToArray());
            });

            var statements = stringBuilder.ToString();

            var transactionOption = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted };
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOption))
            {
                ExecuteNonQuery(statements, CommandType.Text);

                scope.Complete();
            }

            _logger.Debug("Exiting BulkInsert");
        }

        public HashSet<long> GetPidsWithRatings()
        {
            _logger.Debug("In GetPidsWithRatings.");

            var result = new HashSet<long>();

            RunStoredProcedure("uspProductRatingsGetPidsWithRatings", dbCommand =>
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(Convert.ToInt64(reader[0]));
                    }       
                }
            });

            _logger.Debug("Exiting GetPidsWithRatings.");

            return result;
        }

        public IList<ProductRating> GetRatings(long pid)
        {
            _logger.Debug("In GetRatings.");

            var result = new List<ProductRating>();

            RunStoredProcedure("uspProductRatingsGetRatingsByPid", dbCommand =>
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var nativeRatings = GetRatingCounts(reader,
                            "NativeRating1s",
                            "NativeRating2s",
                            "NativeRating3s",
                            "NativeRating4s",
                            "NativeRating5s");

                        var externalRatings = GetRatingCounts(reader,
                            "ExternalRating1s",
                            "ExternalRating2s",
                            "ExternalRating3s",
                            "ExternalRating4s",
                            "ExternalRating5s");

                        var languageString = (string) reader["Language"];
                        var nativeTotalReviews = (int)reader["NativeTotalReviews"];
                        var externalTotalReviews = (int)reader["ExternalTotalReviews"];
                        var combinedTotalReviews = (int)reader["CombinedTotalReviews"];

                        var rating = new ProductRating(pid, languageString, nativeTotalReviews, externalTotalReviews,
                            combinedTotalReviews, nativeRatings, externalRatings);

                        result.Add(rating);
                    }
                }
            },

                new SqlParameter("@PID", (decimal) pid)
                );

            _logger.Debug("Exiting GetRatings.");

            return result;
        }

        public void Insert(ProductRating productRating)
        {
            _logger.Debug("In Insert.");

            var parameters = GetInsertOrUpdateParameterArray(productRating);

            ExecuteNonQuery("uspProductRatingsInsert", parameters);

            _logger.Debug("Existing Insert.");
        }

        public void Update(ProductRating productRating)
        {
            _logger.Debug("In Update.");

            var parameters = GetInsertOrUpdateParameterArray(productRating);

            ExecuteNonQuery("uspProductRatingsUpdate", parameters);

            _logger.Debug("Existing Update.");
        }

        public void Delete(long pid)
        {
            _logger.Debug("In Delete(pid).");

            ExecuteNonQuery("uspProductRatingsDeleteByPid", new SqlParameter("@PID", (decimal) pid));

            _logger.Debug("Exiting Delete(pid).");
        }

        public void Delete(long pid, string languageString)
        {
            _logger.Debug("In Delete(pid, languageString).");

            ExecuteNonQuery("uspProductRatingsDeleteByPidAndLanguage", 
                new SqlParameter("@PID", (decimal)pid),
                new SqlParameter("@Language", languageString));

            _logger.Debug("Exiting Delete(pid, languageString).");
        }

        #region Private Methods

        private void ExecuteNonQuery(string procedureName, params SqlParameter[] parameters)
        {
            ExecuteNonQuery(procedureName, CommandType.StoredProcedure, parameters);
        }

        private void ExecuteNonQuery(string commandText, CommandType commandType, params SqlParameter[] parameters)
        {
            RunStoredProcedure(commandText, dbCommand =>
            {
                dbCommand.CommandType = commandType;
                dbCommand.ExecuteNonQuery();

            }, parameters);
        }

        private void RunStoredProcedure(string commandText, Action<IDbCommand> callback, params SqlParameter[] parameters)
        {
            using (var sqlConnection = new SqlConnection(BatchDbConnectionString))
            {
                sqlConnection.Open();

                using (var sqlCommand = new SqlCommand(commandText, sqlConnection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = DbCommandTimeout,
                })
                {
                    sqlCommand.Parameters.AddRange(parameters);
                    callback(sqlCommand);
                }
            }
        }

        private static ProductRating.RatingCounts GetRatingCounts(IDataReader reader, params string[] columns)
        {
            var result = new ProductRating.RatingCounts();

            for (int i = 1; i <= 5; i++)
            {
                result[i] = (int) reader[columns[i - 1]];
            }

            return result;
        }

        private static SqlParameter[] GetInsertOrUpdateParameterArray(ProductRating productRating)
        {
            var result = new List<SqlParameter>()
            {
                new SqlParameter("@PID", (decimal) productRating.PID),
                new SqlParameter("@Language", productRating.LanguageString),
                
                new SqlParameter("@NativeRating1s", productRating.NativeRatings[1]),
                new SqlParameter("@NativeRating2s", productRating.NativeRatings[2]),
                new SqlParameter("@NativeRating3s", productRating.NativeRatings[3]),
                new SqlParameter("@NativeRating4s", productRating.NativeRatings[4]),
                new SqlParameter("@NativeRating5s", productRating.NativeRatings[5]),
                
                new SqlParameter("@ExternalRating1s", productRating.ExternalRatings[1]),
                new SqlParameter("@ExternalRating2s", productRating.ExternalRatings[2]),
                new SqlParameter("@ExternalRating3s", productRating.ExternalRatings[3]),
                new SqlParameter("@ExternalRating4s", productRating.ExternalRatings[4]),
                new SqlParameter("@ExternalRating5s", productRating.ExternalRatings[5]),

                new SqlParameter("@NativeTotalReviews", productRating.NativeTotalReviewCount),
                new SqlParameter("@ExternalTotalReviews", productRating.ExternalTotalReviewCount)
            };

            return result.ToArray();
        }

        #endregion Private Methods
    }
}

