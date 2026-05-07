using System;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

namespace ResourceQuantityEditor {
	public static class ProductHelper {
		public static bool MatchesFilter(ProductProto product, string filter) {
			if (string.IsNullOrEmpty(filter)) {
				return true;
			}

			return product.Id.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
				|| product.Strings.Name.TranslatedString.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static void ValidateAmount(int amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException("amount", amount, "Amount must be non-negative.");
			}
		}

		public static ProductProto GetProduct(string productId, ProtosDb protosDb) {
			ProductProto product;
			if (!protosDb.TryGetProto(new ProductProto.ID(productId), out product)) {
				throw new ArgumentException(
					string.Format("Product '{0}' was not found. Use the product search to find valid product ids.", productId),
					"productId");
			}

			return product;
		}
	}
}
