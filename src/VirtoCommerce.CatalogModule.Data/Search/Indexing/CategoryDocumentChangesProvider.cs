using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.Platform.Core.ChangeLog;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.CatalogModule.Data.Search.Indexing
{
    public class CategoryDocumentChangesProvider : IIndexDocumentChangesProvider
    {
        public const string ChangeLogObjectType = nameof(Category);

        private readonly Func<ICatalogRepository> _catalogRepositoryFactory;
        private readonly IChangeLogSearchService _changeLogSearchService;

        public CategoryDocumentChangesProvider(Func<ICatalogRepository> catalogRepositoryFactory, IChangeLogSearchService changeLogSearchService)
        {
            _catalogRepositoryFactory = catalogRepositoryFactory;
            _changeLogSearchService = changeLogSearchService;
        }

        public virtual async Task<long> GetTotalChangesCountAsync(DateTime? startDate, DateTime? endDate)
        {
            long result;

            if (startDate == null && endDate == null)
            {
                // Get total categories count
                using (var repository = _catalogRepositoryFactory())
                {
                    result = await repository.Categories.CountAsync();
                }
            }
            else
            {
                var criteria = new ChangeLogSearchCriteria
                {
                    ObjectType = ChangeLogObjectType,
                    StartDate = startDate,
                    EndDate = endDate,
                    Take = 0
                };
                // Get changes count from operation log
                result = (await _changeLogSearchService.SearchAsync(criteria)).TotalCount;
            }

            return result;
        }

        public virtual async Task<IList<IndexDocumentChange>> GetChangesAsync(DateTime? startDate, DateTime? endDate, long skip, long take)
        {
            IList<IndexDocumentChange> result;

            if (startDate == null && endDate == null)
            {
                // Get documents from repository and return them as changes
                using (var repository = _catalogRepositoryFactory())
                {
                    var categoryIds = await repository.Categories
                        .OrderBy(i => i.CreatedDate)
                        .Select(i => i.Id)
                        .Skip((int)skip)
                        .Take((int)take)
                        .ToArrayAsync();

                    result = categoryIds.Select(id =>
                        new IndexDocumentChange
                        {
                            DocumentId = id,
                            ChangeType = IndexDocumentChangeType.Modified,
                            ChangeDate = DateTime.UtcNow
                        }
                    ).ToArray();
                }
            }
            else
            {
                var criteria = new ChangeLogSearchCriteria
                {
                    ObjectType = ChangeLogObjectType,
                    StartDate = startDate,
                    EndDate = endDate,
                    Skip = (int)skip,
                    Take = (int)take
                };

                // Get changes from operation log
                var operations = (await _changeLogSearchService.SearchAsync(criteria)).Results;

                result = operations.Select(o =>
                    new IndexDocumentChange
                    {
                        DocumentId = o.ObjectId,
                        ChangeType = o.OperationType == EntryState.Deleted ? IndexDocumentChangeType.Deleted : IndexDocumentChangeType.Modified,
                        ChangeDate = o.ModifiedDate ?? o.CreatedDate,
                    }
                ).ToArray();
            }

            return result;
        }
    }
}
