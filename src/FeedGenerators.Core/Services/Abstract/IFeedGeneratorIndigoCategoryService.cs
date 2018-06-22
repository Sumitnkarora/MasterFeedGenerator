using Indigo.Feeds.Entities.Abstract;
using Indigo.Feeds.Services.Interfaces;
using System.Collections.Generic;

namespace FeedGenerators.Core.Services.Abstract
{
    public interface IFeedGeneratorIndigoCategoryService : IIndigoBreadcrumbService, IIndigoCategoryDataService
    {
        IEnumerable<IIndigoCategory> GetAllIndigoCategories();
    }
}
