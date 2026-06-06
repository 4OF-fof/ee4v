using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Api.Connecter.Blm;
using Ee4v.AssetManager.Api.Connecter.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        private sealed class ItemRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class ShopRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public string subdomain { get; set; }
            public string thumbnail_url { get; set; }
        }

        private sealed class BoothRow
        {
            public string id { get; set; }
            public string item_info_id { get; set; }
            public long booth_item_id { get; set; }
            public string shop_info_id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string thumbnail_url { get; set; }
            public string last_updated_at { get; set; }
            public string shop_name { get; set; }
            public string shop_subdomain { get; set; }
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
            public string file_name { get; set; }
            public string extension { get; set; }
            public long? size_bytes { get; set; }
            public long? download_id { get; set; }
            public int is_primary { get; set; }
            public string lifecycle { get; set; }
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
            public string imported_at { get; set; }
        }

        private sealed class CollectionRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class CollectionCollectionRow
        {
            public string parent_collection_id { get; set; }
            public string child_collection_id { get; set; }
            public string created_at { get; set; }
        }

        private sealed class SmartCollectionRow
        {
            public string collection_info_id { get; set; }
            public string match_mode { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
        }

        private sealed class SmartConditionRow
        {
            public string id { get; set; }
            public string collection_info_id { get; set; }
            public string field { get; set; }
            public string @operator { get; set; }
            public string query_text { get; set; }
        }

        private sealed class FileDependencyRow
        {
            public string dependent_file_info_id { get; set; }
            public string dependency_file_info_id { get; set; }
            public string dependency_type { get; set; }
        }

        private sealed class SyncInfoRow
        {
            public string source_type { get; set; }
            public string last_sync_at { get; set; }
            public string last_sync_status { get; set; }
        }

        private sealed class TableInfoRow
        {
            public string name { get; set; }
        }
    }
}
