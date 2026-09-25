# 画像表示

`Ee4v.Core.Images`は、エンコード済み画像をUI Toolkitで表示する共通コンポーネントです。

`CachedImageCache.SetSource(key, data)`は画像の取得結果をkey単位で保持します。空のdataも結果として保持できるため、画像がない対象を繰り返し取得する必要はありません。`CachedImage.SetSource(key)`は保持済みdataを必要なときだけTextureへデコードし、同じcache、key、dataを使う要素間でTextureを共有します。表示用TextureはmipmapとCPU側のピクセル保持を無効にし、画像一覧の読み込みとメモリ使用量を抑えます。

cacheがTextureの生成と破棄を所有します。利用側は表示単位で`CachedImageCache`を共有し、Source更新時に`Clear`、表示の破棄時に`Dispose`を呼びます。`CachedImage`自身はTextureを破棄しません。

```csharp
var cache = new CachedImageCache();
cache.SetSource("preview", pngBytes);

var image = new CachedImage(cache);
image.SetSource("preview");
root.Add(image);
```

AssetManagerは`AssetManagerView`ごとに1つのcacheを持ち、GridとItem詳細で共有します。スクロールや画面切り替えで表示要素が変わってもデコード済みTextureを再利用し、Source同期または再読み込み時だけcacheを破棄します。
