using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.SceneSwitcher
{
    internal sealed class SceneSwitcherController
    {
        private readonly ISceneSwitcherRepository _repository;
        private readonly ISceneSwitcherGateway _gateway;
        private List<SceneSwitcherRecord> _records;
        private string _query = string.Empty;

        public SceneSwitcherController(
            ISceneSwitcherRepository repository,
            ISceneSwitcherGateway gateway)
        {
            _repository = repository ??
                throw new ArgumentNullException(nameof(repository));
            _gateway = gateway ??
                throw new ArgumentNullException(nameof(gateway));
            _records = (_repository.Load() ??
                        Array.Empty<SceneSwitcherRecord>())
                .Where(record => record != null)
                .ToList();
            State = new SceneSwitcherViewState(
                string.Empty,
                Array.Empty<SceneSwitcherItem>(),
                false);
        }

        public event Action<SceneSwitcherViewState> StateChanged;

        public event Action<SceneOperationResult> OperationFailed;

        public SceneSwitcherViewState State { get; private set; }

        public void RefreshCatalog()
        {
            _records = SceneSwitcherPolicy.Synchronize(
                _records,
                _gateway.FindScenePaths());
            _repository.Save(_records);
            RebuildState();
        }

        public void RefreshOpenScenes()
        {
            RebuildState();
        }

        public void SetQuery(string query)
        {
            var normalized = query ?? string.Empty;
            if (string.Equals(
                    _query,
                    normalized,
                    StringComparison.Ordinal))
            {
                return;
            }

            _query = normalized;
            RebuildState();
        }

        public void ToggleFavorite(string path)
        {
            var record = FindRecord(path);
            if (record == null)
            {
                return;
            }

            record.IsFavorite = !record.IsFavorite;
            _repository.Save(_records);
            RebuildState();
        }

        public void ApplyOrder(IEnumerable<string> orderedPaths)
        {
            if (!string.IsNullOrWhiteSpace(_query))
            {
                return;
            }

            _records = SceneSwitcherPolicy.Reorder(
                _records,
                orderedPaths);
            _repository.Save(_records);
            RebuildState();
        }

        public bool Activate(
            string path,
            int sourceSceneHandle)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var result = _gateway.SwitchScene(
                path,
                sourceSceneHandle);
            if (!result.Succeeded)
            {
                OperationFailed?.Invoke(result);
                return false;
            }

            MoveToTop(path);
            RebuildState();
            return true;
        }

        public bool Add(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var result = _gateway.AddScene(path);
            if (!result.Succeeded)
            {
                OperationFailed?.Invoke(result);
                return false;
            }

            MoveToTop(path);
            RebuildState();
            return true;
        }

        public bool Create(string sceneName, string folder)
        {
            var result = _gateway.CreateScene(
                folder,
                (sceneName ?? string.Empty).Trim());
            if (!result.Succeeded)
            {
                OperationFailed?.Invoke(result);
                return false;
            }

            _records = SceneSwitcherPolicy.Synchronize(
                _records,
                _gateway.FindScenePaths());
            MoveToTop(result.Path);
            _query = string.Empty;
            RebuildState();
            return true;
        }

        private SceneSwitcherRecord FindRecord(string path)
        {
            return _records.FirstOrDefault(record =>
                string.Equals(
                    record.Path,
                    path,
                    StringComparison.Ordinal));
        }

        private void MoveToTop(string path)
        {
            var record = FindRecord(path);
            if (record == null)
            {
                return;
            }

            _records.Remove(record);
            _records.Insert(0, record);
            _repository.Save(_records);
        }

        private void RebuildState()
        {
            State = SceneSwitcherPolicy.BuildView(
                _records,
                _gateway.GetOpenScenePaths(),
                _query);
            StateChanged?.Invoke(State);
        }
    }
}
