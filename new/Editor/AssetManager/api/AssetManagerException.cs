using System;

namespace Ee4v.AssetManager.Api
{
    public sealed class AssetManagerException : Exception
    {
        public AssetManagerException(AssetManagerErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }

        public AssetManagerException(AssetManagerErrorCode code, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
        }

        public AssetManagerErrorCode Code { get; private set; }
    }
}
