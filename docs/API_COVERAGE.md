# Linnworks API Coverage Matrix

This living matrix tracks the coverage of Linnworks REST API endpoints in `LinnworksMcp`.

| Linnworks Endpoint | Application Service Method | MCP Tool Name | Tool Type | Status |
|---|---|---|---|---|
| **Inventory** | | | | |
| `POST /api/Inventory/GetInventoryItems` | `InventoryService.GetInventoryItemsAsync` | `get_inventory_items` | ReadOnly | Implemented / Tested |
| `POST /api/Inventory/GetInventoryItemById` | `InventoryService.GetInventoryItemByIdAsync` | `get_inventory_item_by_id` | ReadOnly | Implemented / Tested |
| `POST /api/Inventory/GetLowStockLevel` | `InventoryService.GetLowStockItemsAsync` | `get_low_stock_items` | ReadOnly | Implemented / Tested |
| `POST /api/Inventory/AddInventoryItem` | `InventoryService.CreateInventoryItemAsync` | `create_inventory_item` | Mutates | TODO |
| `POST /api/Inventory/UpdateInventoryItem` | `InventoryService.UpdateInventoryItemAsync` | `update_inventory_item` | Mutates | TODO |
| `POST /api/Inventory/DeleteInventoryItems` | `InventoryService.DeleteInventoryItemsAsync` | `delete_inventory_item` | Mutates | TODO |
| `POST /api/Inventory/GetInventoryItemPrices` | `InventoryService.GetInventoryItemPricesAsync` | `get_inventory_item_prices` | ReadOnly | TODO |
| `POST /api/Inventory/UpdateInventoryItemPrices` | `InventoryService.UpdateInventoryItemPricesAsync` | `update_inventory_item_prices` | Mutates | TODO |
| **Stock** | | | | |
| `POST /api/Stock/GetStockLevel` | `StockService.GetStockLevelsAsync` | `get_stock_levels` | ReadOnly | Implemented / Tested |
| `POST /api/Stock/SetStockLevel` | `StockService.SetStockLevelsAsync` | `update_stock_levels` | Mutates | Implemented / Tested |
| `POST /api/Stock/GetItemChangesHistory` | `StockService.GetStockLevelHistoryAsync` | `get_stock_level_history` | ReadOnly | TODO |
| `POST /api/Stock/BatchStockLevelDelta` | `StockService.SetStockItemBatchAsync` | `set_stock_item_batch` | Mutates | TODO |
| **Orders** | | | | |
| `POST /api/Orders/GetOpenOrders` | `OrderService.GetOpenOrdersAsync` | `get_open_orders` | ReadOnly | Implemented / Tested |
| `POST /api/Orders/GetOrdersById` | `OrderService.GetOrdersByIdAsync` | `get_order_by_id` | ReadOnly | Implemented / Tested |
| `POST /api/Orders/GetOpenOrders` + Detail | `OrderService.GetUnfulfilledOrdersAsync` | `get_unfulfilled_orders` | ReadOnly | Implemented / Tested |
| `POST /api/ProcessedOrders/SearchProcessedOrders` | `OrderService.GetProcessedOrdersAsync` | `get_processed_orders` | ReadOnly | TODO |
| `POST /api/ProcessedOrders/SearchProcessedOrdersPaged` | `OrderService.SearchOrdersAsync` | `search_orders` | ReadOnly | TODO |
| `POST /api/Orders/AddOrderNote` | `OrderService.AddOrderNoteAsync` | `add_order_note` | Mutates | TODO |
| `POST /api/Orders/GetOrderShippingInfo` | `ShippingService.GetTrackingInfoAsync` | `get_order_shipping_info` | ReadOnly | Implemented / Tested |
| **Locations / Warehouse** | | | | |
| `POST /api/Inventory/GetStockLocations` | `LocationService.GetLocationsAsync` | `get_locations` | ReadOnly | Implemented / Tested |
| `POST /api/Inventory/GetStockLocationById` | `LocationService.GetLocationByIdAsync` | `get_location_by_id` | ReadOnly | Implemented / Tested |
| `POST /api/Stock/GetStockLevel` | `StockService.GetStockLevelsAsync` | `get_stock_by_location` | ReadOnly | Implemented / Tested |
| **Listings** | | | | |
| `POST /api/Listings/GetListingsBySKU` | `ListingService.GetListingsAsync` | `get_listings` | ReadOnly | Implemented / Tested |
| `POST /api/Listings/GetInventoryItemListings` | `ListingService.GetListingByIdAsync` | `get_listing_by_id` | ReadOnly | Implemented / Tested |
| `POST /api/Listings/GetListingErrors` | `ListingService.GetChannelListingErrorsAsync` | `get_channel_listing_errors` | ReadOnly | Implemented / Tested |
| **Customers** | | | | |
| `POST /api/Customers/SearchCustomers` | `CustomerService.SearchCustomersAsync` | `search_customers` | ReadOnly | Implemented / Tested |
| `POST /api/Customers/GetCustomerById` | `CustomerService.GetCustomerByIdAsync` | `get_customer_by_id` | ReadOnly | Implemented / Tested |
| **Shipping** | | | | |
| `POST /api/PostalServices/GetPostalServices` | `ShippingService.GetShippingServicesAsync` | `get_shipping_services` | ReadOnly | Implemented / Tested |
| **Purchase Orders** | | | | |
| `POST /api/PurchaseOrder/GetPurchaseOrders` | `PurchaseOrderService.GetPurchaseOrdersAsync` | `get_purchase_orders` | ReadOnly | Implemented / Tested |
| `POST /api/PurchaseOrder/CreatePurchaseOrder` | `PurchaseOrderService.CreatePurchaseOrderAsync` | `create_purchase_order` | Mutates | Implemented / Tested |
| **Returns / RMA** | | | | |
| `POST /api/Returns/SearchReturns` | `ReturnService.GetReturnsAsync` | `get_returns` | ReadOnly | Implemented / Tested |
| `POST /api/Returns/CreateReturn` | `ReturnService.CreateReturnAsync` | `create_return` | Mutates | Implemented / Tested |
