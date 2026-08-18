using System.Collections.Generic;

namespace Ee4v.SceneSwitcher
{
    internal enum SceneOperationFailure
    {
        None,
        InvalidName,
        InvalidFolder,
        AlreadyExists,
        Failed
    }

    internal struct SceneOperationResult
    {
        public SceneOperationResult(
            bool succeeded,
            SceneOperationFailure failure = SceneOperationFailure.None,
            string path = "")
        {
            Succeeded = succeeded;
            Failure = failure;
            Path = path ?? string.Empty;
        }

        public bool Succeeded { get; }

        public SceneOperationFailure Failure { get; }

        public string Path { get; }
    }

    internal interface ISceneSwitcherGateway
    {
        IReadOnlyList<string> FindScenePaths();

        IReadOnlyList<string> GetOpenScenePaths();

        SceneOperationResult SwitchScene(
            string path,
            int sourceSceneHandle);

        SceneOperationResult AddScene(string path);

        SceneOperationResult CreateScene(
            string folder,
            string sceneName);
    }
}
