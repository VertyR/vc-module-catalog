using System.Collections.Generic;
using VirtoCommerce.CoreModule.Core.Common;

namespace VirtoCommerce.CatalogModule.Core.Model.DynamicAssociations
{
    public class DynamicAssociationEvaluationContext : IEvaluationContext
    {
        public ICollection<CatalogProduct> Products { get; set; } = new List<CatalogProduct>();
    }
}
