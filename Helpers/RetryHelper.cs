using System;
using System.Threading.Tasks;

namespace GitVault.Helpers
{
    public static class RetryHelper
    {
        private const string SRC = "Retry";

        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            int maxRetries = 3,
            int delaySeconds = 5,
            string operationName = "")
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        LogHelpers.Error(
                            $"{operationName} - {maxRetries} denemede de basarisiz oldu: {ex.Message}",
                            LogCategory.Service, SRC);
                        throw;
                    }

                    LogHelpers.Warn(
                        $"{operationName} - Deneme {attempt}/{maxRetries} basarisiz: {ex.Message}. " +
                        $"{delaySeconds} saniye sonra tekrar denenecek...",
                        LogCategory.Service, SRC);

                    await Task.Delay(delaySeconds * 1000);
                }
            }

            // Buraya ulasilmamali
            throw new InvalidOperationException("Retry dongusunden beklenmedik cikis");
        }

        public static async Task ExecuteAsync(
            Func<Task> action,
            int maxRetries = 3,
            int delaySeconds = 5,
            string operationName = "")
        {
            await ExecuteAsync(async () =>
            {
                await action();
                return true;
            }, maxRetries, delaySeconds, operationName);
        }
    }
}
