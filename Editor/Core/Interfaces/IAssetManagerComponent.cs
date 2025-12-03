using System;
using _4OF.ee4v.AssetManager;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.AssetManager.Views;
using _4OF.ee4v.AssetManager.Views.Toast;
using UnityEngine.UIElements;

namespace _4OF.ee4v.Core.Interfaces {
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
        public AssetListService ViewController { get; set; }
        public SelectionService SelectionService { get; set; }
        public Func<VisualElement, VisualElement> ShowDialog { get; set; }
        public Action<string, float?, ToastType> ShowToast { get; set; }

        public Action<bool> RequestRefresh { get; set; }
        public Action RequestTagListRefresh { get; set; }
    }

    public interface IAssetManagerComponent : IDisposable {
        AssetManagerComponentLocation Location { get; }
        int Priority { get; }

        void Initialize(AssetManagerContext context);
        VisualElement CreateElement();
    }
}