using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.CatalogModule.Core;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Authorization;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogModule.Web.Controllers.Api
{
    [Route("api/catalog/products")]
    [Authorize]
    public class CatalogModuleProductsController : Controller
    {
        private readonly IItemService _itemsService;
        private readonly ICatalogService _catalogService;
        private readonly ICategoryService _categoryService;
        private readonly ISkuGenerator _skuGenerator;
        private readonly IProductAssociationSearchService _productAssociationSearchService;
        private readonly IAuthorizationService _authorizationService;
        private readonly MvcNewtonsoftJsonOptions _jsonOptions;

        public CatalogModuleProductsController(
            IItemService itemsService
            , ICatalogService catalogService
            , ICategoryService categoryService
            , ISkuGenerator skuGenerator
            , IProductAssociationSearchService productAssociationSearchService
            , IAuthorizationService authorizationService
            , IOptions<MvcNewtonsoftJsonOptions> jsonOptions)
        {
            _itemsService = itemsService;
            _categoryService = categoryService;
            _catalogService = catalogService;
            _skuGenerator = skuGenerator;
            _productAssociationSearchService = productAssociationSearchService;
            _authorizationService = authorizationService;
            _jsonOptions = jsonOptions.Value;
        }


        /// <summary>
        /// Gets product by id.
        /// </summary>
        /// <param name="id">Item id.</param>
        ///<param name="respGroup">Response group.</param>
        [HttpGet]
        [Route("{id}")]
        public async Task<ActionResult<CatalogProduct>> GetProductById(string id, [FromQuery] string respGroup = null)
        {

            var product = await _itemsService.GetByIdAsync(id, respGroup);
            if (product == null)
            {
                return NotFound();
            }
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, product, new CatalogAuthorizationRequirement(ModuleConstants.Security.Permissions.Read));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }
            return Ok(product);
        }

        /// <summary>
        /// Gets products by ids
        /// </summary>
        /// <param name="ids">Item ids</param>
        ///<param name="respGroup">Response group.</param>
        [HttpGet]
        [Route("")]
        public async Task<ActionResult> GetProductByIds([FromQuery] string[] ids, [FromQuery] string respGroup = null)
        {
            var items = await _itemsService.GetByIdsAsync(ids, respGroup);
            if (items == null)
            {
                return NotFound();
            }
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, items, new CatalogAuthorizationRequirement(ModuleConstants.Security.Permissions.Read));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }
            //It is a important to return serialized data by such way. Instead you have a slow response time for large outputs 
            //https://github.com/dotnet/aspnetcore/issues/19646
            var result = JsonConvert.SerializeObject(items, _jsonOptions.SerializerSettings);

            return Content(result, "application/json");
        }

        /// <summary>
        /// Gets products by plenty ids 
        /// </summary>
        /// <param name="ids">Item ids</param>
        /// <param name="respGroup">Response group.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("plenty")]
        public async Task<ActionResult<CatalogProduct[]>> GetProductByPlentyIds([FromBody] string[] ids, [FromQuery] string respGroup = null)
        {
            return await GetProductByIds(ids, respGroup);
        }


        /// <summary>
        /// Gets the template for a new product (outside of category).
        /// </summary>
        /// <remarks>Use when need to create item belonging to catalog directly.</remarks>
        /// <param name="catalogId">The catalog id.</param>
        [HttpGet]
        [Route("~/api/catalog/{catalogId}/products/getnew")]
        public async Task<ActionResult<CatalogProduct>> GetNewProductByCatalog(string catalogId)
        {
            return await GetNewProductByCatalogAndCategory(catalogId, null);
        }


        /// <summary>
        /// Gets the template for a new product (inside category).
        /// </summary>
        /// <remarks>Use when need to create item belonging to catalog category.</remarks>
        /// <param name="catalogId">The catalog id.</param>
        /// <param name="categoryId">The category id.</param>
        [HttpGet]
        [Route("~/api/catalog/{catalogId}/categories/{categoryId}/products/getnew")]
        public async Task<ActionResult<CatalogProduct>> GetNewProductByCatalogAndCategory(string catalogId, string categoryId)
        {
            var result = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();
            result.CategoryId = categoryId;
            result.CatalogId = catalogId;
            result.IsActive = true;
            result.SeoInfos = Array.Empty<SeoInfo>();

            Entity parent = null;
            if (catalogId != null)
            {
                parent = (await _catalogService.GetByIdsAsync(new[] { catalogId })).FirstOrDefault();
            }
            if (categoryId != null)
            {
                parent = (await _categoryService.GetByIdsAsync(new[] { categoryId }, CategoryResponseGroup.WithProperties.ToString())).FirstOrDefault();
            }
            if (parent != null)
            {
                result.TryInheritFrom(parent);
            }

            if (result.Properties != null)
            {
                foreach (var property in result.Properties)
                {
                    property.Values = new List<PropertyValue>();
                    property.IsReadOnly = property.Type != PropertyType.Product && property.Type != PropertyType.Variation;
                }
            }
            result.Code = _skuGenerator.GenerateSku(result);

            return Ok(result);
        }


        /// <summary>
        /// Gets the template for a new variation.
        /// </summary>
        /// <param name="productId">The parent product id.</param>
        [HttpGet]
        [Route("{productId}/getnewvariation")]
        public async Task<ActionResult<CatalogProduct>> GetNewVariation(string productId)
        {
            var product = await _itemsService.GetByIdAsync(productId, null);
            if (product == null)
            {
                return NotFound();
            }

            var newVariation = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();

            newVariation.Name = product.Name;
            newVariation.CategoryId = product.CategoryId;
            newVariation.CatalogId = product.CatalogId;
            newVariation.MainProductId = product.MainProductId ?? productId;
            newVariation.Properties = product.Properties.Where(x => x.Type == PropertyType.Variation).ToList();

            foreach (var property in newVariation.Properties)
            {
                // Mark variation property as required
                if (property.Type == PropertyType.Variation)
                {
                    property.Required = true;
                    property.Values.Clear();
                }
            }
            newVariation.Code = _skuGenerator.GenerateSku(newVariation);
            return Ok(newVariation);
        }


        [HttpGet]
        [Route("{productId}/clone")]
        public async Task<ActionResult<CatalogProduct>> CloneProduct(string productId)
        {
            var product = await _itemsService.GetByIdAsync(productId, null);
            if (product == null)
            {
                return NotFound();
            }

            var copyProduct = product.GetCopy() as CatalogProduct;

            // Generate new SKUs and remove SEO records for product and its variations
            copyProduct.Code = _skuGenerator.GenerateSku(product);
            copyProduct.SeoInfos.Clear();

            foreach (var variation in copyProduct.Variations)
            {
                variation.Code = _skuGenerator.GenerateSku(variation);
                variation.SeoInfos.Clear();
            }

            return Ok(copyProduct);
        }

        /// <summary>
        /// Create/Update the specified product.
        /// </summary>
        /// <param name="product">The product.</param>
        [HttpPost]
        [Route("")]
        [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        public async Task<ActionResult<CatalogProduct>> SaveProduct([FromBody] CatalogProduct product)
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, product, new CatalogAuthorizationRequirement(ModuleConstants.Security.Permissions.Update));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }

            var result = (await InnerSaveProducts(new[] { product })).FirstOrDefault();
            if (result != null)
            {
                return Ok(result);
            }
            return NoContent();
        }

        /// <summary>
        /// Create/Update the specified products.
        /// </summary>
        /// <param name="products">The products.</param>
        [HttpPost]
        [Route("batch")]
        public async Task<ActionResult> SaveProducts([FromBody] CatalogProduct[] products)
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, products, new CatalogAuthorizationRequirement(ModuleConstants.Security.Permissions.Update));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }

            await InnerSaveProducts(products);
            return Ok();
        }


        /// <summary>
        /// Deletes the specified items by id.
        /// </summary>
        /// <param name="ids">The items ids.</param>
        [HttpDelete]
        [Route("")]
        public async Task<ActionResult> DeleteProduct([FromQuery] string[] ids)
        {
            var products = await _itemsService.GetByIdsAsync(ids, ItemResponseGroup.ItemInfo.ToString());
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, products, new CatalogAuthorizationRequirement(ModuleConstants.Security.Permissions.Delete));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }
            await _itemsService.DeleteAsync(ids);
            return Ok();
        }

        private async Task<CatalogProduct[]> InnerSaveProducts(CatalogProduct[] products)
        {
            var toSaveList = new List<CatalogProduct>();
            var catalogs = await _catalogService.GetByIdsAsync(products.Select(pr => pr.CatalogId).Distinct().ToArray());
            foreach (var product in products)
            {
                if (product.IsTransient() && product.SeoInfos.IsNullOrEmpty())
                {
                    var slugUrl = GenerateProductDefaultSlugUrl(product);
                    if (!string.IsNullOrEmpty(slugUrl))
                    {
                        var catalog = catalogs.FirstOrDefault(c => c.Id.EqualsInvariant(product.CatalogId));
                        var defaultLanguageCode = catalog?.Languages.First(x => x.IsDefault).LanguageCode;
                        var seoInfo = AbstractTypeFactory<SeoInfo>.TryCreateInstance();
                        seoInfo.LanguageCode = defaultLanguageCode;
                        seoInfo.SemanticUrl = slugUrl;
                        product.SeoInfos = new[] { seoInfo };
                    }
                }

                toSaveList.Add(product);
            }

            if (!toSaveList.IsNullOrEmpty())
            {
                await _itemsService.SaveChangesAsync(toSaveList.ToArray());
            }

            return toSaveList.ToArray();
        }

        private string GenerateProductDefaultSlugUrl(CatalogProduct product)
        {
            var retVal = new List<string>
            {
                product.Name
            };
            if (product.Properties != null)
            {
                //foreach (var property in product.Properties.Where(x => x.Type == PropertyType.Variation && x.Values != null))
                //{
                //    retVal.AddRange(property.Values.Select(x => x.PropertyName + "-" + x.Value));
                //}
            }
            return string.Join(" ", retVal).GenerateSlug();
        }
    }
}
