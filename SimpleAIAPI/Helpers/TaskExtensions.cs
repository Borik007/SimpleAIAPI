namespace SimpleAIAPI.Helpers;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        });
    }
}