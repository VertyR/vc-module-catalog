﻿using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Domain.Catalog.Model.Search;
using VirtoCommerce.Domain.Commerce.Model.Search;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogModule.Data.Search
{
    public class CategorySearchRequestBuilder : ISearchRequestBuilder
    {
        private readonly ISearchPhraseParser _searchPhraseParser;

        public CategorySearchRequestBuilder(ISearchPhraseParser searchPhraseParser)
        {
            _searchPhraseParser = searchPhraseParser;
        }

        public virtual string DocumentType { get; } = KnownDocumentTypes.Category;

        public virtual SearchRequest BuildRequest(SearchCriteriaBase criteria)
        {
            SearchRequest request = null;

            var categorySearchCriteria = criteria as CategorySearchCriteria;
            if (categorySearchCriteria != null)
            {
                // Getting filters modifies search phrase
                var filters = GetFilters(categorySearchCriteria);

                request = new SearchRequest
                {
                    SearchKeywords = categorySearchCriteria.SearchPhrase,
                    SearchFields = new[] { "__content" },
                    Filter = filters.And(),
                    Sorting = GetSorting(categorySearchCriteria),
                    Skip = criteria.Skip,
                    Take = criteria.Take,
                };
            }

            return request;
        }


        protected virtual IList<IFilter> GetFilters(CategorySearchCriteria criteria)
        {
            var result = new List<IFilter>();

            if (!string.IsNullOrEmpty(criteria.SearchPhrase))
            {
                var parseResult = _searchPhraseParser.Parse(criteria.SearchPhrase);
                criteria.SearchPhrase = parseResult.SearchPhrase;
                result.AddRange(parseResult.Filters);
            }

            if (criteria.ObjectIds != null)
            {
                result.Add(new IdsFilter { Values = criteria.ObjectIds });
            }

            if (!string.IsNullOrEmpty(criteria.CatalogId))
            {
                result.Add(FiltersHelper.CreateTermFilter("catalog", criteria.CatalogId.ToLowerInvariant()));
            }

            if (!criteria.Outline.IsNullOrEmpty())
            {
                var outline = string.Join("/", criteria.CatalogId, criteria.Outline).TrimEnd('/', '*').ToLowerInvariant();
                result.Add(FiltersHelper.CreateTermFilter("__outline", outline));
            }

            if (criteria.Terms != null)
            {
                var terms = criteria.Terms.AsKeyValues();
                if (terms.Any())
                {
                    foreach (var term in terms)
                    {
                        var filter = FiltersHelper.CreateTermFilter(term.Key, term.Values);
                        result.Add(filter);
                    }
                }
            }

            return result;
        }

        protected virtual IList<SortingField> GetSorting(CategorySearchCriteria criteria)
        {
            var result = new List<SortingField>();

            var categoryId = criteria.Outline.AsCategoryId();
            var priorityFieldName = StringsHelper.JoinNonEmptyStrings("_", "priority", criteria.CatalogId, categoryId).ToLowerInvariant();
            var priorityFields = new[] { "priority", priorityFieldName }.Distinct().ToArray();

            foreach (var sortInfo in criteria.SortInfos)
            {
                var fieldName = sortInfo.SortColumn.ToLowerInvariant();
                var isDescending = sortInfo.SortDirection == SortDirection.Descending;

                switch (fieldName)
                {
                    case "priority":
                        result.AddRange(priorityFields.Select(priorityField => new SortingField(priorityField, isDescending)));
                        break;
                    case "name":
                    case "title":
                        result.Add(new SortingField("name", isDescending));
                        break;
                    default:
                        result.Add(new SortingField(fieldName, isDescending));
                        break;
                }
            }

            if (!result.Any())
            {
                result.AddRange(priorityFields.Select(priorityField => new SortingField(priorityField, true)));
                result.Add(new SortingField("__sort"));
            }

            return result;
        }
    }
}
