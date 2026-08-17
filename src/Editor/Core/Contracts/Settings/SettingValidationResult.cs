namespace Ee4v.Core.Settings
{
    public struct SettingValidationResult
    {
        public static readonly SettingValidationResult Success = new SettingValidationResult(true, string.Empty);

        public SettingValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }

        public string Message { get; }

        public static SettingValidationResult Error(string message)
        {
            return new SettingValidationResult(false, message);
        }
    }
}
