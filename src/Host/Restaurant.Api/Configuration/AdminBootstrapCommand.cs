using Restaurant.Modules.Identity.Infrastructure;
using Restaurant.Modules.Identity.Infrastructure.Initialization;

namespace Restaurant.Api.Configuration;

internal static class AdminBootstrapCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Use only --bootstrap-admin; provide credentials through environment secrets.");
            return 1;
        }
        try
        {
            var input = new AdminBootstrapInput(
                Environment.GetEnvironmentVariable("BootstrapAdmin__UserName"),
                Environment.GetEnvironmentVariable("BootstrapAdmin__Email"),
                Environment.GetEnvironmentVariable("BootstrapAdmin__Password"));
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            // No logger providers: neither SQL errors nor credentials enter application logs.
            services.AddLogging();
            services.AddIdentityModule();
            services.AddScoped<AdminBootstrapper>();
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().InitializeAsync(input);
            Console.WriteLine("Admin initialized. Remove bootstrap credentials from the process environment.");
            return 0;
        }
        catch (Exception)
        {
            // Deliberately omit exception details, which can contain connection/user data.
            Console.Error.WriteLine("Admin initialization failed. Check input, migration, password policy and prior bootstrap status. No secrets are printed.");
            return 1;
        }
    }
}
