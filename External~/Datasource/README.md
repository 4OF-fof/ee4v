# Datasource bridge

Run with Node.js and two explicit isolated directories:

```powershell
node server.mjs --library 'D:/isolated/folderlibrary' --downloads 'D:/isolated/downloads'
```

Use `../Scriptcat/B2ee4v.user.js` with this bridge. It listens on 127.0.0.1:48197 only
and copies completed downloads without deleting the originals. Ctrl+C stops it.
No service registration or persistent credentials are created.

Select Custom and the same folder library in AssetManager settings, then reload
AssetManager to synchronize the metadata and files into its selected database.
The Unity adapter owns SQLite writes; the bridge only owns the folder library.

See `docs~/datasources.md` for the metadata and API contracts.
