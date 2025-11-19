using System.Collections.Generic;
using System.Threading.Tasks;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    /// <summary>
    /// アセットデータの永続化（保存・読み込み）とファイル操作を抽象化するインターフェース
    /// </summary>
    public interface IAssetRepository {
        /// <summary>
        /// リポジトリの初期化処理（ディレクトリ作成など）
        /// </summary>
        void Initialize();

        /// <summary>
        /// 全てのアセットとライブラリメタデータをロードします
        /// </summary>
        void Load();

        /// <summary>
        /// キャッシュの整合性を非同期で検証・修復します
        /// </summary>
        Task LoadAndVerifyAsync();

        /// <summary>
        /// 指定したIDのアセットメタデータを取得します
        /// </summary>
        AssetMetadata GetAsset(Ulid assetId);

        /// <summary>
        /// 全てのアセットメタデータを取得します
        /// </summary>
        IEnumerable<AssetMetadata> GetAllAssets();

        /// <summary>
        /// フォルダ構造などのライブラリメタデータを取得します
        /// </summary>
        LibraryMetadata GetLibraryMetadata();

        /// <summary>
        /// 外部ファイルから新しいアセットを作成して取り込みます
        /// </summary>
        /// <param name="sourcePath">取り込むファイルのパス</param>
        void CreateAssetFromFile(string sourcePath);

        /// <summary>
        /// ファイルを持たない空のアセットを作成します（Boothアイテム用など）
        /// </summary>
        /// <returns>作成されたアセットのメタデータ</returns>
        AssetMetadata CreateEmptyAsset();

        /// <summary>
        /// アセットのメタデータを保存（更新）します
        /// </summary>
        void SaveAsset(AssetMetadata asset);

        /// <summary>
        /// アセットの実体ファイル名を変更します
        /// </summary>
        void RenameAssetFile(Ulid assetId, string newName);

        /// <summary>
        /// アセットとそのファイルを完全に削除します
        /// </summary>
        void DeleteAsset(Ulid assetId);

        /// <summary>
        /// ライブラリ全体のメタデータ（フォルダ構造など）を保存します
        /// </summary>
        void SaveLibraryMetadata(LibraryMetadata libraryMetadata);

        /// <summary>
        /// アセットにサムネイル画像を設定します
        /// </summary>
        void SetThumbnail(Ulid assetId, string imagePath);

        /// <summary>
        /// アセットのサムネイル画像を削除します
        /// </summary>
        void RemoveThumbnail(Ulid assetId);
    }
}