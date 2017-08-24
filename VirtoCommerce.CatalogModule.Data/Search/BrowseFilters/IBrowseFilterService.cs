﻿using System.Collections.Generic;
using VirtoCommerce.Domain.Store.Model;

namespace VirtoCommerce.CatalogModule.Data.Search.BrowseFilters
{
    public interface IBrowseFilterService
    {
        IList<IBrowseFilter> GetAllFilters(string storeId);
        void SetAllFilters(Store store, IList<IBrowseFilter> filters);
    }
}
