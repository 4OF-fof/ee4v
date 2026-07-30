using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Infrastructure.Datasources.Blm;
using Ee4v.AssetManager.Infrastructure.Datasources.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        private sealed class SchemaVersionRow
        {
            public int version { get; set; }
        }

        private sealed class ItemRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public int is_available { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class ShopRow
        {
            public string name { get; set; }
            public string subdomain { get; set; }
            public string thumbnail_url { get; set; }
        }

        private sealed class BoothRow
        {
            public string item_info_id { get; set; }
            public long booth_item_id { get; set; }
            public string shop_subdomain { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string thumbnail_url { get; set; }
            public string last_updated_at { get; set; }
            public string shop_name { get; set; }
            public string shop_thumbnail_url { get; set; }
        }

        private sealed class TagRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class FileRow
        {
            public string id { get; set; }
            public string item_info_id { get; set; }
            public string version_group_id { get; set; }
            public string variant_group_id { get; set; }
            public string file_name { get; set; }
            public string extension { get; set; }
            public long? size_bytes { get; set; }
            public long? download_id { get; set; }
            public string lifecycle { get; set; }
            public int is_available { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class VariantGroupRow
        {
            public string id { get; set; }
            public string item_info_id { get; set; }
            public string name { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class VersionGroupRow
        {
            public string id { get; set; }
            public string item_info_id { get; set; }
            public string variant_group_id { get; set; }
            public string name { get; set; }
            public string primary_file_info_id { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class Ee4vOriginRow
        {
            public string file_info_id { get; set; }
            public string ee4v_file_id { get; set; }
            public string file_path_cache { get; set; }
            public string imported_at { get; set; }
        }

        private sealed class EagleOriginRow
        {
            public string file_info_id { get; set; }
            public string eagle_item_id { get; set; }
            public string file_path_cache { get; set; }
            public int? is_deleted { get; set; }
            public string imported_at { get; set; }
        }

        private sealed class BlmOriginRow
        {
            public string file_info_id { get; set; }
            public string registered_item_id { get; set; }
            public string relative_path { get; set; }
            public string file_path_cache { get; set; }
            public int is_missing { get; set; }
            public string imported_at { get; set; }
        }

        private sealed class ItemSourceOriginRow
        {
            public string source_type { get; set; }
            public string source_id { get; set; }
            public string item_info_id { get; set; }
            public string source_name { get; set; }
            public string source_description { get; set; }
            public int is_missing { get; set; }
            public string imported_at { get; set; }
        }

        private sealed class DatasourceTagRow
        {
            public string name { get; set; }
        }

        private sealed class CollectionRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public string icon { get; set; }
            public string icon_asset_guid { get; set; }
            public int sort_order { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class CollectionCollectionRow
        {
            public string parent_collection_id { get; set; }
            public string child_collection_id { get; set; }
        }

        private sealed class CollectionSubtreeRow
        {
            public string id { get; set; }
            public int depth { get; set; }
        }

        private sealed class SmartCollectionRow
        {
            public string collection_info_id { get; set; }
            public string match_mode { get; set; }
        }

        private sealed class SmartConditionRow
        {
            public string collection_info_id { get; set; }
            public int sort_order { get; set; }
            public string field { get; set; }
            public string @operator { get; set; }
            public string query_text { get; set; }
        }

        private sealed class DependencyRow
        {
            public string source_file_info_id { get; set; }
            public string source_version_group_id { get; set; }
            public string source_variant_group_id { get; set; }
            public string target_file_info_id { get; set; }
            public string target_version_group_id { get; set; }
        }

        private sealed class FileImportTargetRow
        {
            public string file_info_id { get; set; }
            public string relative_path { get; set; }
        }

        private sealed class ImportedAssetGuidRow
        {
            public string item_info_id { get; set; }
            public string file_info_id { get; set; }
            public string asset_guid { get; set; }
            public int is_protected { get; set; }
            public string imported_at { get; set; }
        }

        private sealed class SyncInfoRow
        {
            public string source_type { get; set; }
            public string last_sync_at { get; set; }
            public string last_sync_status { get; set; }
        }
    }
}
