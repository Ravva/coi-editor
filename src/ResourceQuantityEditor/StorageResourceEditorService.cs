using System;
using System.Linq;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Entities;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

namespace ResourceQuantityEditor {

	public sealed class StorageResourceEditorService {
		private readonly IEntitiesManager m_entities;
		private readonly ProtosDb m_protosDb;

		public StorageResourceEditorService(IEntitiesManager entities, ProtosDb protosDb) {
			m_entities = entities;
			m_protosDb = protosDb;
		}

		public string ListStorages() {
			Storage[] storages = GetStorages();
			if (storages.Length == 0) {
				return "No storages found.";
			}

			return string.Join(
				Environment.NewLine,
				storages.Select(storage => {
					string productId = storage.StoredProduct.HasValue
						? storage.StoredProduct.ValueOrThrow("Storage product was empty.").Id.ToString()
						: "<empty>";
					return string.Format(
						"{0}: {1} | product={2} | quantity={3}/{4}",
						storage.Id,
						storage,
						productId,
						storage.CurrentQuantity.Value,
						storage.Capacity.Value);
				}).ToArray());
		}

		public Storage[] GetStorages() {
			return m_entities.GetAllEntitiesOfType<Storage>().OrderBy(x => x.Id.Value).ToArray();
		}

		public string ListProducts(string filter) {
			ProductProto[] products = GetProducts(filter).Take(250).ToArray();

			if (products.Length == 0) {
				return "No storable products found.";
			}

			return string.Join(
				Environment.NewLine,
				products.Select(x => string.Format("{0} | {1}", x.Id, x.Strings.Name)).ToArray());
		}

		public ProductProto[] GetProducts(string filter) {
			string normalizedFilter = (filter ?? string.Empty).Trim();
			return m_protosDb.All<ProductProto>()
				.Where(x => x.IsStorable)
				.Where(x => MatchesFilter(x, normalizedFilter))
				.OrderBy(x => x.Id.ToString())
				.ToArray();
		}

		public string SetStorage(EntityId storageId, string productId, int amount, bool allowReplaceNonEmpty) {
			Storage storage = GetStorage(storageId);
			ProductProto product = GetProduct(productId);
			ValidateAmount(amount);

			if (storage.StoredProduct.HasValue) {
				ProductProto currentProduct = storage.StoredProduct.ValueOrThrow("Storage product was empty.");
				if (!currentProduct.Equals(product)) {
					if (storage.CurrentQuantity.Value > 0 && !allowReplaceNonEmpty) {
						return string.Format(
							"Storage {0} contains {1}. Empty it first or enable allow_replace_non_empty_storage.",
							storage.Id,
							currentProduct.Id);
					}

					storage.RemoveAsMuchAs(storage.CurrentQuantity);
					if (!storage.AssignProduct(product)) {
						return string.Format("Storage {0} does not support product {1}.", storage.Id, product.Id);
					}
				}
			} else if (!storage.AssignProduct(product)) {
				return string.Format("Storage {0} does not support product {1}.", storage.Id, product.Id);
			}

			int current = storage.CurrentQuantity.Value;
			if (amount > current) {
				storage.AddAsMuchAs(new Quantity(amount - current));
			} else if (amount < current) {
				storage.RemoveAsMuchAs(new Quantity(current - amount));
			}

			return FormatStorage(storage, "set");
		}

		public string AddStorage(EntityId storageId, int amount) {
			ValidateAmount(amount);
			Storage storage = GetStorage(storageId);
			storage.AddAsMuchAs(new Quantity(amount));
			return FormatStorage(storage, "added");
		}

		public string RemoveStorage(EntityId storageId, int amount) {
			ValidateAmount(amount);
			Storage storage = GetStorage(storageId);
			storage.RemoveAsMuchAs(new Quantity(amount));
			return FormatStorage(storage, "removed");
		}

		public string FillStorage(EntityId storageId) {
			Storage storage = GetStorage(storageId);
			if (!storage.StoredProduct.HasValue) {
				return string.Format("Storage {0} has no assigned product.", storage.Id);
			}

			storage.AddAsMuchAs(new Quantity(Math.Max(0, storage.Capacity.Value - storage.CurrentQuantity.Value)));
			return FormatStorage(storage, "filled");
		}

		public string EmptyStorage(EntityId storageId) {
			Storage storage = GetStorage(storageId);
			storage.RemoveAsMuchAs(storage.CurrentQuantity);
			return FormatStorage(storage, "emptied");
		}

		private Storage GetStorage(EntityId id) {
			return m_entities.GetEntity<Storage>(id).ValueOrThrow("Storage not found.");
		}

		private ProductProto GetProduct(string productId) {
			ProductProto product;
			if (!m_protosDb.TryGetProto(new ProductProto.ID(productId), out product)) {
				throw new ArgumentException(
					string.Format("Product '{0}' was not found. Use rqe_list_products or rqe_list_products <filter> to find product ids.", productId),
					"productId");
			}

			return product;
		}

		private static bool MatchesFilter(ProductProto product, string filter) {
			if (string.IsNullOrEmpty(filter)) {
				return true;
			}

			return product.Id.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
				|| product.Strings.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void ValidateAmount(int amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException("amount", amount, "Amount must be non-negative.");
			}
		}

		private static string FormatStorage(Storage storage, string action) {
			string productId = storage.StoredProduct.HasValue
				? storage.StoredProduct.ValueOrThrow("Storage product was empty.").Id.ToString()
				: "<empty>";
			return string.Format(
				"Storage {0} {1}: product={2}, quantity={3}/{4}",
				storage.Id,
				action,
				productId,
				storage.CurrentQuantity.Value,
				storage.Capacity.Value);
		}
	}
}
