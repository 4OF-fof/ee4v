using System;
using _4OF.ee4v.AssetManager.Component;
using _4OF.ee4v.AssetManager.Core;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Interfaces {
    public enum AssetManagerComponentLocation {
        Navigation,
        MainView,
        Inspector,
        Overlay
    }

    public class AssetManagerContext {
        public IAssetRepository Repository { get; set; }
        public AssetService AssetService { get; set; }
        public FolderService FolderService { get; set; }
        public TextureService TextureService { get; set; }
        public AssetViewController ViewController { get; set; }
        public AssetSelectionModel SelectionModel { get; set; }

        // 変更点: Action<VisualElement> -> Func<VisualElement, VisualElement>
        // ダイアログを表示し、そのコンテナを返すため Func を使用します
        public Func<VisualElement, VisualElement> ShowDialog { get; set; }
        public Action<string, float?, ToastType> ShowToast { get; set; }

        public Action<bool> RequestRefresh { get; set; }
        public Action RequestTagListRefresh { get; set; }
    }

    public interface IAssetManagerComponent : IDisposable {
        string Name { get; }
        string Description { get; }
        AssetManagerComponentLocation Location { get; }
        int Priority { get; }

        void Initialize(AssetManagerContext context);
        VisualElement CreateElement();
    }
}