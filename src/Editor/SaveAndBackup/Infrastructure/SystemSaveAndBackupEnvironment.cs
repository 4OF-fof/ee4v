using System;
using Ee4v.SaveAndBackup.Application;

namespace Ee4v.SaveAndBackup.Infrastructure
{
    internal sealed class SystemSaveAndBackupEnvironment : ISaveAndBackupEnvironment
    {
        public string CreateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
