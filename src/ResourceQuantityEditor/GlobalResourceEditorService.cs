using System;
using System.Linq;
using Mafi;
using Mafi.Core;
using Mafi.Core.Economy;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

namespace ResourceQuantityEditor {

	public sealed class GlobalResourceEditorService {
		private readonly IAssetTransactionManager m_assets;
		private readonly ProtosDb m_protosDb;

		public GlobalResourceEditorService(IAssetTransactionManager assets, ProtosDb protosDb) {
			m_assets = assets;
			m_protosDb = protosDb;
		}

		public string ListGlobalProducts(string filter) {
			GlobalProductRow[] rows = GetGlobalProducts(filter).Take(250).ToArray();
			if (rows.Length == 0) {
				return "No global products found.";
			}

			return string.Join(
				Environment.NewLine,
				rows.Select(x => string.Format("{0} | {1} | available={2}", x.Product.Id, x.Product.Strings.Name.TranslatedString, x.Amount)).ToArray());
		}

		public GlobalProductRow[] GetGlobalProducts(string filter) {
			string normalizedFilter = (filter ?? string.Empty).Trim();
			return m_protosDb.All<ProductProto>()
				.Where(x => ProductHelper.MatchesFilter(x, normalizedFilter))
				.Select(x => new GlobalProductRow(x, m_assets.GetAvailableQuantityForRemoval(x).Value))
				.OrderBy(x => x.Product.Strings.Name.TranslatedString)
				.ToArray();
		}

		public string SetGlobal(string productId, int amount) {
			ProductHelper.ValidateAmount(amount);
			ProductProto product = ProductHelper.GetProduct(productId, m_protosDb);
			int current = m_assets.GetAvailableQuantityForRemoval(product).Value;

			if (amount > current) {
				StoreViaShipyard(product, amount - current);
			} else if (amount < current) {
				m_assets.RemoveAsMuchAs(new ProductQuantity(product, new Quantity(current - amount)), DestroyReason.Cheated);
			}

			return Format(product, "set");
		}

		public string AddGlobal(string productId, int amount) {
			ProductHelper.ValidateAmount(amount);
			ProductProto product = ProductHelper.GetProduct(productId, m_protosDb);
			StoreViaShipyard(product, amount);
			return Format(product, "added");
		}

		public string RemoveGlobal(string productId, int amount) {
			ProductHelper.ValidateAmount(amount);
			ProductProto product = ProductHelper.GetProduct(productId, m_protosDb);
			m_assets.RemoveAsMuchAs(new ProductQuantity(product, new Quantity(amount)), DestroyReason.Cheated);
			return Format(product, "removed");
		}

		private string Format(ProductProto product, string action) {
			return string.Format(
				"Global product {0} {1}: available={2}",
				product.Id,
				action,
				m_assets.GetAvailableQuantityForRemoval(product).Value);
		}

		private void StoreViaShipyard(ProductProto product, int amount) {
			if (amount <= 0) {
				return;
			}

			m_assets.StoreValue(product.Id.ToAssetValue(amount, m_protosDb), CreateReason.Cheated);
		}
	}

	public struct GlobalProductRow {
		public readonly ProductProto Product;
		public readonly int Amount;

		public GlobalProductRow(ProductProto product, int amount) {
			Product = product;
			Amount = amount;
		}
	}
}
